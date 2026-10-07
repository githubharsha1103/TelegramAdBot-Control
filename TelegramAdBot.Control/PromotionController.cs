using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TelegramAdBot.Core;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Control;

// This class deliberately delegates all promotion work to the existing InstanceManager/BotEngine.
public sealed class PromotionController
{
    private readonly BotConfig _config;
    private readonly BotConfigManager _botConfig;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private InstanceManager? _manager;
    private PromotionRunConfiguration? _run;
    private DateTimeOffset? _startedAt;
    private bool _isStopping;
    private string _lastError = "";
    private int _currentAccount = 1;
    private bool _accountSwitching;
    private string _accountState = "Idle";
    private int _currentSelectedAccountIndex;

    public PromotionController(BotConfig config, BotConfigManager? botConfig = null) { _config = config; _botConfig = botConfig ?? new BotConfigManager(); }

    public IReadOnlyList<string> GetAvailableBots() => _botConfig.GetBots();
    public int GetConfiguredBotCount() => _botConfig.GetBots().Count;
    public bool TryAddBot(string username, out string normalized, out string error) => _botConfig.TryAdd(username, out normalized, out error);
    public bool RemoveBot(string username) => _botConfig.Remove(username);

    public async Task<ControlResult> ConfigureAsync(PromotionRunConfiguration run)
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_manager?.AnyRunning == true || _isStopping)
                return Fail("Promotion is active; stop it before changing the configuration.");
            if (_manager?.AnyBrowserAlive == true)
                await _manager.StopAllAsync();

            var available = _botConfig.GetBots();
            var requested = run.SelectedBots.Select(x => x.Trim().TrimStart('@')).Where(x => x.Length > 0).Distinct().ToList();
            if (requested.Count == 0) return Fail("Select at least one bot.");
            if (requested.Any(x => !available.Contains(x, StringComparer.OrdinalIgnoreCase))) return Fail("One or more selected bots are no longer available.");
            if (run.InstanceCount < 1 || run.InstanceCount > 20) return Fail("Instance count must be between 1 and 20.");
            if (run.CyclesPerBot < 1 || run.CyclesPerBot > 100000) return Fail("Cycles per bot must be between 1 and 100000.");
            var selectedAccounts = (run.SelectedAccounts ?? new List<int>()).Distinct().OrderBy(x => x).ToList();
            if (selectedAccounts.Count == 0 || selectedAccounts.Any(x => x < 1 || x > 3)) return Fail("Select at least one valid Telegram account.");

            _run = new PromotionRunConfiguration { SelectedBots = requested, InstanceCount = run.InstanceCount, CyclesPerBot = run.CyclesPerBot, SelectedAccounts = selectedAccounts, AccountCount = selectedAccounts.Count };
            _config.BotList = requested;
            _config.InstanceCount = 1;
            _config.CyclesPerBot = run.CyclesPerBot;
            _manager = null;
            _startedAt = null;
            _lastError = "";
            _currentAccount = 1;
            _currentSelectedAccountIndex = 0;
            _accountState = "Preparing first account";
            Logger.Info("[CONTROL] Configuration received.");
            return Ok("Configuration accepted.");
        }
        finally { _commandLock.Release(); }
    }

    public async Task<ControlResult> StartAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_isStopping) return Fail("Promotion is stopping.");
            if (_manager?.AnyRunning == true) return Fail("Promotion is already running.");
            if (_run == null) return Fail("Configure a promotion run first.");
            if (!_config.AllSet) return Fail("Engine setup is incomplete: click positions are not configured.");

            // Account switching is serialized through one existing engine and browser context.
            _config.InstanceCount = 1;
            _manager = new InstanceManager(_config);
            _manager.KeepBrowserOnFinish();
            _manager.ConfigureAccounts(_run.SelectedAccounts);
            _manager.SetAccountCompletionHandler(_ => AdvanceAccountAsync());
            _manager.OnAllFinished += () => Logger.Info("[CONTROL] All promotion instances completed.");
            Logger.Info("[CONTROL] START requested.");
            await _manager.OpenSequentialBrowserAsync();
            if (!_manager.AnyBrowserAlive) return Fail("No Chromium instances started. Check engine logs and DISPLAY.");
            _startedAt = DateTimeOffset.UtcNow;
            _currentAccount = 1;
            _currentSelectedAccountIndex = 0;
            int firstAccount = _run.SelectedAccounts[0];
            _accountState = firstAccount == 1 ? "Initializing Account 1..." : $"Switching to Account {firstAccount}...";
            var engine = _manager.Engines[0];
            engine.SetSelectedAccountIndex(0);
            if (!await engine.PrepareFirstAccountAsync(firstAccount))
            {
                _lastError = $"Account {firstAccount} preparation/initialization failed.";
                _accountState = "Failed";
                return Fail(_lastError);
            }
            _accountState = $"Account {firstAccount} promotion running...";
            if (!await _manager.StartSequentialAsync()) return Fail("Promotion engine failed to start.");
            return Ok("Promotion started.");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            Logger.Error("[CONTROL] START failed: " + ex.Message);
            return Fail("Start failed: " + ex.Message);
        }
        finally { _commandLock.Release(); }
    }

    private async Task<bool> AdvanceAccountAsync()
    {
        if (_run == null || _currentSelectedAccountIndex + 1 >= _run.SelectedAccounts.Count || _isStopping) return false;
        while (_manager?.IsPaused == true && !_isStopping) await Task.Delay(200);
        if (_isStopping) return false;
        _accountSwitching = true;
        int nextIndex = _currentSelectedAccountIndex + 1;
        int next = _run.SelectedAccounts[nextIndex];
        _accountState = $"Switching to Account {next}...";
        Logger.Info($"[CONTROL] Switching Account {_currentAccount} -> Account {next}.");
        try
        {
            if (_manager == null || !await _manager.SwitchAccountAsync(next))
            {
                _lastError = $"Account switch to {next} failed; promotion halted safely.";
                _accountState = "Failed";
                Logger.Error("[CONTROL] " + _lastError);
                return false;
            }
            _currentAccount = next;
            _currentSelectedAccountIndex = nextIndex;
            _accountState = $"Initializing Account {next}...";
            _manager.SetRunForNextAccount();
            _manager.Engines[0].SetSelectedAccountIndex(nextIndex);
            while (_manager.IsPaused && !_isStopping) await Task.Delay(200);
            if (_isStopping) return false;
            if (_manager == null || !await _manager.InitializeSequentialAsync()) throw new InvalidOperationException("Fresh account initialization failed.");
            _manager.Engines[0].MarkAccountInitializedAtBoundary();
            _accountState = $"Account {next} promotion running...";
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Account switch to {next} failed: {ex.Message}";
            _accountState = "Failed";
            Logger.Error("[CONTROL] " + _lastError);
            return false;
        }
        finally { _accountSwitching = false; }
    }

    public async Task<ControlResult> PauseAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_manager?.AnyBrowserAlive != true) return Fail("Promotion is not running.");
            if (_manager.Engines.All(x => x.IsPaused)) return Fail("Promotion is already paused.");
            Logger.Info("[CONTROL] PAUSE requested.");
            _manager.PauseAll();
            return Ok("Promotion paused.");
        }
        finally { _commandLock.Release(); }
    }

    public async Task<ControlResult> ResumeAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_manager == null || !_manager.AnyBrowserAlive) return Fail("Promotion is not available to resume.");
            if (_manager.Engines.All(x => !x.IsPaused)) return Fail("Promotion is not paused.");
            Logger.Info("[CONTROL] RESUME requested.");
            _manager.ResumeSequential();
            return Ok("Promotion resumed.");
        }
        finally { _commandLock.Release(); }
    }

    public async Task<ControlResult> StopAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_manager == null || (!_manager.AnyBrowserAlive && !_manager.AnyRunning)) return Fail("Promotion is already stopped.");
            _isStopping = true;
            Logger.Info("[CONTROL] STOP requested.");
            await _manager.StopAllAsync();
            return Ok("Promotion stopped.");
        }
        catch (Exception ex) { _lastError = ex.Message; return Fail("Stop failed: " + ex.Message); }
        finally { _isStopping = false; _commandLock.Release(); }
    }

    public PromotionStatus GetStatus()
    {
        var manager = _manager;
        var started = _startedAt;
        var status = new PromotionStatus
        {
            IsConfigured = _run != null,
            IsRunning = manager?.AnyRunning == true || _accountSwitching || _accountState.StartsWith("Initializing", StringComparison.OrdinalIgnoreCase),
            IsStopping = _isStopping,
            CurrentAccount = _currentAccount,
            AccountCount = _run?.AccountCount ?? 1,
            SelectedAccounts = _run?.SelectedAccounts.ToList() ?? new List<int>(),
            CurrentSelectedAccountIndex = _currentSelectedAccountIndex,
            AccountState = _accountState,
            IsPaused = manager?.IsPaused == true,
            StartTime = started,
            RuntimeSeconds = started.HasValue ? (long)(DateTimeOffset.UtcNow - started.Value).TotalSeconds : 0,
            SelectedBots = _run?.SelectedBots.ToList() ?? new List<string>(),
            InstanceCount = _run?.InstanceCount ?? 0,
            CyclesPerBot = _run?.CyclesPerBot ?? 0,
			// Each existing engine instance processes the configured bot list independently.
			TotalCycles = (_run?.SelectedBots.Count ?? 0) * (_run?.CyclesPerBot ?? 0) * (_run?.SelectedAccounts.Count ?? 0),
            CompletedCycles = manager?.Engines.Sum(x => x.CompletedCycles) ?? 0,
            LastError = _lastError
        };
        if (manager != null)
        {
            foreach (var engine in manager.Engines)
                status.Instances.Add(new PromotionInstanceStatus
                {
                    InstanceId = engine.InstanceId,
                    Bot = engine.CurrentBotUsername,
                    CurrentCycle = engine.CyclesOnCurrentBot,
                    TotalCyclesForBot = _run?.CyclesPerBot ?? 0,
                    State = engine.IsFinished ? "Finished" : engine.IsPaused ? "Paused" : engine.IsRunning ? "Running" : engine.IsBrowserAlive ? "Waiting" : "Stopped"
                });
        }
        return status;
    }

    private static ControlResult Ok(string message) => new() { Success = true, Message = message };
    private static ControlResult Fail(string message) => new() { Success = false, Message = message };
}
