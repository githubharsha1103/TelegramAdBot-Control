using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Core;

public class InstanceManager
{
	private readonly BotConfig _config;

	private readonly List<BotEngine> _engines = new List<BotEngine>();

	private int _finishedCount;

	private readonly object _finishLock = new object();
	private readonly SemaphoreSlim _cleanupLock = new SemaphoreSlim(1, 1);
	private bool _cleanupVerified;

	public bool AnyBrowserAlive => _engines.Any((BotEngine e) => e.IsBrowserAlive);

	public bool HasBrowserResources => _engines.Any((BotEngine e) => e.HasBrowserResources);

	public bool AnyRunning => _engines.Any((BotEngine e) => e.IsRunning);

	public bool AllFinished => _engines.All((BotEngine e) => e.IsFinished);

	public IReadOnlyList<BotEngine> Engines => _engines;

	public event Action? OnAllFinished;

	public InstanceManager(BotConfig config)
	{
		_config = config;
		for (int i = 1; i <= config.InstanceCount; i++)
		{
			BotEngine engine = new BotEngine(config, i);
			engine.OnFinished += HandleEngineFinished;
			_engines.Add(engine);
		}
	}

	private void HandleEngineFinished(int instanceId)
	{
		int count;
		lock (_finishLock)
		{
			_finishedCount++;
			count = _finishedCount;
		}
		Logger.Warning($"Browser closed. ({count}/{_engines.Count} done)", instanceId);
		if (count >= _engines.Count)
		{
			Logger.AllDone();
			this.OnAllFinished?.Invoke();
		}
	}

	public async Task OpenAllAsync()
	{
		if (_config.BotList.Count == 0)
		{
			Logger.Error("Bot list empty.");
			return;
		}
		TelegramController.CloneSessionIfNeeded(_config.InstanceCount);
		Logger.Info($"PHASE 1: Launching {_config.InstanceCount} browsers");
		for (int i = 0; i < _engines.Count; i++)
		{
			BotEngine engine = _engines[i];
			if (!engine.IsBrowserAlive)
			{
				Logger.Info($"Launching browser {engine.InstanceId}...");
				if (!(await engine.PrepareBrowserAsync()))
				{
					Logger.Error($"Instance {engine.InstanceId} launch failed.", engine.InstanceId);
				}
				if (i < _engines.Count - 1)
				{
					await Task.Delay(2000);
				}
			}
		}
		Logger.Info("All browsers launched. Waiting 3s for settle...");
		await Task.Delay(3000);
		Logger.Info("PHASE 2: Navigating all to @" + _config.BotList[0]);
		await Task.WhenAll((from e in _engines
			where e.IsBrowserAlive
			select e.NavigateToFirstBotAsync()).ToList());
		Logger.Success("All instances ready and navigated.");
	}

	public async Task OpenFirstInstanceOnlyAsync()
	{
		if (_config.BotList.Count == 0)
		{
			Logger.Error("Bot list empty.");
			return;
		}
		Logger.Info("Opening ONLY instance 1 for first-time setup -> @" + _config.BotList[0]);
		BotEngine first = _engines[0];
		if (!first.IsBrowserAlive)
		{
			if (!(await first.PrepareAsync()))
			{
				Logger.Error("Instance 1 failed.", 1);
			}
			else
			{
				Logger.Success("Instance 1 ready. Pick your positions now.");
			}
		}
	}

	public async Task CloseFirstInstanceAsync()
	{
		BotEngine first = _engines[0];
		if (first.IsBrowserAlive)
		{
			Logger.Info("Closing setup browser...");
			await first.FullStopAsync();
		}
	}

	public async Task StartAllAsync()
	{
		if (!AnyBrowserAlive)
		{
			Logger.Error("No browsers open.");
			return;
		}
		if (!_config.AllSet)
		{
			PrintMissingPositions();
			return;
		}
		Logger.Info($"Starting all (stagger: {_config.StaggerDelayMs}ms)...");
		List<Task> tasks = new List<Task>();
		int delayStep = 0;
		for (int i = 0; i < _engines.Count; i++)
		{
			BotEngine engine = _engines[i];
			if (engine.IsBrowserAlive && !engine.IsFinished)
			{
				int delay = delayStep * _config.StaggerDelayMs;
				delayStep++;
				tasks.Add(engine.StartLoopAsync(delay));
			}
		}
		await Task.WhenAll(tasks);
	}

	public async Task OpenSequentialBrowserAsync()
	{
		if (_engines.Count != 1) throw new InvalidOperationException("Sequential account mode requires one browser engine.");
		if (!await _engines[0].PrepareBrowserAsync()) throw new InvalidOperationException("Chromium browser could not be prepared.");
	}

	public async Task<bool> SwitchAccountAsync(int accountNumber)
	{
		if (_engines.Count != 1 || !_engines[0].IsBrowserAlive) return false;
		return await _engines[0].SwitchAccountAsync(accountNumber);
	}

