using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramAdBot.Control;

// Telegram UI only. It directly calls PromotionController and never creates engine workers.
public sealed class TelegramControlBot : IDisposable
{
    private readonly PromotionController _controller;
    private readonly long _adminId;
    private readonly TelegramBotClient _bot;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<long, Session> _sessions = new();
    private readonly ConcurrentDictionary<long, int> _statusMessages = new();

    public TelegramControlBot(PromotionController controller, string token, long adminId)
    {
        _controller = controller;
        _adminId = adminId;
        _bot = new TelegramBotClient(token);
    }

    public void Start()
    {
        _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } }, _cts.Token);
        Helpers.Logger.Success("[CONTROL] Integrated Telegram control bot started.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        long? userId = update.Message?.From?.Id ?? update.CallbackQuery?.From.Id;
        long? chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
        if (userId != _adminId)
        {
            if (chatId.HasValue) await _bot.SendMessage(chatId.Value, "Unauthorized.", cancellationToken: ct);
            return;
        }
        if (!chatId.HasValue) return;
        if (update.CallbackQuery is { } callback)
        {
            await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            await CallbackAsync(chatId.Value, callback.Data ?? "", ct);
            return;
        }
        string text = update.Message?.Text?.Trim() ?? "";
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase)) { await BeginAsync(chatId.Value, ct); return; }
        if (text.StartsWith("/status", StringComparison.OrdinalIgnoreCase)) { await StatusAsync(chatId.Value, ct); return; }
        if (text.StartsWith("/pause", StringComparison.OrdinalIgnoreCase)) { await ResultAsync(chatId.Value, await _controller.PauseAsync(), ct); return; }
        if (text.StartsWith("/resume", StringComparison.OrdinalIgnoreCase)) { await ResultAsync(chatId.Value, await _controller.ResumeAsync(), ct); return; }
        if (text.StartsWith("/stop", StringComparison.OrdinalIgnoreCase)) { await ConfirmStopAsync(chatId.Value, ct); return; }
        if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase)) { await _bot.SendMessage(chatId.Value, "/start — configure\n/status — current status\n/pause, /resume, /stop", cancellationToken: ct); return; }
        await NumericAsync(chatId.Value, text, ct);
    }

    private async Task BeginAsync(long chatId, CancellationToken ct)
    {
        var bots = _controller.GetAvailableBots().ToList();
        if (bots.Count == 0) { await _bot.SendMessage(chatId, "No promotion bots are configured.", cancellationToken: ct); return; }
        _sessions[chatId] = new Session { AvailableBots = bots };
        await BotPickerAsync(chatId, ct);
    }

    private async Task CallbackAsync(long chatId, string data, CancellationToken ct)
    {
        if (data == "pause") { await ResultAsync(chatId, await _controller.PauseAsync(), ct); return; }
        if (data == "resume") { await ResultAsync(chatId, await _controller.ResumeAsync(), ct); return; }
        if (data == "refresh") { await StatusAsync(chatId, ct); return; }
        if (data == "stop:ask") { await ConfirmStopAsync(chatId, ct); return; }
        if (data == "stop:yes") { await ResultAsync(chatId, await _controller.StopAsync(), ct); return; }
        if (data == "stop:no") { await _bot.SendMessage(chatId, "Stop cancelled.", cancellationToken: ct); return; }
        if (data == "new") { await BeginAsync(chatId, ct); return; }
        if (!_sessions.TryGetValue(chatId, out var s)) { await _bot.SendMessage(chatId, "Use /start to begin configuration.", cancellationToken: ct); return; }
        if (data.StartsWith("bot:")) { string bot = data.Substring(4); if (!s.SelectedBots.Add(bot)) s.SelectedBots.Remove(bot); await BotPickerAsync(chatId, ct); return; }
        if (data == "all") { s.SelectedBots = s.AvailableBots.ToHashSet(); await BotPickerAsync(chatId, ct); return; }
        if (data == "clear") { s.SelectedBots.Clear(); await BotPickerAsync(chatId, ct); return; }
        if (data == "continue") { if (s.SelectedBots.Count == 0) { await _bot.SendMessage(chatId, "Select at least one bot.", cancellationToken: ct); return; } s.Step = Step.Instances; await InstancePickerAsync(chatId, ct); return; }
        if (data == "custom") { s.Step = Step.Instances; await _bot.SendMessage(chatId, "Send an instance count from 1 to 20.", cancellationToken: ct); return; }
        if (data.StartsWith("instances:")) { s.Instances = int.Parse(data.Substring(10)); s.Step = Step.Cycles; await _bot.SendMessage(chatId, "🔄 How many cycles should each selected bot run?", cancellationToken: ct); return; }
        if (data == "start") { await StartAsync(chatId, s, ct); return; }
        if (data == "edit") { s.Step = Step.Bots; await BotPickerAsync(chatId, ct); return; }
        if (data == "cancel") { _sessions.TryRemove(chatId, out _); await _bot.SendMessage(chatId, "Configuration cancelled.", cancellationToken: ct); }
    }

    private async Task NumericAsync(long chatId, string text, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(chatId, out var s) || !int.TryParse(text, out int n) || n < 1) return;
        if (s.Step == Step.Instances) { if (n > 20) { await _bot.SendMessage(chatId, "Maximum is 20.", cancellationToken: ct); return; } s.Instances = n; s.Step = Step.Cycles; await _bot.SendMessage(chatId, "🔄 How many cycles should each selected bot run?", cancellationToken: ct); return; }
        if (s.Step == Step.Cycles) { if (n > 100000) { await _bot.SendMessage(chatId, "Maximum is 100000.", cancellationToken: ct); return; } s.Cycles = n; s.Step = Step.Confirm; await ConfirmRunAsync(chatId, s, ct); }
    }

    private async Task BotPickerAsync(long chatId, CancellationToken ct)
    {
        var s = _sessions[chatId];
        var rows = s.AvailableBots.Select(x => new[] { InlineKeyboardButton.WithCallbackData((s.SelectedBots.Contains(x) ? "☑ " : "☐ ") + "@" + x, "bot:" + x) }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Select All", "all"), InlineKeyboardButton.WithCallbackData("Clear All", "clear") });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Continue", "continue") });
        await _bot.SendMessage(chatId, "🤖 Select the bots you want to promote", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    private Task InstancePickerAsync(long chatId, CancellationToken ct) => _bot.SendMessage(chatId, "🌐 How many Chromium instances do you want to run?", replyMarkup: new InlineKeyboardMarkup(new[] { new[] { 1, 2, 3, 4, 5 }.Select(x => InlineKeyboardButton.WithCallbackData(x.ToString(), "instances:" + x)).ToArray(), new[] { InlineKeyboardButton.WithCallbackData("Custom", "custom") } }), cancellationToken: ct);

    private Task ConfirmRunAsync(long chatId, Session s, CancellationToken ct) => _bot.SendMessage(chatId, $"🚀 PROMOTION CONFIGURATION\n\nBots:\n{string.Join("\n", s.SelectedBots.Select(x => "• @" + x))}\n\nChromium instances: {s.Instances}\nCycles per bot: {s.Cycles}\nTotal bots: {s.SelectedBots.Count}\nTotal planned bot-cycles per instance: {s.SelectedBots.Count * s.Cycles}", replyMarkup: new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("▶ START", "start"), InlineKeyboardButton.WithCallbackData("✏️ EDIT", "edit") }, new[] { InlineKeyboardButton.WithCallbackData("❌ CANCEL", "cancel") } }), cancellationToken: ct);

    private async Task StartAsync(long chatId, Session s, CancellationToken ct)
    {
        var configured = await _controller.ConfigureAsync(new PromotionRunConfiguration { SelectedBots = s.SelectedBots.ToList(), InstanceCount = s.Instances, CyclesPerBot = s.Cycles });
        if (!configured.Success) { await ResultAsync(chatId, configured, ct); return; }
        var result = await _controller.StartAsync();
        await ResultAsync(chatId, result, ct);
        if (!result.Success) return;
        _sessions.TryRemove(chatId, out _);
        var message = await _bot.SendMessage(chatId, Format(_controller.GetStatus()), replyMarkup: Keyboard(_controller.GetStatus()), cancellationToken: ct);
        _statusMessages[chatId] = message.MessageId;
        _ = RefreshAsync(chatId, message.MessageId, ct);
    }

    private async Task ResultAsync(long chatId, ControlResult result, CancellationToken ct)
    {
        await _bot.SendMessage(chatId, (result.Success ? "✅ " : "⚠️ ") + result.Message, cancellationToken: ct);
        await StatusAsync(chatId, ct);
    }
    private Task ConfirmStopAsync(long chatId, CancellationToken ct) => _bot.SendMessage(chatId, "Are you sure you want to stop the promotion?", replyMarkup: new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("YES, STOP", "stop:yes"), InlineKeyboardButton.WithCallbackData("CANCEL", "stop:no") } }), cancellationToken: ct);
    private async Task StatusAsync(long chatId, CancellationToken ct)
    {
        var status = _controller.GetStatus(); string text = Format(status);
        if (_statusMessages.TryGetValue(chatId, out int id)) try { await _bot.EditMessageText(chatId, id, text, replyMarkup: Keyboard(status), cancellationToken: ct); return; } catch { _statusMessages.TryRemove(chatId, out _); }
        var message = await _bot.SendMessage(chatId, text, replyMarkup: Keyboard(status), cancellationToken: ct); _statusMessages[chatId] = message.MessageId;
    }
    private async Task RefreshAsync(long chatId, int messageId, CancellationToken ct)
    {
        try { while (!ct.IsCancellationRequested && _statusMessages.TryGetValue(chatId, out int current) && current == messageId) { await Task.Delay(3000, ct); var s = _controller.GetStatus(); await _bot.EditMessageText(chatId, messageId, Format(s), replyMarkup: Keyboard(s), cancellationToken: ct); if (!s.IsRunning && !s.IsPaused && !s.IsStopping) break; } } catch (OperationCanceledException) { } catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Status update failed: " + ex.Message); }
    }
    private static InlineKeyboardMarkup Keyboard(PromotionStatus s) => !s.IsRunning && !s.IsPaused ? new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("▶ START NEW RUN", "new") } }) : new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData(s.IsPaused ? "▶ RESUME" : "⏸ PAUSE", s.IsPaused ? "resume" : "pause"), InlineKeyboardButton.WithCallbackData("⛔ STOP", "stop:ask"), InlineKeyboardButton.WithCallbackData("🔄 REFRESH", "refresh") } });
    private static string Format(PromotionStatus s)
    {
        string state = s.IsStopping ? "🛑 STOPPING" : s.IsPaused ? "⏸ PROMOTION PAUSED" : s.IsRunning ? "🚀 PROMOTION RUNNING" : "🛑 PROMOTION STOPPED";
        var b = new StringBuilder($"{state}\n\n🤖 Bots: {s.SelectedBots.Count}\n🌐 Instances: {s.InstanceCount}\n🔄 Progress: {s.CompletedCycles}/{s.TotalCycles}\n");
        foreach (var i in s.Instances) b.Append($"\nInstance {i.InstanceId}\nBot: @{i.Bot}\nCycle: {i.CurrentCycle}/{i.TotalCyclesForBot}\nStatus: {i.State}\n");
        return b.Append($"\n⏱ Runtime: {TimeSpan.FromSeconds(s.RuntimeSeconds):hh\\:mm\\:ss}\n⚡ Status: {(s.IsPaused ? "PAUSED" : s.IsRunning ? "RUNNING" : "STOPPED")}").ToString();
    }
    private static Task HandleErrorAsync(ITelegramBotClient _, Exception ex, HandleErrorSource __, CancellationToken ___) { Helpers.Logger.Warning("[CONTROL] Telegram error: " + ex.Message); return Task.CompletedTask; }
    public void Dispose() { _cts.Cancel(); _cts.Dispose(); }

    private sealed class Session { public List<string> AvailableBots { get; set; } = new(); public HashSet<string> SelectedBots { get; set; } = new(); public int Instances { get; set; } public int Cycles { get; set; } public Step Step { get; set; } }
    private enum Step { Bots, Instances, Cycles, Confirm }
}
