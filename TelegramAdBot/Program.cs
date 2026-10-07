using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TelegramAdBot.Core;
using TelegramAdBot.Control;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot;

internal class Program
{
	private static readonly BotConfig _config = new BotConfig();

	private static InstanceManager _manager = null;

	private static GlobalHotkeyListener _hotkeys = null;

	private static readonly TaskCompletionSource _exitSignal = new TaskCompletionSource();

	private static bool _isFirstRunSetupMode = false;

	private static async Task Main(string[] args)
	{
		Console.Title = "Telegram Ad Bot - Professional v" + VersionInfo.CurrentString;
		if (Enumerable.Contains(args, "--update"))
		{
			await RunAsUpdaterAsync(args);
			return;
		}
		AppDataPaths.EnsureDirectories();
		Logger.InitFileLogging();
		if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELEGRAM_CONTROL_BOT_TOKEN")))
		{
			await RunIntegratedTelegramControlAsync();
			return;
		}
		Logger.Info("Application starting — v" + VersionInfo.CurrentString);
		if (!CheckChromiumExists())
		{
			Logger.Warning("Chromium not found. Installing...");
			if (!(await InstallChromiumAsync()))
			{
				Logger.Error("Cannot continue without Chromium browser.");
				Console.WriteLine("\n Press any key to exit...");
				Console.ReadKey();
				Logger.CloseFileLogging();
				return;
			}
		}
		UpdateInfo update = await UpdateChecker.CheckAsync();
		if (update != null && await Updater.PerformUpdateAsync(update))
		{
			Logger.Info("Exiting so updater can replace this exe...");
			Logger.CloseFileLogging();
			Environment.Exit(0);
			return;
		}
		_config.BotList = BotListLoader.Load();
		if (_config.BotList.Count == 0)
		{
			Logger.Error("No bots. See: " + AppDataPaths.BotsFile);
			Console.WriteLine("\n Press any key to exit...");
			Console.ReadKey();
			Logger.CloseFileLogging();
			return;
		}
                ConfigureRunSelection();


		bool positionsLoaded = ConfigStorage.LoadPositions(_config);
		_config.SetupDone = ConfigStorage.IsSetupDone();
		bool fullyConfigured = _config.SetupDone && positionsLoaded && _config.AllSet;
		_isFirstRunSetupMode = !fullyConfigured;
		_manager = new InstanceManager(_config);
		_manager.OnAllFinished += delegate
		{
			_exitSignal.TrySetResult();
		};
		RegisterGlobalHotkeys();
		ConsoleUI.PrintMainBanner(_config.BotList.Count, _config.InstanceCount, _config.CyclesPerBot, fullyConfigured);
		if (!_isFirstRunSetupMode)
		{
			await NormalRunFlowAsync();
		}
		else
		{
			await FirstTimeSetupFlowAsync();
		}
		await _exitSignal.Task;
		Logger.Info("Shutting down...");
		_hotkeys?.Dispose();
		Logger.CloseFileLogging();
		await Task.Delay(200);
		Environment.Exit(0);
	}
                private static void ConfigureRunSelection()
        {
                Console.Clear();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("============================================================");
                Console.WriteLine("              TELEGRAM AD BOT - RUN SETUP");
                Console.WriteLine("============================================================");
                Console.ResetColor();

                Console.WriteLine();
                Console.WriteLine("AVAILABLE BOTS");
                Console.WriteLine("------------------------------------------------------------");

                for (int i = 0; i < _config.BotList.Count; i++)
                {
                        Console.WriteLine($"[{i + 1}] @{_config.BotList[i]}");
                }

                Console.WriteLine();
                Console.WriteLine("Select bots (example: 1,3,5 or all):");

                while (true)
                {
                        Console.Write("> ");
                        string input = Console.ReadLine()?.Trim() ?? "";

                        if (string.Equals(input, "all", StringComparison.OrdinalIgnoreCase))
                                break;

                        string[] parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

                        var selected = new System.Collections.Generic.List<string>();
                        bool valid = parts.Length > 0;

                        foreach (string part in parts)
                        {
                                if (!int.TryParse(part.Trim(), out int number) ||
                                    number < 1 || number > _config.BotList.Count)
                                {
                                        valid = false;
                                        break;
                                }

                                string bot = _config.BotList[number - 1];

                                if (!selected.Contains(bot))
                                        selected.Add(bot);
                        }

                        if (valid && selected.Count > 0)
                        {
                                _config.BotList = selected;
                                break;
                        }

                        Console.WriteLine("Invalid selection. Please enter valid bot numbers.");
                }

                Console.WriteLine();
                Console.WriteLine("Number of Chromium browsers:");

                while (true)
                {
                        Console.Write("> ");
                        string input = Console.ReadLine()?.Trim() ?? "";

                        if (int.TryParse(input, out int instances) && instances > 0)
                        {
                                _config.InstanceCount = instances;
                                break;
                        }

                        Console.WriteLine("Please enter a number greater than 0.");
                }

                Console.WriteLine();
                Console.WriteLine("How many cycles per bot?");

                while (true)
                {
                        Console.Write("> ");
                        string input = Console.ReadLine()?.Trim() ?? "";

                        if (int.TryParse(input, out int cycles) && cycles > 0)
                        {
                                _config.CyclesPerBot = cycles;
                                break;
                        }

                        Console.WriteLine("Please enter a number greater than 0.");
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("============================================================");
                Console.WriteLine("                 SELECTED CONFIGURATION");
                Console.WriteLine("============================================================");
                Console.WriteLine($"Bots selected : {_config.BotList.Count}");
                Console.WriteLine($"Chromium      : {_config.InstanceCount}");
                Console.WriteLine($"Cycles/bot    : {_config.CyclesPerBot}");
                Console.WriteLine("============================================================");
                Console.ResetColor();

                Console.WriteLine();
                Console.Write("Press ENTER to start...");
                Console.ReadLine();
                Console.WriteLine();
        }

	private static async Task RunIntegratedTelegramControlAsync()
	{
		string? token = Environment.GetEnvironmentVariable("TELEGRAM_CONTROL_BOT_TOKEN");
		if (!long.TryParse(Environment.GetEnvironmentVariable("ADMIN_TELEGRAM_ID"), out long adminId))
		{
			Logger.Error("ADMIN_TELEGRAM_ID must be a numeric Telegram user ID.");
			Logger.CloseFileLogging();
			return;
		}
		ConfigStorage.LoadPositions(_config);
		_config.SetupDone = ConfigStorage.IsSetupDone();
		var controller = new PromotionController(_config);
		using var bot = new TelegramControlBot(controller, token!, adminId);
		bot.Start();
		Logger.Info("[CONTROL] Integrated control bot ready; awaiting Telegram commands.");
		var shutdown = new TaskCompletionSource();
		Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.TrySetResult(); };
		await shutdown.Task;
		await controller.StopAsync();
		Logger.CloseFileLogging();
	}

