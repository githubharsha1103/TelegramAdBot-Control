using System;
using System.Collections.Generic;
using TelegramAdBot.Models;

namespace TelegramAdBot.Control;

public sealed class PromotionRunConfiguration
{
    public int MaxSupportedInstanceCount { get; set; } = int.MaxValue;
    public List<string> SelectedBots { get; set; } = new();
    public int InstanceCount { get; set; }
    public List<int> SelectedInstanceIds { get; set; } = new();
    public int CyclesPerBot { get; set; }
    public int AccountCount { get; set; } = 1;
    public List<int> SelectedAccounts { get; set; } = new() { 1 };
    public Dictionary<int, List<int>> AccountsByInstance { get; set; } = new();
}

public sealed class PromotionStatus
{
    public int MaxSupportedInstanceCount { get; set; } = int.MaxValue;
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public bool IsStopping { get; set; }
    public bool IsConfigured { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public long RuntimeSeconds { get; set; }
    public List<string> SelectedBots { get; set; } = new();
    public int InstanceCount { get; set; }
    public List<int> SelectedInstanceIds { get; set; } = new();
    public List<int> RunningInstanceIds { get; set; } = new();
    public int CyclesPerBot { get; set; }
    public int CurrentAccount { get; set; } = 1;
    public int AccountCount { get; set; } = 1;
    public List<int> SelectedAccounts { get; set; } = new();
    public Dictionary<int, List<int>> AccountsByInstance { get; set; } = new();
    public int CurrentSelectedAccountIndex { get; set; }
    public string AccountState { get; set; } = "Idle";
    public int TotalCycles { get; set; }
    public int CompletedCycles { get; set; }
    public int FailedCycles { get; set; }
    public string LastError { get; set; } = "";
    public List<PromotionInstanceStatus> Instances { get; set; } = new();
}

public sealed class PromotionInstanceStatus
{
    public int InstanceId { get; set; }
    public int CurrentAccount { get; set; } = 1;
    public int SelectedAccountIndex { get; set; }
    public int SelectedAccountCount { get; set; } = 1;
    public string AccountState { get; set; } = "";
    public string Bot { get; set; } = "";
    public int CurrentCycle { get; set; }
    public int TotalCyclesForBot { get; set; }
    public string State { get; set; } = "Waiting";
    public string Error { get; set; } = "";
}

public sealed class ControlResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
