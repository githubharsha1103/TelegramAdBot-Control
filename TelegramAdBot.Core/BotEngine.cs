using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Core;

public class BotEngine
{
	private readonly BotConfig _config;

	private readonly int _instanceId;

	private TelegramController? _controller;

	private CancellationTokenSource? _cts;

	private Task? _loopTask;

	private volatile bool _running;

	private volatile bool _paused;

	private volatile bool _finished;
	private IReadOnlyList<int> _selectedAccounts = new[] { 1 };
	private int _selectedAccountIndex;
	private int _currentAccount = 1;
	private volatile bool _accountSwitching;
	private volatile bool _initializing;
	private bool _accountInitializedAtBoundary;
	private bool _initializedForNextLoop;

	private int _totalCycles;

	private int _completedCycles;

	private int _cyclesOnCurrentBot;

	private int _currentBotIndex;

	private int _promoIndex;

	private string _currentBotUsername = "";

	public int InstanceId => _instanceId;

	public bool IsRunning => _running;

	public bool IsPaused => _paused;

	public bool IsFinished => _finished;

	public bool IsBrowserAlive => _controller?.IsBrowserOpen() ?? false;
	public bool HasBrowserResources => _controller?.HasResources ?? false;

	public TelegramController? Controller => _controller;

	public int CurrentBotIndex => _currentBotIndex;

	public int CyclesOnCurrentBot => _cyclesOnCurrentBot;

	public int CompletedCycles => _completedCycles;
	public int CurrentAccount => _currentAccount;
	public int SelectedAccountIndex => _selectedAccountIndex;
	public int SelectedAccountCount => _selectedAccounts.Count;
	public bool IsAccountSwitching => _accountSwitching;
	public bool IsInitializing => _initializing;
	public string AccountState => _accountSwitching ? $"🔄 Switching to Account {_selectedAccounts.ElementAtOrDefault(_selectedAccountIndex + 1)}..." : _initializing ? $"⚙️ Initializing Account {_currentAccount}..." : _running ? $"🚀 Account {_currentAccount} promotion running..." : IsFinished ? "All selected accounts complete" : "";

	public string CurrentBotUsername => _currentBotUsername;
	public void ConfigureAccounts(IReadOnlyList<int> accounts) => _selectedAccounts = accounts;
	public void SetSelectedAccountIndex(int index) { _selectedAccountIndex = index; if (index >= 0 && index < _selectedAccounts.Count) _currentAccount = _selectedAccounts[index]; }
	public void MarkAccountInitializedAtBoundary() { _accountInitializedAtBoundary = true; _initializedForNextLoop = false; }

	public async Task<bool> SwitchAccountAsync(int accountNumber)
	{
		if (_controller == null) return false;
		return await _controller.SwitchAccountAsync(accountNumber);
	}

	public void ResetForNextAccount()
	{
		_cyclesOnCurrentBot = 0;
		_currentBotIndex = 0;
		_currentBotUsername = _config.BotList.Count > 0 ? _config.BotList[0] : "";
		_promoIndex = _instanceId - 1;
		_initializedForNextLoop = false;
		_accountInitializedAtBoundary = false;
		_finished = false;
		_running = false;
	}

	public event Action<int>? OnFinished;
	public event Func<int, Task<bool>>? OnAccountWorkFinished;

	public BotEngine(BotConfig config, int instanceId)
	{
		_config = config;
		_instanceId = instanceId;
		_promoIndex = instanceId - 1;
	}

	public async Task<bool> PrepareBrowserAsync()
	{
		if (IsBrowserAlive)
		{
			return true;
		}
		if (_config.BotList.Count == 0)
		{
			Logger.Error("Bot list is empty.", _instanceId);
			return false;
		}
		_currentBotIndex = 0;
		_currentBotUsername = _config.BotList[0];
		_controller = new TelegramController(_config, _instanceId);
		if (!(await _controller.InitializeAsync()))
		{
			Logger.Error("Browser init failed.", _instanceId);
			return false;
		}
		if (!(await _controller.WaitForLoginAsync()))
		{
			Logger.Error("Login failed.", _instanceId);
			return false;
		}
		Logger.Success($"Browser {_instanceId} ready (not navigated yet).", _instanceId);
		return true;
	}

