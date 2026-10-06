using System;
using System.Collections.Generic;

namespace TelegramAdBot.Control;

public sealed class PromotionRunConfiguration
{
    public List<string> SelectedBots { get; set; } = new();
    public int InstanceCount { get; set; }
    public int CyclesPerBot { get; set; }
}

public sealed class PromotionStatus
{
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public bool IsStopping { get; set; }
    public bool IsConfigured { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public long RuntimeSeconds { get; set; }
    public List<string> SelectedBots { get; set; } = new();
    public int InstanceCount { get; set; }
    public int CyclesPerBot { get; set; }
    public int TotalCycles { get; set; }
    public int CompletedCycles { get; set; }
    public int FailedCycles { get; set; }
    public string LastError { get; set; } = "";
    public List<PromotionInstanceStatus> Instances { get; set; } = new();
}

public sealed class PromotionInstanceStatus
{
    public int InstanceId { get; set; }
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
