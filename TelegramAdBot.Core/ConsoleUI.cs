using System;
using System.Threading;
using TelegramAdBot.Helpers;

namespace TelegramAdBot.Core;

public static class ConsoleUI
{
	private static string _version => "v" + VersionInfo.CurrentString;

	public static void PrintAuthBanner()
	{
		Console.Clear();
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine();
		Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║         TELEGRAM AD BOT  —  Professional            ║");
		Console.WriteLine("  ║                    " + _version + "                           ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╠══════════════════════════════════════════════════════╣");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Automated Telegram advertising platform           ║");
		Console.WriteLine("  ║   Multi-instance • Bot cycling • Auto-start         ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Protected by KeyAuth licensing system             ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}

	public static void PrintLoginPrompt()
	{
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("  ┌─── LICENSE AUTHENTICATION ─────────────────────────┐");
		Console.WriteLine("  │                                                      │");
		Console.WriteLine("  │   Enter your KeyAuth credentials to continue.       │");
		Console.WriteLine("  │   Credentials will be saved for future logins.      │");
		Console.WriteLine("  │                                                      │");
		Console.WriteLine("  └──────────────────────────────────────────────────────┘");
		Console.ResetColor();
		Console.WriteLine();
	}

	public static void PrintAuthSuccess(string username)
	{
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine("  ✓  Authenticated as: " + username);
		Console.ResetColor();
		Console.WriteLine();
		Thread.Sleep(1000);
	}

	public static void PrintMainBanner(int botCount, int instanceCount, int cyclesPerBot, bool configLoaded)
	{
		Console.Clear();
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine();
		Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
		Console.WriteLine("  ║         TELEGRAM AD BOT  —  Professional            ║");
		Console.WriteLine("  ║                    " + _version + "                           ║");
		Console.WriteLine("  ╠══════════════════════════════════════════════════════╣");
		Console.WriteLine("  ║                                                      ║");
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine($"  ║   Bots loaded    : {botCount,-34} ║");
		Console.WriteLine($"  ║   Instances      : {instanceCount,-34} ║");
		Console.WriteLine($"  ║   Cycles per bot : {cyclesPerBot,-34} ║");
		Console.ForegroundColor = (configLoaded ? ConsoleColor.Green : ConsoleColor.Yellow);
		string cfgStatus = (configLoaded ? "✓ Loaded from config.json" : "⚠ Not set — pick positions first");
		Console.WriteLine($"  ║   Positions      : {cfgStatus,-34} ║");
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╠══════════════════════════════════════════════════════╣");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   ── HOTKEYS ───────────────────────────────────    ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Ctrl+Shift+G  →  Start / Resume all loops         ║");
		Console.WriteLine("  ║   Ctrl+Shift+P  →  Pause all instances              ║");
		Console.WriteLine("  ║   Ctrl+Shift+K  →  Kill / Emergency stop            ║");
		Console.WriteLine("  ║   Ctrl+Shift+Z  →  Exit application                 ║");
		Console.WriteLine("  ║   Ctrl+Shift+M  →  Mark sticker setup done          ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   ── POSITION PICKING ──────────────────────────    ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Ctrl+Alt+E    →  Pick EMOJI BUTTON position       ║");
		Console.WriteLine("  ║   Ctrl+Alt+F    →  Pick STICKER TAB position        ║");
		Console.WriteLine("  ║   Ctrl+Alt+T    →  Pick TEXTBOX position            ║");
		Console.WriteLine("  ║   Ctrl+Alt+S    →  Pick STICKER (to send)           ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╠══════════════════════════════════════════════════════╣");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   BOT1 = magenta   BOT2 = yellow   BOT3 = cyan      ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}

	public static void PrintFirstRunInstructions()
	{
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine("  ╔══ FIRST RUN GUIDE ══════════════════════════════════╗");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║  This is your first time running the bot.           ║");
		Console.WriteLine("  ║  Follow these steps carefully.                      ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║  STEP 1: Login to Telegram                          ║");
		Console.WriteLine("  ║    → 3 browser windows have opened                  ║");
		Console.WriteLine("  ║    → Scan the QR code in EACH browser               ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║  STEP 2: Set up the Sticker Panel                   ║");
		Console.WriteLine("  ║    → In EACH browser:                               ║");
		Console.WriteLine("  ║      a) Click the emoji button (bottom of chat)     ║");
		Console.WriteLine("  ║      b) Switch to the STICKER tab                   ║");
		Console.WriteLine("  ║    → Telegram remembers this automatically          ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║  STEP 3: Pick Click Positions (do it in order)      ║");
		Console.WriteLine("  ║    → Ctrl+Alt+E  : hover EMOJI BUTTON → wait 4s     ║");
		Console.WriteLine("  ║    → Ctrl+Alt+F  : hover STICKER TAB  → wait 4s     ║");
		Console.WriteLine("  ║    → Ctrl+Alt+S  : hover a STICKER    → wait 4s     ║");
		Console.WriteLine("  ║    → Ctrl+Alt+T  : hover TEXTBOX      → wait 4s     ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║  STEP 4: Confirm Setup Done                         ║");
		Console.WriteLine("  ║    → Press Ctrl+Shift+M                             ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║  STEP 5: Start                                      ║");
		Console.WriteLine("  ║    → Press Ctrl+Shift+G to start all loops          ║");
		Console.WriteLine("  ║    → Future launches: loops start AUTOMATICALLY     ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}

	public static void PrintPickInstructions()
	{
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("  ╔══ PICK POSITIONS ═══════════════════════════════════╗");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Ctrl+Alt+E  →  Hover over EMOJI BUTTON → wait 4s  ║");
		Console.WriteLine("  ║   Ctrl+Alt+F  →  Hover over STICKER TAB  → wait 4s  ║");
		Console.WriteLine("  ║   Ctrl+Alt+S  →  Hover over a STICKER    → wait 4s  ║");
		Console.WriteLine("  ║   Ctrl+Alt+T  →  Hover over TEXTBOX      → wait 4s  ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   After picking all 4:                              ║");
		Console.WriteLine("  ║   → Press Ctrl+Shift+G to START                     ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}

	public static void PrintReadyBanner()
	{
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine("  ╔══ READY ════════════════════════════════════════════╗");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   ✓ All positions loaded                            ║");
		Console.WriteLine("  ║   ✓ Browsers open and ready                         ║");
		Console.WriteLine("  ║   ✓ Auto-starting in 3 seconds...                   ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Bot will:                                         ║");
		Console.WriteLine("  ║   → Auto-click emoji button on each bot             ║");
		Console.WriteLine("  ║   → Run cycles automatically                        ║");
		Console.WriteLine("  ║   → Switch bots every N cycles                      ║");
		Console.WriteLine("  ║   → Close and exit when all bots done               ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}

	public static void PrintSeparator()
	{
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine("  ──────────────────────────────────────────────────────");
		Console.ResetColor();
	}
}