	public bool IsPaused => _engines.Count == 1 && _engines[0].IsPaused;
	public void ResumeSequential() { if (_engines.Count == 1) _engines[0].ResumePause(); }
	public async Task<bool> StartSequentialAsync(int initialDelayMs = 0) => _engines.Count == 1 && await _engines[0].StartLoopAsync(initialDelayMs);
	public async Task<bool> InitializeSequentialAsync() => _engines.Count == 1 && await _engines[0].InitializeCurrentAccountAsync();
	public void ConfigureAccounts(IReadOnlyList<int> accounts) { foreach (BotEngine engine in _engines) engine.ConfigureAccounts(accounts); }

	public void SetRunForNextAccount()
	{
		if (_engines.Count == 1) _engines[0].ResetForNextAccount();
	}

	public void SetAccountCompletionHandler(Func<int, Task<bool>> handler)
	{
		if (_engines.Count == 1) _engines[0].OnAccountWorkFinished += handler;
	}

	private void PrintMissingPositions()
	{
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("  ╔══ CANNOT START ═════════════════════════════════════╗");
		Console.WriteLine("  ║   Missing click positions:                          ║");
		Console.WriteLine("  ╠══════════════════════════════════════════════════════╣");
		PrintPosLine("Textbox     ", _config.TextboxX, _config.TextboxY, "Ctrl+Alt+T");
		PrintPosLine("EmojiButton ", _config.EmojiButtonX, _config.EmojiButtonY, "Ctrl+Alt+E");
		PrintPosLine("StickerTab  ", _config.StickerTabX, _config.StickerTabY, "Ctrl+Alt+F");
		PrintPosLine("Sticker     ", _config.StickerX, _config.StickerY, "Ctrl+Alt+S");
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}

	private void PrintPosLine(string name, int x, int y, string hotkey)
	{
		int num;
		int foregroundColor;
		if (x > 0)
		{
			num = ((y > 0) ? 1 : 0);
			if (num != 0)
			{
				foregroundColor = 10;
				goto IL_0014;
			}
		}
		else
		{
			num = 0;
		}
		foregroundColor = 12;
		goto IL_0014;
		IL_0014:
		Console.ForegroundColor = (ConsoleColor)foregroundColor;
		string mark = ((num != 0) ? "✓" : "✗");
		string value = ((num != 0) ? $"({x}, {y})" : "NOT SET");
		string hint = ((num != 0) ? "" : (" — press " + hotkey));
		Console.Write($"  ║   {mark}  {name}: {value}{hint}".PadRight(56));
		Console.WriteLine("║");
	}

	public void PauseAll()
	{
		foreach (BotEngine engine in _engines)
		{
			engine.Pause();
		}
	}

	public Task StopAllAsync() => CleanupAsync();

	public async Task CleanupAsync()
	{
		await _cleanupLock.WaitAsync();
		try
		{
			if (_cleanupVerified && !HasBrowserResources) return;
			Logger.Info("[Cleanup] Promotion run stopping...");
			Logger.Info("[Cleanup] Stopping active instances...");
			var errors = new List<Exception>();
			foreach (BotEngine engine in _engines)
			{
				try { await engine.FullStopAsync(); }
				catch (Exception ex) { errors.Add(ex); Logger.Error("[Cleanup] Instance cleanup error: " + ex.Message, engine.InstanceId); }
			}
			if (HasBrowserResources || AnyBrowserAlive)
			{
				Logger.Warning("[Cleanup] Browser resources remain; retrying cleanup.");
				foreach (BotEngine engine in _engines.Where(e => e.HasBrowserResources || e.IsBrowserAlive))
				{
					try { await engine.FullStopAsync(); }
					catch (Exception ex) { errors.Add(ex); Logger.Error("[Cleanup] Retry failed: " + ex.Message, engine.InstanceId); }
				}
			}
			if (HasBrowserResources || AnyBrowserAlive)
			{
				string reason = "One or more browser resources remain alive after cleanup.";
				Logger.Error("[Cleanup] Browser cleanup failed: " + reason);
				throw new InvalidOperationException(reason, errors.FirstOrDefault());
			}
			if (errors.Count > 0) Logger.Warning("[Cleanup] A disposal attempt reported an error, but all browser resources were verified closed.");
			_cleanupVerified = true;
			Logger.Info("[Cleanup] Browser cleanup completed");
			Logger.Info("[Cleanup] Browser verified closed");
		}
		finally { _cleanupLock.Release(); }
	}

	public BotEngine? GetInstance(int id)
	{
		return _engines.FirstOrDefault((BotEngine e) => e.InstanceId == id);
	}

	public async Task PickAsync(string label, Action<int, int> save)
	{
		BotEngine first = _engines.FirstOrDefault((BotEngine e) => e.IsBrowserAlive);
		if (first?.Controller == null)
		{
			Logger.Error("No browser open for picking.");
			return;
		}
		(int, int)? pos = await first.Controller.PickPositionAsync(label);
		if (pos.HasValue)
		{
			save(pos.Value.Item1, pos.Value.Item2);
			Logger.Success($"{label} → ({pos.Value.Item1}, {pos.Value.Item2}) saved.");
		}
	}
}
