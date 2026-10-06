using System;
using System.Linq;
using System.Reflection;

namespace TelegramAdBot.Helpers;

public static class VersionInfo
{
	public static Version Current
	{
		get
		{
			try
			{
				return (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())?.GetName().Version ?? new Version(1, 1, 0, 0);
			}
			catch
			{
				return new Version(1, 1, 0, 0);
			}
		}
	}

	public static string CurrentString => $"v{Current}";

	public static Version? Parse(string tag)
	{
		try
		{
			string clean = tag.Trim().ToLower().Replace("v", "");
			for (int dotCount = clean.Count((char c) => c == '.'); dotCount < 3; dotCount++)
			{
				clean += ".0";
			}
			return Version.Parse(clean);
		}
		catch
		{
			return null;
		}
	}
}
