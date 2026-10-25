using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramAdBot.Control;

public enum ControlMessageOperation { Applied, Missing, TransientFailure, PermanentFailure, RateLimited }
public sealed record ControlMessageResult(ControlMessageOperation Operation, int? MessageId = null, int RetryAfterSeconds = 0);
public sealed record ControlScreen(string Name, string Text, InlineKeyboardMarkup Keyboard, string RenderKey, long Revision);

/// <summary>Per-chat single-writer coordinator. Enqueues are cheap and overwrite older pending screens.</summary>
public sealed class ControlMessageCoordinator
{
    private sealed class ChatState
    {
        public readonly object Sync = new();
        public int? MessageId;
        public string RenderKey = "";
        public ControlScreen? Desired;
        public long Revision;
        public bool WorkerRunning;
        public CancellationToken DesiredToken;
    }

    private readonly ConcurrentDictionary<long, ChatState> _chats = new();
    private readonly Func<long, int, ControlScreen, CancellationToken, Task<ControlMessageResult>> _edit;
    private readonly Func<long, ControlScreen, CancellationToken, Task<ControlMessageResult>> _send;
    private readonly Action<string>? _log;
    private readonly Func<int, CancellationToken, Task> _delay;

    public ControlMessageCoordinator(
        Func<long, int, ControlScreen, CancellationToken, Task<ControlMessageResult>> edit,
        Func<long, ControlScreen, CancellationToken, Task<ControlMessageResult>> send,
        Action<string>? log = null,
        Func<int, CancellationToken, Task>? delay = null)
    {
        _edit = edit;
        _send = send;
        _log = log;
        _delay = delay ?? ((seconds, token) => Task.Delay(TimeSpan.FromSeconds(seconds), token));
    }

    public int? GetMessageId(long chatId) => _chats.TryGetValue(chatId, out var state) ? state.MessageId : null;
    public long? GetDesiredRevision(long chatId) => _chats.TryGetValue(chatId, out var state) ? state.Desired?.Revision : null;

    public void TrackMessage(long chatId, int messageId)
    {
        var state = _chats.GetOrAdd(chatId, _ => new ChatState());
        lock (state.Sync) state.MessageId = messageId;
    }

    public long Enqueue(long chatId, string screen, string text, InlineKeyboardMarkup keyboard, CancellationToken cancellationToken = default)
    {
        var state = _chats.GetOrAdd(chatId, _ => new ChatState());
        lock (state.Sync)
        {
            long revision = ++state.Revision;
            string key = text + "\n--KEYBOARD--\n" + string.Join("|", keyboard.InlineKeyboard.SelectMany(row => row).Select(button => button.CallbackData + "=" + button.Text));
            if (key == state.RenderKey && state.MessageId.HasValue && state.Desired == null) return revision;
            state.Desired = new ControlScreen(screen, text, keyboard, key, revision);
            state.DesiredToken = cancellationToken;
            _log?.Invoke($"render queued chat={chatId} screen={screen} revision={revision} message={state.MessageId?.ToString() ?? "new"}");
            if (!state.WorkerRunning)
            {
                state.WorkerRunning = true;
                _ = Task.Run(() => RunWorkerAsync(chatId, state, cancellationToken), CancellationToken.None);
            }
            return revision;
        }
    }

    private async Task RunWorkerAsync(long chatId, ChatState state, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ControlScreen? screen;
                int? messageId;
                lock (state.Sync) { screen = state.Desired; messageId = state.MessageId; }
                if (screen == null) return;

                ControlMessageResult result;
                if (!messageId.HasValue) result = await _send(chatId, screen, ct);
                else result = await _edit(chatId, messageId.Value, screen, ct);

                if (result.Operation == ControlMessageOperation.RateLimited)
                {
                    int delay = Math.Max(result.RetryAfterSeconds, 1);
                    _log?.Invoke($"render delayed chat={chatId} message={messageId} retry_after={delay}s revision={screen.Revision}");
                    await _delay(delay, ct);
                    continue;
                }
                if (result.Operation == ControlMessageOperation.Missing && messageId.HasValue)
                {
                    lock (state.Sync)
                    {
                        if (state.MessageId == messageId.Value) state.MessageId = null;
                        state.RenderKey = "";
                    }
                    continue;
                }
                if (result.Operation is ControlMessageOperation.Applied or ControlMessageOperation.Missing && result.MessageId.HasValue)
                {
                    lock (state.Sync) state.MessageId = result.MessageId.Value;
                }
                if (result.Operation is ControlMessageOperation.Applied or ControlMessageOperation.Missing)
                {
                    lock (state.Sync)
                    {
                        if (state.Desired?.Revision == screen.Revision) state.RenderKey = screen.RenderKey;
                        if (state.Desired?.Revision == screen.Revision) state.Desired = null;
                    }
                }
                else
                {
                    _log?.Invoke($"render failed chat={chatId} screen={screen.Name} revision={screen.Revision} result={result.Operation}");
                    lock (state.Sync)
                    {
                        if (state.Desired?.Revision == screen.Revision) state.Desired = null;
                        state.RenderKey = "";
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _log?.Invoke($"render worker exception chat={chatId}: {ex.Message}"); }
        finally
        {
            bool restart;
            lock (state.Sync)
            {
                state.WorkerRunning = false;
                restart = state.Desired != null && !state.DesiredToken.IsCancellationRequested;
                if (restart) state.WorkerRunning = true;
            }
            if (restart)
            {
                CancellationToken latestToken;
                lock (state.Sync) latestToken = state.DesiredToken;
                _ = Task.Run(() => RunWorkerAsync(chatId, state, latestToken), CancellationToken.None);
            }
        }
    }
}