	public async Task<bool> NavigateToFirstBotAsync()
	{
		if (_controller == null) return false;
		bool success = await _controller.TryNavigateToBotChatAsync(_currentBotUsername);
		if (success) Logger.Success($"Ready on @{_currentBotUsername} (1/{_config.BotList.Count})", _instanceId);
		return success;
	}

	public async Task<bool> PrepareAsync()
	{
		if (!(await PrepareBrowserAsync()))
		{
			return false;
		}
		return await InitializeCurrentAccountAsync();
	}

	public async Task<bool> PrepareFirstAccountAsync(int accountNumber)
	{
		_currentAccount = 1;
		if (accountNumber != 1)
		{
			_accountSwitching = true;
			try
			{
				if (_controller == null || !await _controller.SwitchAccountAsync(accountNumber)) return false;
			}
			finally { _accountSwitching = false; }
		}
		_currentAccount = accountNumber;
		ResetForNextAccount();
		return await InitializeCurrentAccountAsync();
	}

	public async Task<bool> InitializeCurrentAccountAsync()
	{
		_initializing = true;
		try
		{
			if (_controller == null || !await _controller.WaitForTelegramReadyAsync())
				throw new InvalidOperationException("Telegram page was not ready.");
			Logger.Info($"[PromotionInit] Starting fresh initialization for Account {_currentAccount}.", _instanceId);
			bool navigationCompleted = false;
			for (int attempt = 1; attempt <= 3 && !navigationCompleted; attempt++)
			{
				if (attempt > 1) Logger.Warning($"[PromotionInit] Retrying bot search/navigation ({attempt}/3) for @{_currentBotUsername}.", _instanceId);
				navigationCompleted = await NavigateToFirstBotAsync();
				if (!navigationCompleted && attempt < 3) await Task.Delay(1000);
			}
			if (!navigationCompleted) throw new InvalidOperationException($"Bot search/open failed for @{_currentBotUsername} after 3 attempts.");
			Logger.Info("[PromotionInit] Setting up sticker tab.", _instanceId);
			if (!await PrepareBotForCyclingAsync(CancellationToken.None)) throw new InvalidOperationException("Sticker and chat setup failed.");
			Logger.Success($"[PromotionInit] Account {_currentAccount} initialization completed.", _instanceId);
			_initializedForNextLoop = true;
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error($"[PromotionInit] Account {_currentAccount} initialization failed: {ex.Message}", _instanceId);
			_finished = true;
			return false;
		}
		finally { _initializing = false; }
	}

	public Task<bool> StartLoopAsync(int initialDelayMs = 0)
	{
		if (!IsBrowserAlive)
		{
			Logger.Error("Browser not open.", _instanceId);
			return Task.FromResult(result: false);
		}
		if (_running && _paused)
		{
			_paused = false;
			Logger.Success("RESUMED.", _instanceId);
			return Task.FromResult(result: true);
		}
		if (_running)
		{
			Logger.Warning("Already running.", _instanceId);
			return Task.FromResult(result: false);
		}
		if (!_config.AllSet)
		{
			Logger.Warning("Positions not set. Pick all 4 first.", _instanceId);
			return Task.FromResult(result: false);
		}
		_cts = new CancellationTokenSource();
		_running = true;
		_paused = false;
		_finished = false;
		_loopTask = Task.Run(() => RunLoopAsync(_cts.Token, initialDelayMs));
		Logger.Success($"STARTED (stagger: {initialDelayMs}ms)", _instanceId);
		return Task.FromResult(result: true);
	}

	public void Pause()
	{
		if (IsBrowserAlive) { _paused = true; Logger.Warning("PAUSED.", _instanceId); }
	}

	public void ResumePause() => _paused = false;

