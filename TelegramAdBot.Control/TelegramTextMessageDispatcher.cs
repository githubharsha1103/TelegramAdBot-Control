using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramAdBot.Control;

public sealed record TelegramTextMessage(string Screen, string Text, ReplyKeyboardMarkup Keyboard);
public sealed record TelegramTextSendResult(bool Sent, int RetryAfterSeconds = 0);

/// <summary>One coalescing sender per chat. A 429 keeps only the newest pending screen until retry_after expires.</summary>
public sealed class TelegramTextMessageDispatcher
{
    private sealed class ChatState
    {
        public readonly object Sync = new();
        public TelegramTextMessage? Pending;
        public DateTimeOffset? CooldownUntil;
        public DateTimeOffset LastAttempt = DateTimeOffset.MinValue;
        public bool WorkerRunning;
        public CancellationToken Token;
    }

    private readonly ConcurrentDictionary<long, ChatState> _chats = new();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _persistedCooldowns;
    private readonly Func<long, TelegramTextMessage, CancellationToken, Task<TelegramTextSendResult>> _send;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Action<string>? _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _cooldownPath;
    private readonly object _persistSync = new();

    public TelegramTextMessageDispatcher(
        string cooldownPath,
        Func<long, TelegramTextMessage, CancellationToken, Task<TelegramTextSendResult>> send,
        Action<string>? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<DateTimeOffset>? now = null)
    {
        _cooldownPath = cooldownPath;
        _send = send;
        _log = log;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        _now = now ?? (() => DateTimeOffset.UtcNow);
        try
        {
            Dictionary<long, DateTimeOffset> stored = File.Exists(cooldownPath)
                ? JsonSerializer.Deserialize<Dictionary<long, DateTimeOffset>>(File.ReadAllText(cooldownPath)) ?? new()
                : new();
            _persistedCooldowns = new ConcurrentDictionary<long, DateTimeOffset>(stored);
        }
        catch (Exception ex)
        {
            _persistedCooldowns = new ConcurrentDictionary<long, DateTimeOffset>();
            _log?.Invoke("Could not load Telegram cooldown state: " + ex.Message);
        }
    }

    public void Enqueue(long chatId, TelegramTextMessage message, CancellationToken cancellationToken = default)
    {
        ChatState state = _chats.GetOrAdd(chatId, id => new ChatState
        {
            CooldownUntil = _persistedCooldowns.TryGetValue(id, out DateTimeOffset until) ? until : null
        });
        bool startWorker = false;
        lock (state.Sync)
        {
            state.Pending = message;
            state.Token = cancellationToken;
            if (!state.WorkerRunning)
            {
                state.WorkerRunning = true;
                startWorker = true;
            }
        }
        if (startWorker) _ = Task.Run(() => RunWorkerAsync(chatId, state, cancellationToken), CancellationToken.None);
    }

    private async Task RunWorkerAsync(long chatId, ChatState state, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TelegramTextMessage? message = null;
                TimeSpan wait = TimeSpan.Zero;
                lock (state.Sync)
                {
                    DateTimeOffset now = _now();
                    if (state.CooldownUntil is DateTimeOffset blocked && blocked > now) wait = blocked - now;
                    TimeSpan spacing = TimeSpan.FromSeconds(1) - (now - state.LastAttempt);
                    if (spacing > wait) wait = spacing;
                    if (wait <= TimeSpan.Zero && state.Pending != null)
                    {
                        message = state.Pending;
                        state.Pending = null;
                        state.LastAttempt = now;
                    }
                    else if (state.Pending == null)
                    {
                        state.WorkerRunning = false;
                        return;
                    }
                }

                if (wait > TimeSpan.Zero)
                {
                    await _delay(wait, ct);
                    continue;
                }
                if (message == null) continue;

                TelegramTextSendResult result = await _send(chatId, message, ct);
                if (result.RetryAfterSeconds > 0)
                {
                    DateTimeOffset until = _now().AddSeconds(result.RetryAfterSeconds);
                    lock (state.Sync)
                    {
                        if (state.CooldownUntil is not DateTimeOffset existing || existing < until)
                            state.CooldownUntil = until;
                        if (state.Pending == null) state.Pending = message;
                    }
                    PersistCooldown(chatId, until);
                    _log?.Invoke($"Telegram send rate limited chat={chatId} screen={message.Screen} retry_after={result.RetryAfterSeconds}s");
                    continue;
                }

                if (!result.Sent)
                    _log?.Invoke($"Telegram send failed chat={chatId} screen={message.Screen}; awaiting a newer request");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _log?.Invoke($"Telegram send worker failed chat={chatId}: {ex.Message}"); }
        finally
        {
            bool restart;
            CancellationToken latestToken;
            lock (state.Sync)
            {
                state.WorkerRunning = false;
                latestToken = state.Token;
                restart = state.Pending != null && !latestToken.IsCancellationRequested;
                if (restart) state.WorkerRunning = true;
            }
            if (restart) _ = Task.Run(() => RunWorkerAsync(chatId, state, latestToken), CancellationToken.None);
        }
    }

    private void PersistCooldown(long chatId, DateTimeOffset until)
    {
        _persistedCooldowns.AddOrUpdate(chatId, until, (_, previous) => previous > until ? previous : until);
        lock (_persistSync)
        {
            try
            {
                string directory = Path.GetDirectoryName(_cooldownPath) ?? ".";
                Directory.CreateDirectory(directory);
                string temporaryPath = _cooldownPath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_persistedCooldowns));
                File.Move(temporaryPath, _cooldownPath, overwrite: true);
            }
            catch (Exception ex) { _log?.Invoke("Could not persist Telegram cooldown: " + ex.Message); }
        }
    }
}
