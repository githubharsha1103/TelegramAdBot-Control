using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TelegramAdBot.Control;

public sealed record TelegramControlChatState(
    int? PrimaryMessageId = null,
    DateTimeOffset? RateLimitedUntil = null,
    bool LegacyReplyKeyboardRemoved = false);

/// <summary>Persists Telegram message IDs and server cooldowns across process restarts.</summary>
public sealed class TelegramControlStateStore
{
    private readonly object _sync = new();
    private readonly string _path;
    private readonly Action<string>? _log;
    private readonly Dictionary<long, TelegramControlChatState> _chats;

    public TelegramControlStateStore(string path, Action<string>? log = null)
    {
        _path = path;
        _log = log;
        try
        {
            _chats = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<long, TelegramControlChatState>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _chats = new();
            _log?.Invoke("Could not load Telegram control state: " + ex.Message);
        }
    }

    public IReadOnlyDictionary<long, TelegramControlChatState> Snapshot()
    {
        lock (_sync) return new Dictionary<long, TelegramControlChatState>(_chats);
    }

    public void SetPrimaryMessage(long chatId, int messageId) => Update(chatId, state => state with { PrimaryMessageId = messageId });
    public void SetRateLimitedUntil(long chatId, DateTimeOffset until) => Update(chatId, state => state with
    {
        RateLimitedUntil = state.RateLimitedUntil is DateTimeOffset existing && existing > until ? existing : until
    });
    public void SetLegacyReplyKeyboardRemoved(long chatId) => Update(chatId, state => state with { LegacyReplyKeyboardRemoved = true });

    private void Update(long chatId, Func<TelegramControlChatState, TelegramControlChatState> update)
    {
        lock (_sync)
        {
            _chats.TryGetValue(chatId, out TelegramControlChatState? current);
            _chats[chatId] = update(current ?? new TelegramControlChatState());
            try
            {
                string directory = Path.GetDirectoryName(_path) ?? ".";
                Directory.CreateDirectory(directory);
                string temporaryPath = _path + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_chats));
                File.Move(temporaryPath, _path, overwrite: true);
            }
            catch (Exception ex) { _log?.Invoke("Could not persist Telegram control state: " + ex.Message); }
        }
    }
}