	public async Task FullStopAsync()
	{
		_running = false;
		_paused = false;
		_cts?.Cancel();
		Exception? cleanupError = null;
		try { await CloseControllerAsync(); }
		catch (Exception ex) { cleanupError = ex; }
		if (_loopTask != null)
		{
			try
			{
				await _loopTask.WaitAsync(TimeSpan.FromSeconds(10.0));
			}
			catch (TimeoutException)
			{
				Logger.Error("[Cleanup] Engine loop did not stop within 10 seconds after browser closure.", _instanceId);
			}
			catch (Exception ex)
			{
				Logger.Warning("Engine loop ended with error during cleanup: " + ex.Message, _instanceId);
			}
		}
		_finished = true;
		if (cleanupError != null) throw new InvalidOperationException("Engine browser cleanup failed.", cleanupError);
		Logger.Warning("STOPPED.", _instanceId);
	}

	private async Task<bool> PrepareBotForCyclingAsync(CancellationToken ct)
	{
		if (_controller == null) return false;
		Logger.Step($"Preparing chat: waiting {_config.WaitAfterNavigate}ms...", _instanceId);
		try
		{
			await Task.Delay(_config.WaitAfterNavigate, ct);
		}
		catch
		{
			return false;
		}
		Logger.Step("auto-click EMOJI button", _instanceId);
		if (!await _controller.ClickAsync(_config.EmojiButtonX, _config.EmojiButtonY)) return false;
		try
		{
			await Task.Delay(_config.WaitAfterEmojiButton, ct);
		}
		catch
		{
			return false;
		}
		Logger.Step("auto-click STICKER tab", _instanceId);
		if (!await _controller.ClickAsync(_config.StickerTabX, _config.StickerTabY)) return false;
		try
		{
			await Task.Delay(_config.WaitAfterStickerTab, ct);
		}
		catch
		{
			return false;
		}
		Logger.Success("Chat ready for cycling.", _instanceId);
		return true;
	}

	private async Task RunLoopAsync(CancellationToken ct, int initialDelayMs)
	{
		try
		{
		if (initialDelayMs > 0)
		{
			Logger.Info($"Stagger wait {initialDelayMs}ms...", _instanceId);
			try
			{
				await Task.Delay(initialDelayMs, ct);
			}
			catch
			{
				return;
			}
		}
		if (_initializedForNextLoop) _initializedForNextLoop = false;
		else if (!await InitializeCurrentAccountAsync()) return;
		if (ct.IsCancellationRequested)
		{
			return;
		}
		Logger.Success("Loop active.", _instanceId);
		bool firstCycle = true;
		while (!ct.IsCancellationRequested && _running)
		{
			while (_paused && !ct.IsCancellationRequested)
			{
				await Task.Delay(300, ct);
			}
			if (ct.IsCancellationRequested)
			{
				break;
			}
			try
			{
				if (_cyclesOnCurrentBot < _config.CyclesPerBot)
				{
					goto IL_03aa;
				}
				if (await SwitchToNextBotAsync(ct))
				{
					if (!_accountInitializedAtBoundary && !await PrepareBotForCyclingAsync(ct)) { _finished = true; _running = false; break; }
					_accountInitializedAtBoundary = false;
					if (!ct.IsCancellationRequested)
					{
						firstCycle = true;
						goto IL_03aa;
					}
				}
				goto end_IL_02af;
				IL_03aa:
				_totalCycles++;
				_cyclesOnCurrentBot++;
				Logger.Cycle(_totalCycles, _instanceId, _cyclesOnCurrentBot, _config.CyclesPerBot, _currentBotUsername, _currentBotIndex + 1, _config.BotList.Count);
				if (firstCycle)
				{
					await _controller.ClickAsync(_config.TextboxX, _config.TextboxY);
					await Task.Delay(200, ct);
					firstCycle = false;
				}
				Logger.Step("/search", _instanceId);
				await _controller.TypeAsync("/search");
				await _controller.PressEnterAsync();
				if (!(await Wait(_config.WaitAfterSearch, ct)))
				{
					break;
				}
				Logger.Step("emoji button click", _instanceId);
				await _controller.ClickAsync(_config.EmojiButtonX, _config.EmojiButtonY);
				if (!(await Wait(_config.WaitAfterEmojiClick, ct)))
				{
					break;
				}
				Logger.Step("sticker click", _instanceId);
				await _controller.ClickAsync(_config.StickerX, _config.StickerY);
				if (!(await Wait(_config.WaitAfterSticker, ct)))
				{
					break;
				}
				await _controller.ClickAsync(_config.TextboxX, _config.TextboxY);
				await Task.Delay(200, ct);
				string promo = _config.PromoMessages[_promoIndex % _config.PromoMessages.Length];
				_promoIndex++;
				Logger.Step($"promo [{_promoIndex}]: {promo}", _instanceId);
				await _controller.TypeAsync(promo);
				await _controller.PressEnterAsync();
				if (await Wait(_config.WaitAfterPromo, ct))
				{
					Logger.Step("/stop", _instanceId);
					await _controller.TypeAsync("/stop");
					await _controller.PressEnterAsync();
					if (await Wait(_config.WaitAfterStop, ct))
					{
						_completedCycles++;
						Logger.Success($"Cycle {_cyclesOnCurrentBot}/{_config.CyclesPerBot} done on @{_currentBotUsername}", _instanceId);
						continue;
					}
				}
				end_IL_02af:;
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				Logger.Error("Loop error: " + ex2.Message, _instanceId);
				firstCycle = true;
				try
				{
					await Task.Delay(3000, ct);
				}
				catch
				{
					goto end_IL_0d09;
				}
				continue;
				end_IL_0d09:;
			}
			break;
		}
		_running = false;
		Logger.Warning("Loop ended.", _instanceId);
		}
		catch (Exception ex)
		{
			Logger.Error("Fatal promotion loop error: " + ex.Message, _instanceId);
			_finished = true;
		}
		finally
		{
			_running = false;
			_finished = true;
			await CloseControllerAsync();
			OnFinished?.Invoke(_instanceId);
		}
	}

