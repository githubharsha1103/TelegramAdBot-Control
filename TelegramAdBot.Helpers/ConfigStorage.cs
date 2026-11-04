using System;
using System.IO;
using System.Text.Json;
using TelegramAdBot.Models;

namespace TelegramAdBot.Helpers;

public static class ConfigStorage
{
	public static void SavePositions(BotConfig config)
	{
		try
		{
			AppDataPaths.EnsureDirectories();
			string json = JsonSerializer.Serialize(new SavedPositions
			{
				TextboxX = config.TextboxX,
				TextboxY = config.TextboxY,
				EmojiButtonX = config.EmojiButtonX,
				EmojiButtonY = config.EmojiButtonY,
				StickerTabX = config.StickerTabX,
				StickerTabY = config.StickerTabY,
				StickerX = config.StickerX,
				StickerY = config.StickerY,
				BotSearchResultX = config.BotSearchResultX,
				BotSearchResultY = config.BotSearchResultY,
				AccountMenuX = config.AccountMenuX, AccountMenuY = config.AccountMenuY,
				Account1X = config.Account1X, Account1Y = config.Account1Y,
				Account2X = config.Account2X, Account2Y = config.Account2Y,
				Account3X = config.Account3X, Account3Y = config.Account3Y
			}, AppJsonContext.Default.SavedPositions);
			File.WriteAllText(AppDataPaths.ConfigFile, json);
			Logger.Success("Positions saved to AppData.");
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to save config: " + ex.Message);
		}
	}

	public static bool LoadPositions(BotConfig config)
	{
		try
		{
			if (!File.Exists(AppDataPaths.ConfigFile))
			{
				Logger.Warning("No saved positions found.");
				return false;
			}
			SavedPositions data = JsonSerializer.Deserialize(File.ReadAllText(AppDataPaths.ConfigFile), AppJsonContext.Default.SavedPositions);
			if (data == null)
			{
				return false;
			}
			config.TextboxX = data.TextboxX;
			config.TextboxY = data.TextboxY;
			bool stickerCoordinatesChanged = data.EmojiButtonX != 992 || data.EmojiButtonY != 599 ||
				data.StickerTabX != 815 || data.StickerTabY != 534 || data.StickerX != 655 || data.StickerY != 450;
			config.EmojiButtonX = 992;
			config.EmojiButtonY = 599;
			config.StickerTabX = 815;
			config.StickerTabY = 534;
			config.StickerX = 655;
			config.StickerY = 450;
			config.BotSearchResultX = data.BotSearchResultX;
			config.BotSearchResultY = data.BotSearchResultY;
			config.AccountMenuX = data.AccountMenuX; config.AccountMenuY = data.AccountMenuY;
			config.Account1X = data.Account1X; config.Account1Y = data.Account1Y;
			config.Account2X = data.Account2X; config.Account2Y = data.Account2Y;
			config.Account3X = data.Account3X; config.Account3Y = data.Account3Y;
			Logger.Success("Positions loaded:");
			Logger.Info($"Textbox     : ({config.TextboxX},{config.TextboxY})");
			Logger.Info($"EmojiButton : ({config.EmojiButtonX},{config.EmojiButtonY})");
			Logger.Info($"StickerTab  : ({config.StickerTabX},{config.StickerTabY})");
			Logger.Info($"Sticker     : ({config.StickerX},{config.StickerY})");
			if (stickerCoordinatesChanged)
			{
				Logger.Info("Updating persisted Telegram Web A sticker coordinates.");
				SavePositions(config);
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to load config: " + ex.Message);
			return false;
		}
	}

	public static bool IsSetupDone()
	{
		return File.Exists(AppDataPaths.SetupFlagFile);
	}

	public static void MarkSetupDone()
	{
		try
		{
			AppDataPaths.EnsureDirectories();
			File.WriteAllText(AppDataPaths.SetupFlagFile, $"Setup:{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			Logger.Success("Setup flag saved.");
		}
		catch (Exception ex)
		{
			Logger.Error("Flag error: " + ex.Message);
		}
	}
}
