using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Core;

public sealed record ManualInstanceResult(bool Success, bool AlreadyOpen, string Message);

public sealed class ManualInstanceManager
{
	private readonly BotConfig _config;
	private readonly ConcurrentDictionary<int, Process> _processes = new();
	private readonly ConcurrentDictionary<int, SemaphoreSlim> _instanceLocks = new();

	public ManualInstanceManager(BotConfig config) => _config = config;

	public bool CanManage(int instanceId)
	{
		return instanceId >= 1 && instanceId <= MaximumInstanceCount &&
			(!OperatingSystem.IsLinux() || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))) &&
			Directory.Exists(AppDataPaths.ProfilePath(instanceId)) &&
			!string.IsNullOrWhiteSpace(PlaywrightHelper.FindChromiumPath());
	}

	public bool IsOpen(int instanceId)
	{
		if (!_processes.TryGetValue(instanceId, out Process? process)) return false;
		try
		{
			if (!process.HasExited) return true;
		}
		catch { }
		if (_processes.TryRemove(instanceId, out Process? exited)) exited.Dispose();
		return false;
	}

	public IReadOnlyList<int> OpenInstanceIds => _processes.Keys.Where(IsOpen).OrderBy(id => id).ToList();

	public int MaximumInstanceCount
	{
		get
		{
			string configured = Environment.GetEnvironmentVariable("TELEGRAM_MAX_CHROMIUM_INSTANCES") ?? "";
			int requested = int.TryParse(configured, out int value) ? value : _config.MaxSupportedInstanceCount;
			return Math.Min(BotConfig.AbsoluteMaximumInstanceCount, Math.Max(1, requested));
		}
	}

	public async Task<ManualInstanceResult> OpenAsync(int instanceId, CancellationToken cancellationToken = default)
	{
		SemaphoreSlim instanceLock = _instanceLocks.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
		await instanceLock.WaitAsync(cancellationToken);
		try
		{
			if (IsOpen(instanceId))
			{
				await FocusAsync(_processes[instanceId], cancellationToken);
				return new ManualInstanceResult(true, true, $"Instance {instanceId} is already open; its existing browser was reused.");
			}
			if (instanceId < 1 || instanceId > MaximumInstanceCount)
				return new ManualInstanceResult(false, false, $"Instance ID must be from 1 to {MaximumInstanceCount}.");
			string profilePath = AppDataPaths.ProfilePath(instanceId);
			if (!Directory.Exists(profilePath))
				return new ManualInstanceResult(false, false, $"Instance {instanceId} profile is missing: {profilePath}. Create its profile through normal browser setup first.");
			string? chromiumPath = PlaywrightHelper.FindChromiumPath();
			if (string.IsNullOrWhiteSpace(chromiumPath) || !File.Exists(chromiumPath))
				return new ManualInstanceResult(false, false, "Chromium executable was not found.");
			if (OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
				return new ManualInstanceResult(false, false, "DISPLAY is not set. Start the VNC desktop and retry.");

			var startInfo = new ProcessStartInfo
			{
				FileName = chromiumPath,
				WorkingDirectory = Path.GetDirectoryName(chromiumPath) ?? AppContext.BaseDirectory,
				UseShellExecute = false
			};
			startInfo.ArgumentList.Add("--user-data-dir=" + profilePath);
			startInfo.ArgumentList.Add("--no-first-run");
			startInfo.ArgumentList.Add("--no-default-browser-check");
			startInfo.ArgumentList.Add("--no-sandbox");
			startInfo.ArgumentList.Add("--disable-dev-shm-usage");
			startInfo.ArgumentList.Add($"--window-size={_config.BrowserWidth},{_config.BrowserHeight}");
			startInfo.ArgumentList.Add($"--window-position={50 + (instanceId - 1) * 60},{50 + (instanceId - 1) * 60}");
			startInfo.ArgumentList.Add("--new-window");
			startInfo.ArgumentList.Add(_config.TelegramWebUrl);

			Process? process = Process.Start(startInfo);
			if (process == null) return new ManualInstanceResult(false, false, "Chromium did not start.");
			_processes[instanceId] = process;
			await Task.Delay(900, cancellationToken);
			if (process.HasExited)
			{
				int exitCode = process.ExitCode;
				_processes.TryRemove(instanceId, out _);
				process.Dispose();
				return new ManualInstanceResult(false, false, $"Chromium exited during startup (code {exitCode}). Check the VNC display and profile permissions.");
			}
			await FocusAsync(process, cancellationToken);
			return new ManualInstanceResult(true, false, $"Instance {instanceId} is open on the VNC desktop.");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception ex) { return new ManualInstanceResult(false, false, $"Could not open Instance {instanceId}: {ex.Message}"); }
		finally { instanceLock.Release(); }
	}

	public async Task<ManualInstanceResult> CloseAsync(int instanceId, CancellationToken cancellationToken = default)
	{
		SemaphoreSlim instanceLock = _instanceLocks.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
		await instanceLock.WaitAsync(cancellationToken);
		try
		{
			if (!IsOpen(instanceId)) return new ManualInstanceResult(false, false, $"Instance {instanceId} is already closed.");
			Process process = _processes[instanceId];
			try { process.CloseMainWindow(); } catch { }
			try { await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken); }
			catch (TimeoutException)
			{
				if (!process.HasExited) process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
			}
			if (!process.HasExited) return new ManualInstanceResult(false, false, $"Instance {instanceId} did not close; its process handle is retained.");
			_processes.TryRemove(instanceId, out _);
			process.Dispose();
			return new ManualInstanceResult(true, false, $"Instance {instanceId} is closed. Its persistent profile was kept.");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception ex) { return new ManualInstanceResult(false, false, $"Could not close Instance {instanceId}: {ex.Message}"); }
		finally { instanceLock.Release(); }
	}

	public async Task CloseAllAsync()
	{
		foreach (int instanceId in OpenInstanceIds)
			await CloseAsync(instanceId);
	}

	private static async Task FocusAsync(Process process, CancellationToken cancellationToken)
	{
		if (OperatingSystem.IsWindows())
		{
			try
			{
				process.Refresh();
				if (process.MainWindowHandle != IntPtr.Zero)
				{
					ShowWindow(process.MainWindowHandle, 9);
					SetForegroundWindow(process.MainWindowHandle);
				}
			}
			catch { }
			return;
		}
		if (!OperatingSystem.IsLinux()) return;
		try
		{
			var searchInfo = new ProcessStartInfo { FileName = "xdotool", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
			searchInfo.ArgumentList.Add("search");
			searchInfo.ArgumentList.Add("--onlyvisible");
			searchInfo.ArgumentList.Add("--pid");
			searchInfo.ArgumentList.Add(process.Id.ToString());
			using Process search = Process.Start(searchInfo)!;
			string windowId = (await search.StandardOutput.ReadToEndAsync(cancellationToken)).Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
			await search.WaitForExitAsync(cancellationToken);
			if (search.ExitCode != 0 || windowId.Length == 0) return;
			var focusInfo = new ProcessStartInfo { FileName = "xdotool", UseShellExecute = false, RedirectStandardError = true };
			focusInfo.ArgumentList.Add("windowactivate");
			focusInfo.ArgumentList.Add("--sync");
			focusInfo.ArgumentList.Add(windowId);
			using Process focus = Process.Start(focusInfo)!;
			await focus.WaitForExitAsync(cancellationToken);
		}
		catch { }
	}

	[DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr windowHandle);
	[DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr windowHandle, int command);
}