	private async Task<bool> SwitchToNextBotAsync(CancellationToken ct)
	{
			int nextIndex = _currentBotIndex + 1;
			if (nextIndex >= _config.BotList.Count)
			{
				if (OnAccountWorkFinished != null)
				{
					_running = false;
					_finished = true;
					bool continueWithNextAccount = false;
					foreach (Func<int, Task<bool>> handler in OnAccountWorkFinished.GetInvocationList()) continueWithNextAccount |= await handler(_instanceId);
					if (continueWithNextAccount) { _running = true; return !ct.IsCancellationRequested; }
					return false;
				}
				Logger.Warning($"Finished all {_config.BotList.Count} bots!", _instanceId);
			_finished = true;
			_running = false;
			return false;
		}
		string currentBotUsername = _currentBotUsername;
		_currentBotIndex = nextIndex;
		_currentBotUsername = _config.BotList[nextIndex];
		_cyclesOnCurrentBot = 0;
		Logger.BotSwitch(currentBotUsername, _currentBotUsername, nextIndex + 1, _config.BotList.Count, _instanceId);
		try
		{
			await Task.Delay(1500, ct);
		}
		catch
		{
			return false;
		}
		if (!await _controller.TryNavigateToBotChatAsync(_currentBotUsername)) return false;
		Logger.Info($"Waiting {_config.BotSwitchDelayMs}ms for chat to load...", _instanceId);
		try
		{
			await Task.Delay(_config.BotSwitchDelayMs, ct);
		}
		catch
		{
			return false;
		}
		Logger.Success($"Now on @{_currentBotUsername} ({nextIndex + 1}/{_config.BotList.Count})", _instanceId);
		return true;
	}

	private async Task CloseControllerAsync()
	{
		TelegramController? controller = _controller;
		if (controller == null) return;
		await controller.DisposeAsync();
		if (controller.IsDisposed) System.Threading.Interlocked.CompareExchange(ref _controller, null, controller);
	}

	private async Task<bool> Wait(int ms, CancellationToken ct)
	{
		for (int elapsed = 0; elapsed < ms; elapsed += 100)
		{
			if (ct.IsCancellationRequested)
			{
				return false;
			}
			while (_paused && !ct.IsCancellationRequested)
			{
				await Task.Delay(200, ct);
			}
			try
			{
				await Task.Delay(100, ct);
			}
			catch
			{
				return false;
			}
		}
		return true;
	}
}
