using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TelegramAdBot.Helpers;

public static class BotListLoader
{
	public static List<string> Load()
	{
		string path = AppDataPaths.BotsFile;
		if (!File.Exists(path))
		{
			Logger.Warning("bots.txt not found. Creating template...");
			AppDataPaths.EnsureDirectories();
			File.WriteAllText(path, "# Add one bot username per line\n# Lines starting with # are ignored\n# Example:\n# @mybot1\n# @mybot2\n");
			Logger.Error("Created bots.txt at:\n  " + path + "\n  Fill it with bot usernames and restart.");
			return new List<string>();
		}
		List<string> lines = (from l in File.ReadAllLines(path)
			select l.Trim() into l
			where !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")
			select (!l.StartsWith("@")) ? l : l.Substring(1)).Distinct().ToList();
		if (lines.Count == 0)
		{
			Logger.Error("bots.txt has no valid entries.");
			return new List<string>();
		}
		Logger.Success($"Loaded {lines.Count} bot(s):");
		for (int i = 0; i < lines.Count; i++)
		{
			Logger.Info($"  [{i + 1}] @{lines[i]}");
		}
		return lines;
	}
}
