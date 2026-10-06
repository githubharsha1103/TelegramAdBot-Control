using System;
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

	public TelegramController? Controller => _controller;

	public int CurrentBotIndex => _currentBotIndex;

	public int CyclesOnCurrentBot => _cyclesOnCurrentBot;

	public int CompletedCycles => _completedCycles;

	public string CurrentBotUsername => _currentBotUsername;

	public event Action<int>? OnFinished;

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

	public async Task NavigateToFirstBotAsync()
	{
		if (_controller != null)
		{
			await _controller.NavigateToBotChatAsync(_currentBotUsername);
			Logger.Success($"Ready on @{_currentBotUsername} (1/{_config.BotList.Count})", _instanceId);
		}
	}

	public async Task<bool> PrepareAsync()
	{
		if (!(await PrepareBrowserAsync()))
		{
			return false;
		}
		await NavigateToFirstBotAsync();
		return true;
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
		if (_running)
		{
			_paused = true;
			Logger.Warning("PAUSED.", _instanceId);
		}
	}

	public async Task FullStopAsync()
	{
		_running = false;
		_paused = false;
		_cts?.Cancel();
		if (_loopTask != null)
		{
			try
			{
				await _loopTask.WaitAsync(TimeSpan.FromSeconds(5.0));
			}
			catch
			{
			}
		}
		await CloseControllerAsync();
		Logger.Warning("STOPPED.", _instanceId);
	}

	private async Task PrepareBotForCyclingAsync(CancellationToken ct)
	{
		Logger.Step($"Preparing chat: waiting {_config.WaitAfterNavigate}ms...", _instanceId);
		try
		{
			await Task.Delay(_config.WaitAfterNavigate, ct);
		}
		catch
		{
			return;
		}
		Logger.Step("auto-click EMOJI button", _instanceId);
		await _controller.ClickAsync(_config.EmojiButtonX, _config.EmojiButtonY);
		try
		{
			await Task.Delay(_config.WaitAfterEmojiButton, ct);
		}
		catch
		{
			return;
		}
		Logger.Step("auto-click STICKER tab", _instanceId);
		await _controller.ClickAsync(_config.StickerTabX, _config.StickerTabY);
		try
		{
			await Task.Delay(_config.WaitAfterStickerTab, ct);
		}
		catch
		{
			return;
		}
		Logger.Success("Chat ready for cycling.", _instanceId);
	}

	private async Task RunLoopAsync(CancellationToken ct, int initialDelayMs)
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
		await PrepareBotForCyclingAsync(ct);
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
					await PrepareBotForCyclingAsync(ct);
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

	private async Task<bool> SwitchToNextBotAsync(CancellationToken ct)
	{
		int nextIndex = _currentBotIndex + 1;
		if (nextIndex >= _config.BotList.Count)
		{
			Logger.Warning($"Finished all {_config.BotList.Count} bots! Closing browser.", _instanceId);
			_finished = true;
			_running = false;
			await CloseControllerAsync();
			this.OnFinished?.Invoke(_instanceId);
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
		await _controller.NavigateToBotChatAsync(_currentBotUsername);
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
		if (_controller != null)
		{
			await _controller.DisposeAsync();
			_controller = null;
		}
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
