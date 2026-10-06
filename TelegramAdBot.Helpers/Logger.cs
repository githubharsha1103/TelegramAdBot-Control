using System;
using System.IO;

namespace TelegramAdBot.Helpers;

public static class Logger
{
	private static readonly object _lock = new object();

	private static StreamWriter? _fileWriter;

	private static bool _fileReady = false;

	public static void InitFileLogging()
	{
		try
		{
			AppDataPaths.EnsureDirectories();
			string fileName = $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
			string filePath = Path.Combine(AppDataPaths.LogsFolder, fileName);
			_fileWriter = new StreamWriter(filePath, append: false)
			{
				AutoFlush = true
			};
			_fileReady = true;
			Info("Log file: " + filePath);
		}
		catch (Exception ex)
		{
			Console.WriteLine("[WRN] Could not init log file: " + ex.Message);
		}
	}

	public static void CloseFileLogging()
	{
		lock (_lock)
		{
			_fileWriter?.Flush();
			_fileWriter?.Close();
			_fileWriter = null;
			_fileReady = false;
		}
	}

	public static void Info(string m, int? instance = null)
	{
		Write(m, ConsoleColor.White, "INF", instance);
	}

	public static void Success(string m, int? instance = null)
	{
		Write(m, ConsoleColor.Green, "OK ", instance);
	}

	public static void Warning(string m, int? instance = null)
	{
		Write(m, ConsoleColor.DarkYellow, "WRN", instance);
	}

	public static void Error(string m, int? instance = null)
	{
		Write(m, ConsoleColor.Red, "ERR", instance);
	}

	public static void Step(string m, int? instance = null)
	{
		Write(m, ConsoleColor.Cyan, ">>>", instance);
	}

	public static void Cycle(int totalCycle, int? instance, int cycleOnBot, int cyclesPerBot, string botUsername, int botNumber, int totalBots)
	{
		lock (_lock)
		{
			string inst = (instance.HasValue ? $"[BOT{instance}] " : "");
			string line = $"  ┌── {inst}CYCLE {cycleOnBot}/{cyclesPerBot} │ @{botUsername} ({botNumber}/{totalBots}) │ Total #{totalCycle}";
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.DarkCyan;
			Console.WriteLine(line);
			Console.ResetColor();
			WriteToFile("\n" + line);
		}
	}

	public static void BotSwitch(string fromBot, string toBot, int botNumber, int totalBots, int? instance = null)
	{
		lock (_lock)
		{
			string inst = (instance.HasValue ? $"[BOT{instance}] " : "");
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("  ╔═══ " + inst + "BOT SWITCH ══════════════════════");
			Console.WriteLine("  ║   FROM : @" + fromBot);
			Console.WriteLine($"  ║   TO   : @{toBot} ({botNumber}/{totalBots})");
			Console.WriteLine("  ╚═══════════════════════════════════════════");
			Console.ResetColor();
			WriteToFile($"\n  BOT SWITCH: @{fromBot} → @{toBot} ({botNumber}/{totalBots})");
		}
	}

	public static void AllDone()
	{
		lock (_lock)
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("  ╔══════════════════════════════════════╗");
			Console.WriteLine("  ║   ✓ ALL BOTS COMPLETED!              ║");
			Console.WriteLine("  ║   All browsers closed.               ║");
			Console.WriteLine("  ║   Exiting in 3 seconds...            ║");
			Console.WriteLine("  ╚══════════════════════════════════════╝");
			Console.ResetColor();
			WriteToFile("\n  ALL BOTS COMPLETED.");
		}
	}

	private static void Write(string m, ConsoleColor c, string tag, int? instance)
	{
		lock (_lock)
		{
			string time = DateTime.Now.ToString("HH:mm:ss");
			string inst = (instance.HasValue ? $"[BOT{instance.Value}] " : "");
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.Write("[" + time + "] ");
			if (instance.HasValue)
			{
				Console.ForegroundColor = GetInstanceColor(instance.Value);
				Console.Write($"[BOT{instance.Value}] ");
			}
			Console.ForegroundColor = c;
			Console.Write("[" + tag + "] ");
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine(m);
			Console.ResetColor();
			WriteToFile($"[{time}] {inst}[{tag}] {m}");
		}
	}

	private static void WriteToFile(string line)
	{
		if (!_fileReady || _fileWriter == null)
		{
			return;
		}
		try
		{
			_fileWriter.WriteLine(line);
		}
		catch
		{
		}
	}

	private static ConsoleColor GetInstanceColor(int instance)
	{
		return instance switch
		{
			1 => ConsoleColor.Magenta, 
			2 => ConsoleColor.Yellow, 
			3 => ConsoleColor.Cyan, 
			4 => ConsoleColor.Green, 
			5 => ConsoleColor.Blue, 
			_ => ConsoleColor.Gray, 
		};
	}
}