	private static bool CheckChromiumExists()
	{
		return PlaywrightHelper.FindChromiumPath() != null;
	}

	private static async Task<bool> InstallChromiumAsync()
	{
		return await PlaywrightHelper.EnsureChromiumInstalledAsync();
	}

	private static async Task RunAsUpdaterAsync(string[] args)
	{
		try
		{
			string target = "";
			int pid = 0;
			foreach (string arg in args)
			{
				if (arg.StartsWith("--target="))
				{
					target = arg.Substring("--target=".Length).Trim('"');
				}
				else if (arg.StartsWith("--pid="))
				{
					int.TryParse(arg.Substring("--pid=".Length), out pid);
				}
			}
			if (string.IsNullOrEmpty(target) || pid == 0)
			{
				Console.WriteLine("Invalid updater args.");
				await Task.Delay(3000);
				return;
			}
			await Updater.RunUpdaterModeAsync(target, pid);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Fatal updater error: " + ex.Message);
			await Task.Delay(5000);
		}
	}

	private static async Task FirstTimeSetupFlowAsync()
	{
		Logger.Warning("═══ FIRST-TIME SETUP MODE ═══");
		Logger.Info("Opening ONLY 1 browser for setup...");
		Logger.Info("Other browsers will open after setup is complete.");
		await _manager.OpenFirstInstanceOnlyAsync();
		ConsoleUI.PrintFirstRunInstructions();
		Logger.Warning("Waiting for you to pick positions and press Ctrl+Shift+M...");
	}

	private static async Task NormalRunFlowAsync()
	{
		Logger.Success("Setup previously completed. Full launch mode.");
		Console.WriteLine();
		await _manager.OpenAllAsync();
		ConsoleUI.PrintReadyBanner();
                 _ = Task.Run(TerminalCommandLoopAsync);
		if (_config.AutoStartLoop)
		{
			Logger.Success("Auto-starting loops in 3 seconds...");
			await Task.Delay(3000);
			Task.Run(async delegate
			{
				await _manager.StartAllAsync();
			});
		}
	}

