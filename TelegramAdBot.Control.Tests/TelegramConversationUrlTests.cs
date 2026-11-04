using TelegramAdBot.Core;

namespace TelegramAdBot.Control.Tests;

public sealed class TelegramConversationUrlTests
{
	private const string TelegramWebUrl = "https://web.telegram.org/a/";

	[Fact]
	public void AcceptsExactConfiguredBotConversation()
	{
		Assert.True(TelegramController.IsExpectedBotConversationUrl(
			"https://web.telegram.org/a/#@tikible_bot", TelegramWebUrl, "tikible_bot"));
	}

	[Theory]
	[InlineData("https://web.telegram.org/a/", "tikible_bot")]
	[InlineData("https://web.telegram.org/a/#@another_bot", "tikible_bot")]
	[InlineData("https://web.telegram.org/k/#@tikible_bot", "tikible_bot")]
	[InlineData("https://example.org/k/#@tikible_bot", "tikible_bot")]
	[InlineData("https://web.telegram.org/a/#@tikible_bot/extra", "tikible_bot")]
	public void RejectsMissingOrDifferentConversation(string actualUrl, string botUsername)
	{
		Assert.False(TelegramController.IsExpectedBotConversationUrl(actualUrl, TelegramWebUrl, botUsername));
	}
}
