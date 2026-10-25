using TelegramAdBot.Control;

namespace TelegramAdBot.Control.Tests;

public sealed class ControlTextNavigationTests
{
    [Theory]
    [InlineData("🏠 Dashboard", "dashboard")]
    [InlineData("/start", "dashboard")]
    [InlineData(" /dashboard ", "dashboard")]
    [InlineData("🚀 Promotion", "promotion")]
    [InlineData("🤖 Bots", "bots")]
    [InlineData("👥 Accounts", "accounts")]
    [InlineData("🖥 Instances", "instances")]
    [InlineData("📊 Activity", "activity")]
    [InlineData("/status", "activity")]
    [InlineData("⚙️ Settings", "settings")]
    [InlineData("❓ Help", "help")]
    public void ReplyKeyboardAndCommandsResolveToScreen(string text, string expected) =>
        Assert.Equal(expected, ControlTextNavigation.Resolve(text));

    [Fact]
    public void UnknownTextDoesNotSelectAScreen() => Assert.Null(ControlTextNavigation.Resolve("delete everything"));
}
