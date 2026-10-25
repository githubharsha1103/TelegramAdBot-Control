namespace TelegramAdBot.Control;

/// <summary>Pure per-chat state used by the Telegram control center.</summary>
public sealed class ControlScreenState
{
    public static readonly string[][] NavigationRows =
    {
        new[] { "🏠 Dashboard", "🚀 Promotion" },
        new[] { "🖥 Instances", "👥 Accounts" },
        new[] { "🤖 Bots", "📊 Activity" },
        new[] { "⚙️ Settings", "❓ Help" }
    };

    private int _workflowRevision;
    public int? PrimaryMessageId { get; private set; }
    public string Screen { get; private set; } = "dashboard";
    public string? ActiveWorkflowStep { get; private set; }

    public void TrackPrimaryMessage(int messageId) => PrimaryMessageId = messageId;
    public void Navigate(string screen) => Screen = screen;
    public int BeginWorkflow(string step)
    {
        ActiveWorkflowStep = step;
        Screen = "promotion";
        return ++_workflowRevision;
    }
    public bool IsCurrentWorkflowRevision(int revision) => revision == _workflowRevision;
}
