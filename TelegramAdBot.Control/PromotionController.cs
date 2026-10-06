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

            var available = _botConfig.GetBots();
            var requested = run.SelectedBots.Select(x => x.Trim().TrimStart('@')).Where(x => x.Length > 0).Distinct().ToList();
            if (requested.Count == 0) return Fail("Select at least one bot.");
            if (requested.Any(x => !available.Contains(x, StringComparer.OrdinalIgnoreCase))) return Fail("One or more selected bots are no longer available.");
            if (run.InstanceCount < 1 || run.InstanceCount > 20) return Fail("Instance count must be between 1 and 20.");
            if (run.CyclesPerBot < 1 || run.CyclesPerBot > 100000) return Fail("Cycles per bot must be between 1 and 100000.");

            _run = new PromotionRunConfiguration { SelectedBots = requested, InstanceCount = run.InstanceCount, CyclesPerBot = run.CyclesPerBot };
            _config.BotList = requested;
            _config.InstanceCount = run.InstanceCount;
            _config.CyclesPerBot = run.CyclesPerBot;
            _manager = null;
            _startedAt = null;
            _lastError = "";
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

            _manager = new InstanceManager(_config);
            _manager.OnAllFinished += () => Logger.Info("[CONTROL] All promotion instances completed.");
            Logger.Info("[CONTROL] START requested.");
            await _manager.OpenAllAsync();
            if (!_manager.AnyBrowserAlive) return Fail("No Chromium instances started. Check engine logs and DISPLAY.");
            _startedAt = DateTimeOffset.UtcNow;
            await _manager.StartAllAsync();
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

    public async Task<ControlResult> PauseAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            if (_manager?.AnyRunning != true) return Fail("Promotion is not running.");
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
            await _manager.StartAllAsync();
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
            IsRunning = manager?.AnyRunning == true,
            IsPaused = manager?.AnyRunning == true && manager.Engines.Where(x => x.IsRunning).All(x => x.IsPaused),
            IsStopping = _isStopping,
            StartTime = started,
            RuntimeSeconds = started.HasValue ? (long)(DateTimeOffset.UtcNow - started.Value).TotalSeconds : 0,
            SelectedBots = _run?.SelectedBots.ToList() ?? new List<string>(),
            InstanceCount = _run?.InstanceCount ?? 0,
            CyclesPerBot = _run?.CyclesPerBot ?? 0,
			// Each existing engine instance processes the configured bot list independently.
			TotalCycles = (_run?.SelectedBots.Count ?? 0) * (_run?.CyclesPerBot ?? 0) * (_run?.InstanceCount ?? 0),
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
