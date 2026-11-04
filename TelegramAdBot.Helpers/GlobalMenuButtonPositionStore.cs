using System;
using System.IO;
using System.Text.Json;
using TelegramAdBot.Models;

namespace TelegramAdBot.Helpers;

public sealed class GlobalMenuButtonPositionStore
{
    private readonly string _path;
    private readonly object _sync = new();

    public GlobalMenuButtonPositionStore(string? path = null) => _path = path ?? AppDataPaths.GlobalMenuButtonPositionFile;

    public GlobalMenuButtonPosition? Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var position = JsonSerializer.Deserialize<GlobalMenuButtonPosition>(File.ReadAllText(_path));
                return position is { Configured: true } ? position : null;
            }
            catch (Exception ex) { Logger.Error("Failed to load global menu button position: " + ex.Message); return null; }
        }
    }

    public bool Save(GlobalMenuButtonPosition position)
    {
        if (position is not { Configured: true }) return false;
        lock (_sync)
        {
            try
            {
                string? directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(_path, JsonSerializer.Serialize(position));
                return true;
            }
            catch (Exception ex) { Logger.Error("Failed to save global menu button position: " + ex.Message); return false; }
        }
    }
}
