using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
	private readonly SemaphoreSlim _registryLock = new(1, 1);

	public ManualInstanceManager(BotConfig config) => _config = config;

	public bool CanManage(int instanceId)
	{
		return GetRegisteredInstanceIds().Contains(instanceId) && IsRuntimeAvailable() &&
			Directory.Exists(AppDataPaths.ProfilePath(instanceId));
	}

	private bool IsRuntimeAvailable()
	{
		return
			(!OperatingSystem.IsLinux() || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))) &&
			!string.IsNullOrWhiteSpace(PlaywrightHelper.FindChromiumPath());
	}

	public IReadOnlyList<int> GetRegisteredInstanceIds()
	{
		var ids = new HashSet<int>();
		try
		{
			if (File.Exists(AppDataPaths.ManualInstancesFile))
			{
				int[]? saved = JsonSerializer.Deserialize<int[]>(File.ReadAllText(AppDataPaths.ManualInstancesFile));
				if (saved != null) foreach (int id in saved) if (id >= 1 && id <= MaximumInstanceCount) ids.Add(id);
			}
		}
		catch (Exception ex) { Logger.Warning("Could not read manual instance registry: " + ex.Message); }
		// Adopt existing profiles without scanning every possible ID when the configured limit is large.
		try
		{
			if (Directory.Exists(AppDataPaths.ProfilesFolder))
			{
				foreach (string profileDirectory in Directory.EnumerateDirectories(AppDataPaths.ProfilesFolder, "profile_*", SearchOption.TopDirectoryOnly))
				{
					string name = Path.GetFileName(profileDirectory);
					if (name.StartsWith("profile_", StringComparison.OrdinalIgnoreCase) &&
						int.TryParse(name.Substring("profile_".Length), out int id) && id >= 1 && id <= MaximumInstanceCount &&
						string.Equals(name, $"profile_{id}", StringComparison.OrdinalIgnoreCase)) ids.Add(id);
				}
			}
		}
		catch (Exception ex) { Logger.Warning("Could not discover existing instance profiles: " + ex.Message); }
		return ids.OrderBy(id => id).ToList();
	}

	public bool CanCreateInstance => FindNextAvailableId() != null;

	public async Task<ManualInstanceResult> CreateAndOpenNextAsync(CancellationToken cancellationToken = default)
	{
		int instanceId;
		await _registryLock.WaitAsync(cancellationToken);
		try
		{
			if (!IsRuntimeAvailable())
				return new ManualInstanceResult(false, false, OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
					? "DISPLAY is not set. Start the VNC desktop and retry."
					: "Chromium executable was not found.");
			instanceId = FindNextAvailableId() ?? 0;
			if (instanceId == 0)
				return new ManualInstanceResult(false, false, $"The configured manual instance limit of {MaximumInstanceCount} has been reached.");
			string profilePath = AppDataPaths.ProfilePath(instanceId);
			if (Directory.Exists(profilePath))
				return new ManualInstanceResult(false, false, $"Instance {instanceId} already has a profile and was registered as an existing instance.");
			Directory.CreateDirectory(profilePath);
			var ids = GetRegisteredInstanceIds().Append(instanceId).Distinct().OrderBy(id => id).ToArray();
			AppDataPaths.EnsureDirectories();
			string tempFile = AppDataPaths.ManualInstancesFile + ".tmp";
			File.WriteAllText(tempFile, JsonSerializer.Serialize(ids));
			File.Move(tempFile, AppDataPaths.ManualInstancesFile, overwrite: true);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception ex) { return new ManualInstanceResult(false, false, "Could not register a new instance: " + ex.Message); }
		finally { _registryLock.Release(); }

		ManualInstanceResult opened = await OpenAsync(instanceId, cancellationToken);
		return opened.Success
			? new ManualInstanceResult(true, false, $"Created and opened Instance {instanceId} on the VNC desktop. Its profile will be kept after Finish.")
			: new ManualInstanceResult(false, false, $"Instance {instanceId} was registered with its persistent profile, but Chromium did not open: {opened.Message} You can retry from the instance menu.");
	}

	private int? FindNextAvailableId()
	{
		int candidate = 1;
		foreach (int id in GetRegisteredInstanceIds())
		{
			if (id < candidate) continue;
			if (id > candidate) break;
			if (candidate == int.MaxValue) return null;
			candidate++;
		}
		return candidate <= MaximumInstanceCount ? candidate : null;
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
			return BotConfig.ResolveMaximumInstanceCount(_config.MaxSupportedInstanceCount);
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
			if (instanceId < 1 || instanceId > MaximumInstanceCount || !GetRegisteredInstanceIds().Contains(instanceId))
				return new ManualInstanceResult(false, false, $"Instance {instanceId} is not registered for manual management.");
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
			int xOffset = ((instanceId - 1) % 10) * 60;
			int yOffset = (((instanceId - 1) / 10) % 10) * 60;
			startInfo.ArgumentList.Add($"--window-position={50 + xOffset},{50 + yOffset}");
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
