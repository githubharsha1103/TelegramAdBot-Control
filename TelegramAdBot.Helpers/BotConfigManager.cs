using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TelegramAdBot.Helpers;

/// <summary>Persistent promotion bot configuration, stored outside the application output directory.</summary>
public sealed class BotConfigManager
{
    private readonly object _gate = new();
    private readonly string _path;
    private List<string>? _bots;

    public BotConfigManager(string? path = null) => _path = path ?? AppDataPaths.BotConfigFile;

    public IReadOnlyList<string> GetBots()
    {
        lock (_gate) return EnsureLoaded().ToList();
    }

    public bool TryAdd(string username, out string normalized, out string error)
    {
        normalized = Normalize(username);
        error = "";
        if (normalized.Length == 0) { error = "Send a valid Telegram username (5-32 letters, digits, or underscores)."; return false; }
        lock (_gate)
        {
            var bots = EnsureLoaded();
            if (bots.Contains(normalized, StringComparer.OrdinalIgnoreCase)) { error = "That bot is already configured."; return false; }
            bots.Add(normalized);
            Save(bots);
            return true;
        }
    }

    public bool Remove(string username)
    {
        lock (_gate)
        {
            var bots = EnsureLoaded();
            int index = bots.FindIndex(x => string.Equals(x, username, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            bots.RemoveAt(index);
            Save(bots);
            return true;
        }
    }

    private List<string> EnsureLoaded()
    {
        if (_bots != null) return _bots;
        AppDataPaths.EnsureDirectories();
        if (File.Exists(_path))
        {
            var saved = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path)) ?? new List<string>();
            _bots = saved.Select(Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return _bots;
        }

        // One-time compatibility import. bots.txt remains untouched for rollback/manual recovery.
        _bots = File.Exists(AppDataPaths.BotsFile)
            ? File.ReadAllLines(AppDataPaths.BotsFile).Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith("#", StringComparison.Ordinal)).Select(Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        Save(_bots);
        return _bots;
    }

    private void Save(List<string> bots)
    {
        AppDataPaths.EnsureDirectories();
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(bots, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }

    private static string Normalize(string value)
    {
        string name = (value ?? "").Trim();
        if (name.StartsWith("@", StringComparison.Ordinal)) name = name.Substring(1);
        return name.Length is >= 5 and <= 32 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? name : "";
    }
}
