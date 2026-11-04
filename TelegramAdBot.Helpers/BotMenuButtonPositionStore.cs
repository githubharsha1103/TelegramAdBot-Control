using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TelegramAdBot.Models;

namespace TelegramAdBot.Helpers;

public sealed class BotMenuButtonPositionStore
{
	private static readonly object Gate = new();
	private readonly string _path;

	public BotMenuButtonPositionStore(string? path = null) => _path = path ?? AppDataPaths.BotMenuButtonPositionsFile;

	public BotMenuButtonPosition? Get(string botIdentifier)
	{
		string bot = Normalize(botIdentifier);
		if (bot.Length == 0) return null;
		lock (Gate)
		{
			try { return Load().FirstOrDefault(position => string.Equals(position.BotIdentifier, bot, StringComparison.OrdinalIgnoreCase)); }
			catch (Exception ex) { Logger.Error("Failed to load per-bot menu positions: " + ex.Message); return null; }
		}
	}

	public bool Save(BotMenuButtonPosition position)
	{
		string bot = Normalize(position.BotIdentifier);
		if (bot.Length == 0 || position.X < 0 || position.Y < 0 || position.ViewportWidth <= 0 || position.ViewportHeight <= 0 || position.X >= position.ViewportWidth || position.Y >= position.ViewportHeight) return false;
		lock (Gate)
		{
			try
			{
				List<BotMenuButtonPosition> entries = Load();
				entries.RemoveAll(existing => string.Equals(existing.BotIdentifier, bot, StringComparison.OrdinalIgnoreCase));
				entries.Add(new BotMenuButtonPosition
				{
					BotIdentifier = bot,
					X = position.X,
					Y = position.Y,
					ViewportWidth = position.ViewportWidth,
					ViewportHeight = position.ViewportHeight,
					Configured = true
				});
				AppDataPaths.EnsureDirectories();
				string temp = _path + ".tmp";
				File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
				File.Move(temp, _path, overwrite: true);
				return true;
			}
			catch (Exception ex) { Logger.Error("Failed to save per-bot menu positions: " + ex.Message); return false; }
		}
	}

	public static string Normalize(string botIdentifier) => (botIdentifier ?? "").Trim().TrimStart('@');

	private List<BotMenuButtonPosition> Load()
	{
		if (!File.Exists(_path)) return new List<BotMenuButtonPosition>();
		return JsonSerializer.Deserialize<List<BotMenuButtonPosition>>(File.ReadAllText(_path)) ?? new List<BotMenuButtonPosition>();
	}
}
