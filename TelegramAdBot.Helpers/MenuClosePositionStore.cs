using System;
using System.IO;
using System.Text.Json;
using TelegramAdBot.Models;

namespace TelegramAdBot.Helpers;

public sealed class MenuClosePositionStore
{
    private readonly string _path;
    private readonly object _sync = new();
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public MenuClosePositionStore(string? path = null) => _path = path ?? AppDataPaths.GlobalMenuButtonPositionFile;

    public MenuClosePositions Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_path)) return new MenuClosePositions();
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_path));
                JsonElement root = document.RootElement;
                bool hasOut = TryGetPropertyIgnoreCase(root, "outOfChatMenuClosePosition", out JsonElement outElement);
                bool hasIn = TryGetPropertyIgnoreCase(root, "inChatMenuClosePosition", out JsonElement inElement);
                if (hasOut || hasIn)
                {
                    return new MenuClosePositions
                    {
                        OutOfChatMenuClosePosition = hasOut && outElement.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<MenuClosePosition>(outElement.GetRawText(), Options) ?? new MenuClosePosition() : new MenuClosePosition(),
                        InChatMenuClosePosition = hasIn && inElement.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<MenuClosePosition>(inElement.GetRawText(), Options) ?? new MenuClosePosition() : new MenuClosePosition()
                    };
                }
                // Upgrade the former single-position file as the in-chat position.
                MenuClosePosition? legacy = JsonSerializer.Deserialize<MenuClosePosition>(root.GetRawText(), Options);
                return legacy is { Configured: true } ? new MenuClosePositions { InChatMenuClosePosition = legacy } : new MenuClosePositions();
            }
            catch (Exception ex) { Logger.Error("Failed to load menu close positions: " + ex.Message); return new MenuClosePositions(); }
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
    public bool Save(MenuCaptureTarget target, MenuClosePosition position)
    {
        if (position is not { Configured: true }) return false;
        lock (_sync)
        {
            try
            {
                MenuClosePositions positions = Load();
                if (target == MenuCaptureTarget.OutOfChat) positions.OutOfChatMenuClosePosition = position;
                else positions.InChatMenuClosePosition = position;
                string? directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(positions, Options));
                File.Move(tempPath, _path, overwrite: true);
                return true;
            }
            catch (Exception ex) { Logger.Error("Failed to save menu close position: " + ex.Message); return false; }
        }
    }
}

public enum MenuCaptureTarget { OutOfChat, InChat }
