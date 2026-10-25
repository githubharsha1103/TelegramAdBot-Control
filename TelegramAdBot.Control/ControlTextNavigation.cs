using System;
using System.Collections.Generic;

namespace TelegramAdBot.Control;

/// <summary>Maps ordinary Telegram text and persistent-keyboard labels to screen names.</summary>
public static class ControlTextNavigation
{
    private static readonly IReadOnlyDictionary<string, string> Routes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["/start"] = "dashboard", ["/dashboard"] = "dashboard", ["/panel"] = "dashboard", ["dashboard"] = "dashboard", ["🏠 dashboard"] = "dashboard",
        ["/promotion"] = "promotion", ["promotion"] = "promotion", ["🚀 promotion"] = "promotion",
        ["/bots"] = "bots", ["bots"] = "bots", ["🤖 bots"] = "bots",
        ["/accounts"] = "accounts", ["accounts"] = "accounts", ["👥 accounts"] = "accounts",
        ["/instances"] = "instances", ["/instance"] = "instances", ["instances"] = "instances", ["🖥 instances"] = "instances",
        ["/activity"] = "activity", ["/status"] = "activity", ["activity"] = "activity", ["status"] = "activity", ["📊 activity"] = "activity",
        ["/settings"] = "settings", ["settings"] = "settings", ["⚙️ settings"] = "settings",
        ["/help"] = "help", ["help"] = "help", ["❓ help"] = "help"
    };

    public static string? Resolve(string text)
    {
        string normalized = string.Join(' ', text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.StartsWith("/start@", StringComparison.OrdinalIgnoreCase)) normalized = "/start";
        if (normalized.StartsWith("/panel@", StringComparison.OrdinalIgnoreCase)) normalized = "/panel";
        return Routes.TryGetValue(normalized, out string? screen) ? screen : null;
    }
}
