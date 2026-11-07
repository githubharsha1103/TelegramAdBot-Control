using System.Collections.Generic;
using System.Text.Json.Serialization;
using TelegramAdBot.Helpers;

namespace TelegramAdBot.Models;

public class BotConfig
{
	public const string MaximumInstanceCountEnvironmentVariable = "TELEGRAM_MAX_CHROMIUM_INSTANCES";
	public static int ResolveMaximumInstanceCount(int configuredValue)
	{
		string configured = System.Environment.GetEnvironmentVariable(MaximumInstanceCountEnvironmentVariable) ?? "";
		if (int.TryParse(configured, out int environmentValue) && environmentValue > 0) return environmentValue;
		return configuredValue > 0 ? configuredValue : int.MaxValue;
	}
	public int WaitAfterSearch { get; set; } = 2000;

	public int WaitAfterEmojiClick { get; set; } = 400;

	public int WaitAfterSticker { get; set; } = 600;

	public int WaitAfterPromo { get; set; } = 1000;

	public int WaitAfterStop { get; set; } = 1500;

	public int TypeDelayMs { get; set; } = 50;

	public int WaitAfterNavigate { get; set; } = 5000;

	public int WaitAfterEmojiButton { get; set; } = 700;

	public int WaitAfterStickerTab { get; set; } = 500;

	public int InstanceCount { get; set; } = 3;

	public int MaxSupportedInstanceCount { get; set; } = int.MaxValue;

	public int StaggerDelayMs { get; set; } = 2000;

	public int CyclesPerBot { get; set; } = 500;

	public int BotSwitchDelayMs { get; set; } = 3000;

	public string BotsFilePath => AppDataPaths.BotsFile;

	public string ConfigFilePath => AppDataPaths.ConfigFile;

	public string SetupFlagFile => AppDataPaths.SetupFlagFile;

	public string[] PromoMessages { get; set; } = new string[8] { "Join fast", "Join it", "This one is best", "Its completely free", "Highly recommended", "Try it now", "Best one here", "Don't miss this" };

	public string TelegramWebUrl { get; set; } = "https://web.telegram.org/a/";

	public bool Headless { get; set; }

	public bool StartMinimized { get; set; }

	public bool AutoStartLoop { get; set; } = true;

	public string SessionBasePath { get; set; } = "profile";

	public int BrowserWidth { get; set; } = 1100;

	public int BrowserHeight { get; set; } = 785;

	public int ViewportWidth { get; set; } = 1100;

	public int ViewportHeight { get; set; } = 640;

	public int TextboxX { get; set; }

	public int TextboxY { get; set; }

	public int EmojiButtonX { get; set; } = 992;

	public int EmojiButtonY { get; set; } = 599;

	public int StickerTabX { get; set; } = 815;

	public int StickerTabY { get; set; } = 534;

	public int StickerX { get; set; } = 655;

	public int StickerY { get; set; } = 450;

	public int BotSearchResultX { get; set; } = 228;

	public int BotSearchResultY { get; set; } = 298;

	public int AccountMenuX { get; set; } = 46;
	public int AccountMenuY { get; set; } = 44;
	public int Account1X { get; set; } = 110;
	public int Account1Y { get; set; } = 91;
	public int Account2X { get; set; } = 113;
	public int Account2Y { get; set; } = 131;
	public int Account3X { get; set; } = 118;
	public int Account3Y { get; set; } = 166;

	public bool AllSet
	{
		get
		{
			if (TextboxX > 0 && TextboxY > 0 && EmojiButtonX > 0 && EmojiButtonY > 0 && StickerTabX > 0 && StickerTabY > 0 && StickerX > 0)
			{
				return StickerY > 0;
			}
			return false;
		}
	}

	[JsonIgnore]
	public List<string> BotList { get; set; } = new List<string>();

	[JsonIgnore]
	public bool SetupDone { get; set; }
}
