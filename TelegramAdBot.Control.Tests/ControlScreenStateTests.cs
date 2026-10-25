using TelegramAdBot.Control;

namespace TelegramAdBot.Control.Tests;

public sealed class ControlScreenStateTests
{
    [Fact]
    public void NavigationRetainsPrimaryMessageAndPromotionWorkflow()
    {
        var state = new ControlScreenState();
        state.TrackPrimaryMessage(42);
        state.BeginWorkflow("instances:0");

        state.Navigate("dashboard");
        state.Navigate("instances");
        state.Navigate("dashboard");

        Assert.Equal(42, state.PrimaryMessageId);
        Assert.Equal("dashboard", state.Screen);
        Assert.Equal("instances:0", state.ActiveWorkflowStep);
    }

    [Theory]
    [InlineData("accounts")]
    [InlineData("bots")]
    [InlineData("activity")]
    [InlineData("settings")]
    [InlineData("help")]
    [InlineData("promotion")]
    public void NavigationChangesScreenWithoutReplacingMessage(string screen)
    {
        var state = new ControlScreenState();
        state.TrackPrimaryMessage(13);

        state.Navigate(screen);

        Assert.Equal(screen, state.Screen);
        Assert.Equal(13, state.PrimaryMessageId);
    }

    [Fact]
    public void NewWorkflowStepReplacesOldStepAndStaleRevisionIsRejected()
    {
        var state = new ControlScreenState();
        int firstRevision = state.BeginWorkflow("bots");
        int nextRevision = state.BeginWorkflow("instances");

        Assert.NotEqual(firstRevision, nextRevision);
        Assert.False(state.IsCurrentWorkflowRevision(firstRevision));
        Assert.True(state.IsCurrentWorkflowRevision(nextRevision));
        Assert.Equal("instances", state.ActiveWorkflowStep);
    }


}
