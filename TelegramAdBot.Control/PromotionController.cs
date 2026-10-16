using System;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<int, InstanceAccountProgress> _instanceProgress = new();
    private readonly object _errorLock = new();
    private InstanceManager? _manager;
    private PromotionRunConfiguration? _run;
    private DateTimeOffset? _startedAt;
    private bool _isStopping;
    private string _lastError = "";
    private int _currentAccount = 1;
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
            if (_manager?.HasBrowserResources == true || _manager?.AnyRunning == true)
                await _manager.StopAllAsync();

            var available = _botConfig.GetBots();
            var requested = run.SelectedBots.Select(x => x.Trim().TrimStart('@')).Where(x => x.Length > 0).Distinct().ToList();
            if (requested.Count == 0) return Fail("Select at least one bot.");
            if (requested.Any(x => !available.Contains(x, StringComparer.OrdinalIgnoreCase))) return Fail("One or more selected bots are no longer available.");
            var selectedInstances = (run.SelectedInstanceIds ?? new List<int>()).Distinct().OrderBy(x => x).ToList();
            if (selectedInstances.Count == 0 || selectedInstances.Any(x => x < 1 || x > 3)) return Fail("Select at least one valid browser instance (1, 2, or 3).");
            if (run.CyclesPerBot < 1 || run.CyclesPerBot > 100000) return Fail("Cycles per bot must be between 1 and 100000.");
            var selectedAccounts = (run.SelectedAccounts ?? new List<int>()).Distinct().OrderBy(x => x).ToList();
            var accountsByInstance = new Dictionary<int, List<int>>();
            foreach (int instanceId in selectedInstances)
            {
                List<int> accounts = run.AccountsByInstance != null && run.AccountsByInstance.TryGetValue(instanceId, out var configuredAccounts)
                    ? configuredAccounts.Distinct().OrderBy(x => x).ToList()
                    : selectedAccounts;
                if (accounts.Count == 0 || accounts.Any(x => x < 1 || x > 3)) return Fail($"Select at least one valid Telegram account for instance {instanceId}.");
                accountsByInstance[instanceId] = accounts;
            }

            var unionAccounts = accountsByInstance.Values.SelectMany(x => x).Distinct().OrderBy(x => x).ToList();
            _run = new PromotionRunConfiguration { SelectedBots = requested, InstanceCount = selectedInstances.Count, SelectedInstanceIds = selectedInstances, CyclesPerBot = run.CyclesPerBot, SelectedAccounts = selectedInstances.Count == 1 ? accountsByInstance[selectedInstances[0]].ToList() : unionAccounts, AccountsByInstance = accountsByInstance, AccountCount = accountsByInstance.Values.Sum(x => x.Count) };
            foreach (var assignment in accountsByInstance) Logger.Info($"[Account] Instance {assignment.Key} selected accounts: [{string.Join(",", assignment.Value)}]");
            Logger.Info("[Instance] Selected instances: [" + string.Join(",", selectedInstances) + "]");
            _config.BotList = requested;
            _config.InstanceCount = selectedInstances.Count;
            _config.CyclesPerBot = run.CyclesPerBot;
            _manager = null;
            _startedAt = null;
            _lastError = "";
            _currentAccount = 1;
            _currentSelectedAccountIndex = 0;
            _accountState = "Preparing first account";
            _instanceProgress.Clear();
            foreach (int instanceId in selectedInstances)
                _instanceProgress[instanceId] = new InstanceAccountProgress(0, 1, "Waiting");
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

            _config.InstanceCount = _run.SelectedInstanceIds.Count;
            _manager = new InstanceManager(_config, _run.SelectedInstanceIds);
            _manager.ConfigureAccounts(_run.AccountsByInstance.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value));
            _manager.SetAccountCompletionHandler(AdvanceAccountAsync);
            _manager.OnAllFinished += () => Logger.Info("[CONTROL] All promotion instances completed.");
            Logger.Info("[CONTROL] START requested.");
            var failedBrowserIds = await _manager.OpenSelectedBrowsersAsync();
            if (failedBrowserIds.Count > 0)
            {
                string error = "Browser preparation failed for instances: [" + string.Join(",", failedBrowserIds) + "].";
                foreach (int instanceId in failedBrowserIds) SetInstanceProgress(instanceId, 0, 1, "Failed", error);
                SetLastError(error);
                return Fail(error);
            }
            _startedAt = DateTimeOffset.UtcNow;
            _currentAccount = 1;
            _currentSelectedAccountIndex = 0;
            foreach (var engine in _manager.Engines)
            {
                int instanceId = engine.InstanceId;
                int firstAccount = _run.AccountsByInstance[instanceId][0];
                SetInstanceProgress(instanceId, 0, 1, firstAccount == 1 ? "Initializing Account 1..." : $"Switching to Account {firstAccount}...");
                Logger.Info($"[Account] Instance {instanceId} preparing Account {firstAccount}; Telegram initially uses Account 1.");
                engine.SetSelectedAccountIndex(0);
                if (!await engine.PrepareFirstAccountAsync(firstAccount))
                {
                    string error = $"Instance {instanceId} Account {firstAccount} preparation/initialization failed.";
                    SetInstanceProgress(instanceId, 0, 1, "Failed", error);
                    SetLastError(error);
                    Logger.Error("[CONTROL] " + error);
                    return Fail(error);
                }
                SetInstanceProgress(instanceId, 0, firstAccount, $"Account {firstAccount} ready");
            }
            var failedStartIds = await _manager.StartSelectedAsync();
            if (failedStartIds.Count > 0)
            {
                string error = "Promotion engine failed to start for instances: [" + string.Join(",", failedStartIds) + "].";
                foreach (var engine in _manager.Engines.Where(engine => failedStartIds.Contains(engine.InstanceId))) SetInstanceProgress(engine.InstanceId, 0, _run.AccountsByInstance[engine.InstanceId][0], "Failed", error);
                SetLastError(error);
                return Fail(error);
            }
            foreach (var engine in _manager.Engines)
            {
                int firstAccount = _run.AccountsByInstance[engine.InstanceId][0];
                SetInstanceProgress(engine.InstanceId, 0, firstAccount, $"Account {firstAccount} promotion running...");
                Logger.Info($"[Promotion] Instance {engine.InstanceId} starting promotion for Account {firstAccount}.");
            }
            if (_run.SelectedInstanceIds.Count == 1) _accountState = $"Account {_run.AccountsByInstance[_run.SelectedInstanceIds[0]][0]} promotion running...";
            else _accountState = "Per-instance account workflows running";
            return Ok("Promotion started.");
        }
        catch (Exception ex)
        {
            SetLastError(ex.Message);
            Logger.Error("[CONTROL] START failed: " + ex.Message);
            return Fail("Start failed: " + ex.Message);
        }
        finally { _commandLock.Release(); }
    }

    private async Task<bool> AdvanceAccountAsync(int instanceId)
    {
        if (_run == null || _isStopping || !_instanceProgress.TryGetValue(instanceId, out var progress)) return false;
        List<int> accounts = _run.AccountsByInstance[instanceId];
        if (progress.SelectedAccountIndex + 1 >= accounts.Count) return false;
        while (_manager?.IsPaused == true && !_isStopping) await Task.Delay(200);
        if (_isStopping) return false;
        int nextIndex = progress.SelectedAccountIndex + 1;
        int next = accounts[nextIndex];
        SetInstanceProgress(instanceId, progress.SelectedAccountIndex, progress.CurrentAccount, $"Switching to Account {next}...");
        Logger.Info($"[CONTROL] Instance {instanceId} switching Account {progress.CurrentAccount} -> Account {next}.");
        try
        {
            if (_manager == null || !await _manager.SwitchAccountAsync(instanceId, next))
            {
                _manager?.GetInstance(instanceId)?.RetainBrowserAfterAccountFailure();
                string error = $"Instance {instanceId} account switch to Account {next} failed; promotion halted safely.";
                SetInstanceProgress(instanceId, progress.SelectedAccountIndex, progress.CurrentAccount, "Failed", error);
                SetLastError(error);
                Logger.Error("[CONTROL] " + error);
                return false;
            }
            SetInstanceProgress(instanceId, nextIndex, next, $"Initializing Account {next}...");
            _manager.SetRunForNextAccount(instanceId);
            _manager.GetInstance(instanceId)?.SetSelectedAccountIndex(nextIndex);
            while (_manager.IsPaused && !_isStopping) await Task.Delay(200);
            if (_isStopping) return false;
            if (!await _manager.InitializeEngineAsync(instanceId)) throw new InvalidOperationException("Fresh account initialization failed.");
            _manager.GetInstance(instanceId)?.MarkAccountInitializedAtBoundary();
            SetInstanceProgress(instanceId, nextIndex, next, $"Account {next} promotion running...");
            Logger.Info($"[Promotion] Instance {instanceId} starting promotion for Account {next}.");
            return true;
        }
        catch (Exception ex)
        {
            _manager?.GetInstance(instanceId)?.RetainBrowserAfterAccountFailure();
            string error = $"Instance {instanceId} account switch/initialization to Account {next} failed: {ex.Message}";
            SetInstanceProgress(instanceId, nextIndex, next, "Failed", error);
            SetLastError(error);
            Logger.Error("[CONTROL] " + error);
            return false;
        }
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
            if (_manager == null || (!_manager.HasBrowserResources && !_manager.AnyRunning)) return Fail("Promotion is already stopped.");
            _isStopping = true;
            Logger.Info("[CONTROL] STOP requested.");
            await _manager.CleanupAsync();
            if (_manager.HasBrowserResources || _manager.AnyBrowserAlive)
                throw new InvalidOperationException("Browser resources remain after cleanup verification.");
            return Ok("Promotion stopped.");
        }
        catch (Exception ex) { _lastError = ex.Message; return Fail("Stop failed: " + ex.Message); }
        finally { _isStopping = false; _commandLock.Release(); }
    }

    public PromotionStatus GetStatus()
    {
        var manager = _manager;
        var started = _startedAt;
        var selectedIds = _run?.SelectedInstanceIds.ToList() ?? new List<int>();
        bool accountTransitionActive = _instanceProgress.Values.Any(progress =>
            progress.State.StartsWith("Switching", StringComparison.OrdinalIgnoreCase) ||
            progress.State.StartsWith("Initializing", StringComparison.OrdinalIgnoreCase));
        bool promotionStateActive = manager?.Engines.Any(engine => !engine.IsFinished &&
            _instanceProgress.TryGetValue(engine.InstanceId, out var progress) &&
            progress.State.Contains("promotion running", StringComparison.OrdinalIgnoreCase)) == true;
        var firstProgress = selectedIds.Count > 0 && _instanceProgress.TryGetValue(selectedIds[0], out var value) ? value : null;
        var status = new PromotionStatus
        {
            IsConfigured = _run != null,
            IsRunning = manager?.AnyRunning == true || accountTransitionActive || promotionStateActive,
            IsStopping = _isStopping,
            CurrentAccount = firstProgress?.CurrentAccount ?? _currentAccount,
            AccountCount = _run?.AccountCount ?? 1,
            SelectedAccounts = _run?.SelectedAccounts.ToList() ?? new List<int>(),
            AccountsByInstance = _run?.AccountsByInstance.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()) ?? new Dictionary<int, List<int>>(),
            CurrentSelectedAccountIndex = firstProgress?.SelectedAccountIndex ?? _currentSelectedAccountIndex,
            AccountState = selectedIds.Count > 1 ? "Account progress is shown per instance." : firstProgress?.State ?? _accountState,
            IsPaused = manager?.IsPaused == true,
            StartTime = started,
            RuntimeSeconds = started.HasValue ? (long)(DateTimeOffset.UtcNow - started.Value).TotalSeconds : 0,
            SelectedBots = _run?.SelectedBots.ToList() ?? new List<string>(),
            InstanceCount = _run?.InstanceCount ?? 0,
            SelectedInstanceIds = selectedIds,
            RunningInstanceIds = manager?.Engines
                .Where(engine => engine.IsRunning || engine.IsInitializing || engine.IsAccountSwitching ||
                    (_instanceProgress.TryGetValue(engine.InstanceId, out var progress) &&
                     (progress.State.StartsWith("Switching", StringComparison.OrdinalIgnoreCase) || progress.State.StartsWith("Initializing", StringComparison.OrdinalIgnoreCase) ||
                      (!engine.IsFinished && progress.State.Contains("promotion running", StringComparison.OrdinalIgnoreCase)))))
                .Select(engine => engine.InstanceId).OrderBy(id => id).ToList() ?? new List<int>(),
            CyclesPerBot = _run?.CyclesPerBot ?? 0,
			// Each existing engine instance processes the configured bot list independently.
			TotalCycles = (_run?.SelectedBots.Count ?? 0) * (_run?.CyclesPerBot ?? 0) * (_run?.AccountsByInstance.Values.Sum(accounts => accounts.Count) ?? 0),
            CompletedCycles = manager?.Engines.Sum(x => x.CompletedCycles) ?? 0,
            LastError = _lastError
        };
        if (manager != null)
        {
            foreach (var engine in manager.Engines)
            {
                _instanceProgress.TryGetValue(engine.InstanceId, out var progress);
                status.Instances.Add(new PromotionInstanceStatus
                {
                    InstanceId = engine.InstanceId,
                    CurrentAccount = progress?.CurrentAccount ?? engine.CurrentAccount,
                    SelectedAccountIndex = progress?.SelectedAccountIndex ?? engine.SelectedAccountIndex,
                    SelectedAccountCount = engine.SelectedAccountCount,
                    AccountState = engine.IsFinished && progress?.State != "Failed" ? "Complete" : progress?.State ?? engine.AccountState,
                    Bot = engine.CurrentBotUsername,
                    CurrentCycle = engine.CyclesOnCurrentBot,
                    TotalCyclesForBot = _run?.CyclesPerBot ?? 0,
                    State = progress?.State == "Failed" ? "Failed" : engine.IsFinished ? "Finished" : engine.IsPaused ? "Paused" : engine.IsRunning ? "Running" : engine.IsBrowserAlive ? "Waiting" : "Stopped",
                    Error = progress?.Error ?? ""
                });
            }
        }
        return status;
    }

    private void SetInstanceProgress(int instanceId, int selectedAccountIndex, int currentAccount, string state, string error = "")
    {
        _instanceProgress[instanceId] = new InstanceAccountProgress(selectedAccountIndex, currentAccount, state, error);
        if (_run?.SelectedInstanceIds.Count == 1)
        {
            _currentSelectedAccountIndex = selectedAccountIndex;
            _currentAccount = currentAccount;
            _accountState = state;
        }
    }

    private void SetLastError(string error)
    {
        lock (_errorLock)
        {
            _lastError = string.IsNullOrWhiteSpace(_lastError) ? error : _lastError + " | " + error;
        }
    }

    private sealed record InstanceAccountProgress(int SelectedAccountIndex, int CurrentAccount, string State, string Error = "");

    private static ControlResult Ok(string message) => new() { Success = true, Message = message };
    private static ControlResult Fail(string message) => new() { Success = false, Message = message };
}
