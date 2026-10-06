using System;
using System.IO;

namespace TelegramAdBot.Helpers;

public static class ChromeFinder
{
	public static string? FindChromePath()
	{
		string[] array = new string[5]
		{
			"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
			"C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\Application\\chrome.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google\\Chrome\\Application\\chrome.exe"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google\\Chrome\\Application\\chrome.exe")
		};
		foreach (string path in array)
		{
			if (File.Exists(path))
			{
				return path;
			}
		}
		array = new string[2] { "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe", "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe" };
		foreach (string path2 in array)
		{
			if (File.Exists(path2))
			{
				Logger.Warning("Chrome not found. Using Microsoft Edge instead.");
				return path2;
			}
		}
		return null;
	}

	public static void PrintChromeMissingError()
	{
		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
		Console.WriteLine("  ║   ✗  GOOGLE CHROME NOT FOUND!                        ║");
		Console.WriteLine("  ╠══════════════════════════════════════════════════════╣");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   This app requires Google Chrome to be installed.  ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Please download and install Chrome from:          ║");
		Console.WriteLine("  ║   https://www.google.com/chrome/                    ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ║   Then run this app again.                          ║");
		Console.WriteLine("  ║                                                      ║");
		Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
		Console.ResetColor();
		Console.WriteLine();
	}
}
