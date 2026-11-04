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

	private volatile bool _recoveryBlocked;

	private volatile bool _finished;
	private volatile bool _failed;
	private IReadOnlyList<int> _selectedAccounts = new[] { 1 };
	private int _selectedAccountIndex;
	private int _currentAccount = 1;
	private volatile bool _accountSwitching;
	private volatile bool _initializing;
	private bool _accountInitializedAtBoundary;
	private bool _initializedForNextLoop;
	private volatile bool _retainBrowserOnInitFailure;

	private int _totalCycles;

	private int _completedCycles;

	private int _cyclesOnCurrentBot;

	private int _currentBotIndex;

	private int _promoIndex;

	private bool _stopDeliveredForCurrentCycle;
	private readonly BotMenuButtonPositionStore _menuButtonPositions = new();
	private readonly Dictionary<string, ReplyKeyboardMenuState> _menuStateByBot = new(StringComparer.OrdinalIgnoreCase);
	private string _menuConfigurationError = "";

	private string _currentBotUsername = "";

	public int InstanceId => _instanceId;

	public bool IsRunning => _running;

	public bool IsPaused => _paused;

	public bool IsFinished => _finished;
	public bool HasFailed => _failed;

	public bool IsBrowserAlive => _controller?.IsBrowserOpen() ?? false;
	public bool HasBrowserResources => _controller?.HasResources ?? false;

	public TelegramController? Controller => _controller;

	public int CurrentBotIndex => _currentBotIndex;

	public int CyclesOnCurrentBot => _cyclesOnCurrentBot;

	public int CompletedCycles => _completedCycles;
	public string MenuConfigurationError => _menuConfigurationError;
	public int CurrentAccount => _currentAccount;
	public int SelectedAccountIndex => _selectedAccountIndex;
	public int SelectedAccountCount => _selectedAccounts.Count;
	public bool IsAccountSwitching => _accountSwitching;
	public bool IsInitializing => _initializing;
	public string AccountState => _accountSwitching ? $"🔄 Switching to Account {_selectedAccounts.ElementAtOrDefault(_selectedAccountIndex + 1)}..." : _initializing ? $"⚙️ Initializing Account {_currentAccount}..." : _running ? $"🚀 Account {_currentAccount} promotion running..." : IsFinished ? "All selected accounts complete" : "";

	public string CurrentBotUsername => _currentBotUsername;

	public async Task<(BotMenuButtonPosition? Position, string Error)> PickMenuButtonPositionAsync(string botUsername, CancellationToken ct = default)
	{
		if (_controller == null || !_controller.IsBrowserOpen()) return (null, "No Playwright browser is available for this instance.");
		if (_running && !_paused) return (null, "Pause this promotion instance before selecting a menu button position.");
		string returnBot = _currentBotUsername;
		bool switchedChat = !string.Equals(returnBot, botUsername, StringComparison.OrdinalIgnoreCase);
		bool navigated = !switchedChat || await _controller.TryNavigateToBotChatAsync(botUsername);
		if (!navigated) return (null, $"Could not open @{botUsername} in this VNC browser.");
		if (!await _controller.VerifyExpectedBotConversationAsync(botUsername, requireMessageInput: true))
		{
			if (switchedChat && !await RestorePausedConversationAsync(returnBot)) _recoveryBlocked = true;
			return (null, $"The expected conversation for @{botUsername} was not verified before coordinate selection.");
		}
		var picked = await _controller.PickPositionWithViewportAsync($"Menu button for @{botUsername}", ct);
		if (switchedChat)
		{
			if (!await RestorePausedConversationAsync(returnBot))
			{
				_recoveryBlocked = true;
				return (null, $"Position was captured, but the promotion conversation @{returnBot} could not be restored; the instance remains blocked and paused.");
			}
		}
		return picked.HasValue
			? (new BotMenuButtonPosition { BotIdentifier = BotMenuButtonPositionStore.Normalize(botUsername), X = picked.Value.x, Y = picked.Value.y, ViewportWidth = picked.Value.width, ViewportHeight = picked.Value.height, Configured = true }, "")
			: (null, "No browser position was captured. Move the VNC cursor over Telegram's reply keyboard menu button during the capture countdown.");
	}

	private async Task<bool> RestorePausedConversationAsync(string botUsername) =>
		_controller != null && await _controller.TryNavigateToBotChatAsync(botUsername) && await _controller.VerifyExpectedBotConversationAsync(botUsername, requireMessageInput: true);

	public void ConfigureAccounts(IReadOnlyList<int> accounts) => _selectedAccounts = accounts;
	public void SetSelectedAccountIndex(int index) { _selectedAccountIndex = index; if (index >= 0 && index < _selectedAccounts.Count) _currentAccount = _selectedAccounts[index]; }
	public void MarkAccountInitializedAtBoundary() { _accountInitializedAtBoundary = true; _initializedForNextLoop = false; }

	public async Task<bool> SwitchAccountAsync(int accountNumber)
	{
		if (_controller == null) return false;
		_accountSwitching = true;
		try { return await _controller.SwitchAccountAsync(accountNumber); }
		finally { _accountSwitching = false; }
	}

	public void ResetForNextAccount()
	{
		_cyclesOnCurrentBot = 0;
		_currentBotIndex = 0;
		_currentBotUsername = _config.BotList.Count > 0 ? _config.BotList[0] : "";
		_promoIndex = _instanceId - 1;
		_initializedForNextLoop = false;
		_accountInitializedAtBoundary = false;
		_retainBrowserOnInitFailure = false;
		_finished = false;
		_running = false;
	}

	public void RetainBrowserAfterAccountFailure() => _retainBrowserOnInitFailure = true;

	public event Action<int>? OnFinished;
	public event Func<int, Task<bool>>? OnAccountWorkFinished;

	public BotEngine(BotConfig config, int instanceId)
	{
		if (instanceId < 1) throw new ArgumentOutOfRangeException(nameof(instanceId), "Instance IDs must be positive.");
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
		Logger.Success("[Browser] Telegram UI ready.", _instanceId);
		Logger.Success($"Browser {_instanceId} ready (not navigated yet).", _instanceId);
		return true;
	}

	public async Task<bool> NavigateToFirstBotAsync(bool startup = false)
	{
		if (_controller == null) return false;
		bool success = await _controller.TryNavigateToBotChatAsync(_currentBotUsername, waitAfterEnter: !startup);
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
				Logger.Info($"[Bot] Searching/opening @{_currentBotUsername} (attempt {attempt}/3).", _instanceId);
				if (attempt > 1) Logger.Warning($"[PromotionInit] Retrying bot search/navigation ({attempt}/3) for @{_currentBotUsername}.", _instanceId);
				navigationCompleted = await NavigateToFirstBotAsync(startup: true);
				if (!navigationCompleted && attempt < 3) await Task.Delay(1000);
			}
			if (!navigationCompleted) throw new InvalidOperationException($"Bot search/open failed for @{_currentBotUsername} after 3 attempts.");
			BotMenuButtonPosition? startupMenuPosition = _menuButtonPositions.Get(_currentBotUsername);
			if (startupMenuPosition is { Configured: true })
				Logger.Info($"[Menu] Loaded configured menu button position for @{_currentBotUsername} ({startupMenuPosition.X},{startupMenuPosition.Y}) from {startupMenuPosition.ViewportWidth}x{startupMenuPosition.ViewportHeight} viewport.", _instanceId);
			else
				Logger.Warning($"[Menu] No menu button position is saved for @{_currentBotUsername}; if a menu interaction is needed, promotion will pause before clicking.", _instanceId);
			Logger.Info("[PromotionInit] Setting up sticker tab.", _instanceId);
			if (!await _controller.VerifyStartupChatAndFocusMessageInputAsync(_currentBotUsername)) throw new InvalidOperationException("Startup chat/input verification failed.");
			if (!await PrepareBotForCyclingAsync(CancellationToken.None)) throw new InvalidOperationException("Sticker and chat setup failed.");
			Logger.Success($"[PromotionInit] Account {_currentAccount} initialization completed.", _instanceId);
			_initializedForNextLoop = true;
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error($"[PromotionInit] Account {_currentAccount} initialization failed: {ex.Message}", _instanceId);
			_finished = true;
			_retainBrowserOnInitFailure = true;
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
			if (_recoveryBlocked)
			{
				Logger.Warning("Resume refused: conversation recovery is blocked pending verification.", _instanceId);
				return Task.FromResult(result: false);
			}
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

	public void ResumePause()
	{
		if (_recoveryBlocked)
		{
			Logger.Warning("Resume refused: conversation recovery is blocked pending verification.", _instanceId);
			return;
		}
		_paused = false;
	}

	public async Task FullStopAsync()
	{
		_running = false;
		_paused = false;
		_recoveryBlocked = false;
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

	private async Task<bool> PrepareBotForCyclingAsync(CancellationToken ct, bool verifyStickerPickerReady = false, bool ensureMenuClosed = false)
	{
		if (_controller == null) return false;
		Logger.Step($"Preparing chat: waiting {_config.WaitAfterNavigate}ms...", _instanceId);
		Logger.Info($"[StartupDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Configured chat preparation wait {_config.WaitAfterNavigate}ms started.", _instanceId);
		try
		{
			await Task.Delay(_config.WaitAfterNavigate, ct);
			Logger.Info($"[StartupDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Configured chat preparation wait completed.", _instanceId);
		}
		catch
		{
			return false;
		}
		if (ensureMenuClosed && !await TrySetMenuStateAsync(_currentBotUsername, ReplyKeyboardMenuState.Closed)) return false;
		Logger.Step("auto-click EMOJI button", _instanceId);
		if (!await _controller.ClickAsync(_config.EmojiButtonX, _config.EmojiButtonY, "Sticker icon / emoji button", captureScreenshots: true)) return false;
		try
		{
			await Task.Delay(_config.WaitAfterEmojiButton, ct);
			await LogCycleWaitAsync("Sticker icon", _config.WaitAfterEmojiButton);
		}
		catch
		{
			return false;
		}
		Logger.Step("auto-click STICKER tab", _instanceId);
		if (!await _controller.ClickAsync(_config.StickerTabX, _config.StickerTabY, "Sticker tab", captureScreenshots: true)) return false;
		try
		{
			await Task.Delay(_config.WaitAfterStickerTab, ct);
			await LogCycleWaitAsync("Sticker tab", _config.WaitAfterStickerTab);
		}
		catch
		{
			return false;
		}
		Logger.Success("Chat ready for cycling.", _instanceId);
		Logger.Info($"[StartupDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Sticker configuration completed.", _instanceId);
		if (verifyStickerPickerReady && !await _controller.VerifyStickerPickerReadyAsync()) return false;
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
					if (!_accountInitializedAtBoundary && !await PrepareBotForCyclingAsync(ct, ensureMenuClosed: true)) { _finished = true; _running = false; break; }
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
				_stopDeliveredForCurrentCycle = false;
				Logger.Cycle(_totalCycles, _instanceId, _cyclesOnCurrentBot, _config.CyclesPerBot, _currentBotUsername, _currentBotIndex + 1, _config.BotList.Count);
				Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Cycle {_cyclesOnCurrentBot} started.", _instanceId);
				if (firstCycle)
				{
					await _controller.ClickAsync(_config.TextboxX, _config.TextboxY, "Initial cycle chat textbox");
					await Task.Delay(200, ct);
					firstCycle = false;
				}
				Logger.Step("/search", _instanceId);
				await _controller.TypeAsync("/search");
				await _controller.PressEnterAsync();
				Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Search command sent; configured wait {_config.WaitAfterSearch}ms started.", _instanceId);
				if (!(await Wait(_config.WaitAfterSearch, ct)))
				{
					break;
				}
				await LogCycleWaitAsync("Search command", _config.WaitAfterSearch);
				await Task.Delay(1000, ct);
				if (!await EnsureMenuStateWithPauseAsync(_currentBotUsername, ReplyKeyboardMenuState.Closed, ct)) break;
				Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Telegram menu button confirmed closed after search.", _instanceId);
				await Task.Delay(1000, ct);
				Logger.Step("emoji button click", _instanceId);
				await _controller.ClickAsync(_config.EmojiButtonX, _config.EmojiButtonY, "Cycle sticker icon / emoji button", captureScreenshots: _completedCycles == 0);
				if (!(await Wait(_config.WaitAfterEmojiClick, ct)))
				{
					break;
				}
				await LogCycleWaitAsync("Sticker icon", _config.WaitAfterEmojiClick);
				Logger.Step("sticker click", _instanceId);
				await _controller.ClickAsync(_config.StickerX, _config.StickerY, "Promotional sticker selection", captureScreenshots: _completedCycles == 0);
				Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Sticker selection click completed; sticker sent if Telegram accepted it.", _instanceId);
				if (!(await Wait(_config.WaitAfterSticker, ct)))
				{
					break;
				}
				await LogCycleWaitAsync("Sticker selection", _config.WaitAfterSticker);
				await _controller.ClickAsync(_config.TextboxX, _config.TextboxY, "Promotion message textbox");
				await Task.Delay(200, ct);
				string promo = _config.PromoMessages[_promoIndex % _config.PromoMessages.Length];
				_promoIndex++;
				Logger.Step($"promo [{_promoIndex}]: {promo}", _instanceId);
				await _controller.TypeAsync(promo);
				await _controller.PressEnterAsync();
				Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Promotional message sent; configured wait {_config.WaitAfterPromo}ms.", _instanceId);
				if (await Wait(_config.WaitAfterPromo, ct))
				{
					await LogCycleWaitAsync("Promotional message", _config.WaitAfterPromo);
					Logger.Step("/stop", _instanceId);
					bool stopTyped = await _controller.TypeAsync("/stop");
					bool stopSubmitted = stopTyped && await _controller.PressEnterAsync();
					if (!stopSubmitted)
					{
						string reason = $"Cycle {_cyclesOnCurrentBot}: /stop delivery is uncertain; automation remains paused to avoid resending it.";
						_paused = true;
						_recoveryBlocked = true;
						Logger.Error($"[Recovery] Promotion paused. {reason}", _instanceId);
						await _controller.CaptureRecoveryFailureAsync(reason);
						continue;
					}
					_stopDeliveredForCurrentCycle = true;
					Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | /stop command sent; configured wait {_config.WaitAfterStop}ms.", _instanceId);
					if (await Wait(_config.WaitAfterStop, ct))
					{
						await LogCycleWaitAsync("/stop command", _config.WaitAfterStop);
						await Task.Delay(1000, ct);
						Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Existing post-/stop wait completed; checking conversation URL.", _instanceId);
						_completedCycles++;
						Logger.Success($"Cycle {_cyclesOnCurrentBot}/{_config.CyclesPerBot} done on @{_currentBotUsername}", _instanceId);
						if (_controller != null && !await _controller.VerifyExpectedBotConversationAsync(_currentBotUsername, requireMessageInput: false))
						{
							await RecoverConversationAfterStopAsync(_cyclesOnCurrentBot, ct);
						}
						Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Next cycle will start if configured cycles remain.", _instanceId);
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
			_failed = true;
			Logger.Error("Fatal promotion loop error: " + ex.Message, _instanceId);
			_finished = true;
		}
		finally
		{
			_running = false;
			_finished = true;
			if (!_retainBrowserOnInitFailure) await CloseControllerAsync();
			else Logger.Warning("Browser retained for inspection after account preparation failed.", _instanceId);
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

	private async Task<bool> RecoverConversationAfterStopAsync(int completedCycle, CancellationToken ct)
	{
		if (_controller == null) return false;
		_paused = true;
		_recoveryBlocked = true;
		Logger.Warning($"[Recovery] Conversation navigation loss detected after cycle {completedCycle}; last completed action was /stop and its existing post-stop action. Promotion paused.", _instanceId);
		Logger.Info($"[Recovery] Recovery navigation started for @{_currentBotUsername}.", _instanceId);
		string failureStage = "bot navigation";
		string failure = "Conversation recovery could not be completed.";
		try
		{
			if (!await _controller.TryNavigateToBotChatAsync(_currentBotUsername))
			{
				failure = $"Could not navigate back to @{_currentBotUsername}.";
			}
			else if (!await _controller.VerifyExpectedBotConversationAsync(_currentBotUsername, requireMessageInput: true))
			{
				failureStage = "URL or message input verification";
				failure = $"Recovered navigation did not verify the expected conversation and message input for @{_currentBotUsername}.";
			}
			else if (!await PrepareBotForCyclingAsync(ct, verifyStickerPickerReady: true, ensureMenuClosed: true))
			{
				failureStage = "menu or sticker setup initialization";
				failure = $"Menu and sticker setup did not complete for cycle {completedCycle}.";
			}
			else if (!_stopDeliveredForCurrentCycle && !await SubmitStopOnceDuringRecoveryAsync(completedCycle))
			{
				failureStage = "/stop submission";
				failure = $"Recovery could not verify /stop submission for cycle {completedCycle}; it will not be retried.";
			}
			else
			{
				Logger.Success($"[Recovery] Expected conversation restored after cycle {completedCycle}: @{_currentBotUsername}.", _instanceId);
				_recoveryBlocked = false;
				_paused = false;
				Logger.Success($"[Recovery] Promotion resumed at cycle {completedCycle + 1}.", _instanceId);
				return true;
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			failure = "Conversation recovery was cancelled during shutdown.";
		}
		catch (Exception ex)
		{
			failure = ex.Message;
		}
		Logger.Error($"[Recovery] Recovery failed at stage '{failureStage}' after cycle {completedCycle}; promotion remains paused. {failure}", _instanceId);
		await _controller.CaptureRecoveryFailureAsync($"cycle {completedCycle}, stage {failureStage}: {failure}");
		return false;
	}

	private async Task<bool> EnsureMenuStateWithPauseAsync(string botUsername, ReplyKeyboardMenuState desiredState, CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			if (await TrySetMenuStateAsync(botUsername, desiredState)) return true;
			_paused = true;
			Logger.Error($"[Menu] @{botUsername} menu state is unknown or its position is not configured. Promotion paused before any menu click; configure the position or restore a detectable menu state, then resume.", _instanceId);
			while (_paused && !ct.IsCancellationRequested) await Task.Delay(300, ct);
		}
		return false;
	}

	private async Task<bool> TrySetMenuStateAsync(string botUsername, ReplyKeyboardMenuState desiredState)
	{
		if (_controller == null) return false;
		BotMenuButtonPosition? saved = _menuButtonPositions.Get(botUsername);
		if (saved == null || !saved.Configured)
		{
			_menuStateByBot[botUsername] = ReplyKeyboardMenuState.Unknown;
			_menuConfigurationError = $"Menu button coordinates are not configured for @{botUsername}. Use Configure Menu Button Position, save coordinates, then resume.";
			Logger.Error("[Menu] " + _menuConfigurationError + " No click was attempted.", _instanceId);
			return false;
		}
		if (saved.ViewportWidth <= 0 || saved.ViewportHeight <= 0 || saved.X < 0 || saved.Y < 0 || saved.X >= saved.ViewportWidth || saved.Y >= saved.ViewportHeight)
		{
			_menuStateByBot[botUsername] = ReplyKeyboardMenuState.Unknown;
			_menuConfigurationError = $"Saved menu button coordinates for @{botUsername} are invalid; reconfigure the position.";
			return false;
		}
		ReplyKeyboardMenuState current = await _controller.GetReplyKeyboardMenuStateAsync();
		_menuStateByBot[botUsername] = current;
		if (current == ReplyKeyboardMenuState.Unknown)
		{
			_menuConfigurationError = $"Telegram menu state is unknown for @{botUsername}; promotion is paused without clicking.";
			return false;
		}
		if (current == desiredState) { _menuConfigurationError = ""; return true; }
		(int width, int height)? viewport = await _controller.GetViewportSizeAsync();
		if (!viewport.HasValue) { _menuConfigurationError = $"Could not read the browser viewport for @{botUsername}; promotion is paused."; return false; }
		int x = (int)Math.Round(saved.X * (double)viewport.Value.width / saved.ViewportWidth);
		int y = (int)Math.Round(saved.Y * (double)viewport.Value.height / saved.ViewportHeight);
		if (x < 0 || y < 0 || x >= viewport.Value.width || y >= viewport.Value.height)
		{
			Logger.Error($"[Menu] Saved @{botUsername} coordinate scaled outside the current viewport: ({x},{y}) in {viewport.Value.width}x{viewport.Value.height}.", _instanceId);
			_menuStateByBot[botUsername] = ReplyKeyboardMenuState.Unknown;
			_menuConfigurationError = $"Saved menu button position for @{botUsername} is outside the browser viewport; reconfigure it.";
			return false;
		}
		if (!await _controller.SetReplyKeyboardMenuStateAsync(x, y, desiredState))
		{
			_menuStateByBot[botUsername] = ReplyKeyboardMenuState.Unknown;
			_menuConfigurationError = $"Telegram menu state could not be confirmed for @{botUsername}; promotion is paused.";
			return false;
		}
		_menuStateByBot[botUsername] = desiredState;
		_menuConfigurationError = "";
		return true;
	}

	private async Task<bool> SubmitStopOnceDuringRecoveryAsync(int cycleNumber)
	{
		if (_controller == null || _stopDeliveredForCurrentCycle) return _stopDeliveredForCurrentCycle;
		Logger.Info($"[Recovery] Cycle {cycleNumber}: /stop was not recorded as delivered; submitting it once after sticker setup.", _instanceId);
		bool typed = await _controller.TypeAsync("/stop");
		bool submitted = typed && await _controller.PressEnterAsync();
		if (!submitted)
		{
			Logger.Error($"[Recovery] Cycle {cycleNumber}: /stop delivery is uncertain; it will not be retried.", _instanceId);
			return false;
		}
		_stopDeliveredForCurrentCycle = true;
		Logger.Info($"[Recovery] Cycle {cycleNumber}: /stop submitted once during recovery.", _instanceId);
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

	private Task LogCycleWaitAsync(string action, int configuredMs)
	{
		Logger.Info($"[CycleDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | {action} configured wait completed ({configuredMs}ms).", _instanceId);
		return Task.CompletedTask;
	}
}
