using System.Collections.Generic;
using System.Text.Json.Serialization;
using TelegramAdBot.Helpers;

namespace TelegramAdBot.Models;

public class BotConfig
{
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

	public int StaggerDelayMs { get; set; } = 2000;

	public int CyclesPerBot { get; set; } = 500;

	public int BotSwitchDelayMs { get; set; } = 3000;

	public string BotsFilePath => AppDataPaths.BotsFile;

	public string ConfigFilePath => AppDataPaths.ConfigFile;

	public string SetupFlagFile => AppDataPaths.SetupFlagFile;

	public string[] PromoMessages { get; set; } = new string[8] { "Join fast", "Join it", "This one is best", "Its completely free", "Highly recommended", "Try it now", "Best one here", "Don't miss this" };

	public string TelegramWebUrl { get; set; } = "https://web.telegram.org/k/";

	public bool Headless { get; set; }

	public bool StartMinimized { get; set; }

	public bool AutoStartLoop { get; set; } = true;

	public string SessionBasePath { get; set; } = "profile";

	public int BrowserWidth { get; set; } = 1100;

	public int BrowserHeight { get; set; } = 720;

	public int ViewportWidth { get; set; } = 1100;

	public int ViewportHeight { get; set; } = 640;

	public int TextboxX { get; set; }

	public int TextboxY { get; set; }

	public int EmojiButtonX { get; set; }

	public int EmojiButtonY { get; set; }

	public int StickerTabX { get; set; }

	public int StickerTabY { get; set; }

	public int StickerX { get; set; }

	public int StickerY { get; set; }

	public int AccountMenuX { get; set; } = 80;
	public int AccountMenuY { get; set; } = 274;
	public int Account1X { get; set; } = 134;
	public int Account1Y { get; set; } = 321;
	public int Account2X { get; set; } = 142;
	public int Account2Y { get; set; } = 358;
	public int Account3X { get; set; } = 152;
	public int Account3Y { get; set; } = 392;

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