                private static async Task TerminalCommandLoopAsync()
        {
                Console.WriteLine();
                Console.WriteLine("══════════════════════════════════════════════════════════════");
                Console.WriteLine("TERMINAL CONTROLS");
                Console.WriteLine("pause  = Pause all");
                Console.WriteLine("resume = Resume/start all");
                Console.WriteLine("stop   = Stop all");
                Console.WriteLine("status = Show status");
                Console.WriteLine("exit   = Exit application");
                Console.WriteLine("══════════════════════════════════════════════════════════════");

                while (!_exitSignal.Task.IsCompleted)
                {
                        try
                        {
                                Console.Write("Command: ");
                                string command = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";

                                if (string.IsNullOrEmpty(command))
                                        continue;

                                switch (command)
                                {
                                        case "pause":
                                        case "p":
                                                Logger.Info("[CMD] PAUSE");
                                                _manager.PauseAll();
                                                Console.WriteLine("[OK] All instances paused.");
                                                break;

                                        case "resume":
                                        case "start":
                                        case "r":
                                                Logger.Info("[CMD] RESUME");
                                                await _manager.StartAllAsync();
                                                Console.WriteLine("[OK] All instances resumed.");
                                                break;

                                        case "stop":
                                        case "s":
                                                Logger.Info("[CMD] STOP");
                                                await _manager.StopAllAsync();
                                                Console.WriteLine("[OK] All instances stopped.");
                                                break;

                                        case "status":
                                        case "st":
                                                Console.WriteLine();
                                                Console.WriteLine("STATUS");
                                                Console.WriteLine($"Bots       : {_config.BotList.Count}");
                                                Console.WriteLine($"Instances  : {_config.InstanceCount}");
                                                Console.WriteLine($"Cycles/bot : {_config.CyclesPerBot}");
                                                Console.WriteLine();
                                                break;

                                        case "exit":
                                        case "quit":
                                        case "q":
                                                Logger.Info("[CMD] EXIT");
                                                await _manager.StopAllAsync();
                                                _exitSignal.TrySetResult();
                                                break;

                                        default:
                                                Console.WriteLine("Unknown command. Use: pause, resume, stop, status, exit");
                                                break;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Error("[CMD] " + ex.Message);
                        }
                }
        }

 
	private static async Task HandleMarkSetupDoneAsync()
	{
		if (!_isFirstRunSetupMode)
		{
			Logger.Info("Setup already marked done. Ignoring.");
			return;
		}
		if (!_config.AllSet)
		{
			Logger.Error("Cannot mark done — some positions are missing!");
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("✗ Please pick ALL 4 positions first:");
			Console.WriteLine("Ctrl+Alt+E -> EMOJI BUTTON " + StatusMark(_config.EmojiButtonX));
			Console.WriteLine("Ctrl+Alt+F -> STICKER TAB   " + StatusMark(_config.StickerTabX));
			Console.WriteLine("Ctrl+Alt+S -> STICKER       " + StatusMark(_config.StickerX));
			Console.WriteLine("Ctrl+Alt+T -> TEXTBOX       " + StatusMark(_config.TextboxX));
			Console.ResetColor();
			Console.WriteLine();
			return;
		}
		ConfigStorage.MarkSetupDone();
		_config.SetupDone = true;
		Logger.Success("═══ SETUP COMPLETE ═══");
		Logger.Info("Closing setup browser...");
		await _manager.CloseFirstInstanceAsync();
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine("╔══════════════════════════════════════════════════════╗");
		Console.WriteLine("║     SETUP COMPLETED SUCCESSFULLY!                    ║");
		Console.WriteLine("║                                                      ║");
		Console.WriteLine("║ Restarting application in 3 seconds...               ║");
		Console.WriteLine("║ All browsers will open + auto-start loops.           ║");
		Console.WriteLine("║                                                      ║");
		Console.WriteLine("╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		for (int i = 3; i > 0; i--)
		{
			Logger.Info($"Restarting in {i}...");
			await Task.Delay(1000);
		}
		RestartApplication();
	}

	private static string StatusMark(int val)
	{
		if (val <= 0)
		{
			return "✗ (not set)";
		}
		return "✓ (set)";
	}

	private static void RestartApplication()
	{
		try
		{
			string exePath = Process.GetCurrentProcess().MainModule?.FileName;
			if (!string.IsNullOrEmpty(exePath))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = exePath,
					UseShellExecute = true
				});
			}
		}
		catch (Exception ex)
		{
			Logger.Error("Restart failed: " + ex.Message);
		}
		_hotkeys?.Dispose();
		Logger.CloseFileLogging();
		Environment.Exit(0);
	}

	private static void RegisterGlobalHotkeys()
	{
		_hotkeys = new GlobalHotkeyListener();
		uint CS = 6u;
		uint CA = 3u;
		_hotkeys.Register(CS, 71u, "Ctrl+Shift+G (Start)", delegate
		{
			Logger.Info("[Hotkey] START");
			Task.Run(async delegate
			{
				await _manager.StartAllAsync();
			});
		});
		_hotkeys.Register(CS, 80u, "Ctrl+Shift+P (Pause)", delegate
		{
			Logger.Info("[Hotkey] PAUSE");
			_manager.PauseAll();
		});
		_hotkeys.Register(CS, 75u, "Ctrl+Shift+K (Kill)", delegate
		{
			Logger.Info("[Hotkey] STOP");
			Task.Run(async delegate
			{
				await _manager.StopAllAsync();
			});
		});
		_hotkeys.Register(CS, 90u, "Ctrl+Shift+Z (Exit)", delegate
		{
			Logger.Info("[Hotkey] EXIT");
			Task.Run(async delegate
			{
				await _manager.StopAllAsync();
				_exitSignal.TrySetResult();
			});
		});
		_hotkeys.Register(CS, 77u, "Ctrl+Shift+M (Setup done)", delegate
		{
			Logger.Info("[Hotkey] SETUP DONE");
			Task.Run(async delegate
			{
				await HandleMarkSetupDoneAsync();
			});
		});
		_hotkeys.Register(CA, 69u, "Ctrl+Alt+E (Emoji btn)", delegate
		{
			Logger.Info("[Hotkey] PICK EMOJI BUTTON");
			Task.Run(async delegate
			{
				await _manager.PickAsync("EMOJI BUTTON", delegate(int x, int y)
				{
					_config.EmojiButtonX = x;
					_config.EmojiButtonY = y;
					ConfigStorage.SavePositions(_config);
				});
			});
		});
		_hotkeys.Register(CA, 70u, "Ctrl+Alt+F (Sticker tab)", delegate
		{
			Logger.Info("[Hotkey] PICK STICKER TAB");
			Task.Run(async delegate
			{
				await _manager.PickAsync("STICKER TAB", delegate(int x, int y)
				{
					_config.StickerTabX = x;
					_config.StickerTabY = y;
					ConfigStorage.SavePositions(_config);
				});
			});
		});
		_hotkeys.Register(CA, 84u, "Ctrl+Alt+T (Textbox)", delegate
		{
			Logger.Info("[Hotkey] PICK TEXTBOX");
			Task.Run(async delegate
			{
				await _manager.PickAsync("TEXTBOX", delegate(int x, int y)
				{
					_config.TextboxX = x;
					_config.TextboxY = y;
					ConfigStorage.SavePositions(_config);
				});
			});
		});
		_hotkeys.Register(CA, 83u, "Ctrl+Alt+S (Sticker)", delegate
		{
			Logger.Info("[Hotkey] PICK STICKER");
			Task.Run(async delegate
			{
				await _manager.PickAsync("STICKER", delegate(int x, int y)
				{
					_config.StickerX = x;
					_config.StickerY = y;
					ConfigStorage.SavePositions(_config);
				});
			});
		});
		_hotkeys.Register(CA, 49u, "Ctrl+Alt+1 (Account 1)", delegate
		{
			Task.Run(async delegate
			{
				await _manager.PickAsync("ACCOUNT 1", delegate(int x, int y)
				{
					_config.Account1X = x;
					_config.Account1Y = y;
					ConfigStorage.SavePositions(_config);
					PrintAccountCoordinates();
				});
			});
		});
		_hotkeys.Register(CA, 50u, "Ctrl+Alt+2 (Account 2)", delegate
		{
			Task.Run(async delegate
			{
				await _manager.PickAsync("ACCOUNT 2", delegate(int x, int y)
				{
					_config.Account2X = x;
					_config.Account2Y = y;
					ConfigStorage.SavePositions(_config);
					PrintAccountCoordinates();
				});
			});
		});
		_hotkeys.Register(CA, 51u, "Ctrl+Alt+3 (Account 3)", delegate
		{
			Task.Run(async delegate
			{
				await _manager.PickAsync("ACCOUNT 3", delegate(int x, int y)
				{
					_config.Account3X = x;
					_config.Account3Y = y;
					ConfigStorage.SavePositions(_config);
					PrintAccountCoordinates();
				});
			});
		});
		_hotkeys.Start();
	}

	private static void PrintAccountCoordinates()
	{
		Logger.Info($"Account 1: {_config.Account1X},{_config.Account1Y}");
		Logger.Info($"Account 2: {_config.Account2X},{_config.Account2Y}");
		Logger.Info($"Account 3: {_config.Account3X},{_config.Account3Y}");
		Console.WriteLine($"Account 1: {_config.Account1X},{_config.Account1Y}");
		Console.WriteLine($"Account 2: {_config.Account2X},{_config.Account2Y}");
		Console.WriteLine($"Account 3: {_config.Account3X},{_config.Account3Y}");
	}
}
