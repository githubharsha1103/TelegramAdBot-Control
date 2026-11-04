using System.Text.Json;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Control.Tests;

public sealed class BotSearchResultPositionTests
{
	[Fact]
	public void BotConfigDefaultsToConfiguredWebASearchResultPosition()
	{
		var config = new BotConfig();

		Assert.Equal("https://web.telegram.org/a/", config.TelegramWebUrl);
		Assert.Equal(228, config.BotSearchResultX);
		Assert.Equal(298, config.BotSearchResultY);
		Assert.Equal(993, config.EmojiButtonX);
		Assert.Equal(530, config.EmojiButtonY);
		Assert.Equal(816, config.StickerTabX);
		Assert.Equal(420, config.StickerTabY);
		Assert.Equal(655, config.StickerX);
		Assert.Equal(385, config.StickerY);
	}

	[Fact]
	public void SearchResultPositionPersistsInSavedPositions()
	{
		var positions = new SavedPositions { BotSearchResultX = 228, BotSearchResultY = 298 };
		string json = JsonSerializer.Serialize(positions, AppJsonContext.Default.SavedPositions);
		SavedPositions loaded = JsonSerializer.Deserialize(json, AppJsonContext.Default.SavedPositions)!;

		Assert.Contains("\"BotSearchResultX\":228", json);
		Assert.Contains("\"BotSearchResultY\":298", json);
		Assert.Equal(228, loaded.BotSearchResultX);
		Assert.Equal(298, loaded.BotSearchResultY);
		Assert.Equal(993, loaded.EmojiButtonX);
		Assert.Equal(530, loaded.EmojiButtonY);
		Assert.Equal(816, loaded.StickerTabX);
		Assert.Equal(420, loaded.StickerTabY);
		Assert.Equal(655, loaded.StickerX);
		Assert.Equal(385, loaded.StickerY);
	}

	[Fact]
	public void OlderPositionFilesUseConfiguredSearchResultDefaults()
	{
		SavedPositions loaded = JsonSerializer.Deserialize("{\"TextboxX\":10}", AppJsonContext.Default.SavedPositions)!;

		Assert.Equal(228, loaded.BotSearchResultX);
		Assert.Equal(298, loaded.BotSearchResultY);
	}
}
