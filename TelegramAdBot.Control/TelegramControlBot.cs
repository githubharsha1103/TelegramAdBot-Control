using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TelegramAdBot.Core;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramAdBot.Control;

// Telegram UI only. It directly calls PromotionController and never creates engine workers.
public sealed class TelegramControlBot : IDisposable
{
    private const int InstancePageSize = 20;
    private readonly PromotionController _controller;

    private readonly long _adminId;
    private readonly TelegramBotClient _bot;
    private readonly HttpClient _apiHttpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<long, Session> _sessions = new();
    private readonly ConcurrentDictionary<long, int> _statusMessages = new();
    private readonly ConcurrentDictionary<long, int> _panelMessages = new();
    private readonly ConcurrentDictionary<long, int> _menuKeyboardMessages = new();
    private readonly ConcurrentDictionary<long, string> _panelCallbackTokens = new();
    private readonly ConcurrentDictionary<long, int> _selectionMessages = new();
    private readonly ConcurrentDictionary<long, int> _managementMessages = new();
    private readonly ConcurrentDictionary<long, int> _instanceMessages = new();
    private readonly ConcurrentDictionary<long, string> _instanceCallbackTokens = new();
    private readonly ConcurrentDictionary<long, HashSet<int>> _instanceSelections = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _chatLocks = new();
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _statusRefreshTokens = new();
    private readonly ConcurrentDictionary<long, string> _lastStatusRender = new();
    private readonly ConcurrentDictionary<long, string> _lastNotification = new();
    private readonly ConcurrentDictionary<long, byte> _stopConfirmations = new();
    private readonly ConcurrentDictionary<long, byte> _stopRequested = new();
    private readonly ConcurrentDictionary<long, string> _statusRefreshRunIds = new();
    private readonly ConcurrentDictionary<string, byte> _terminalNotifiedRuns = new();
    private readonly ConcurrentDictionary<long, string> _statusCallbackTokens = new();
    private readonly ConcurrentDictionary<long, string> _managementCallbackTokens = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _selectionLocks = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _statusLocks = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _managementLocks = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _instanceMessageLocks = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _messageApiLocks = new();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _lastMessageOperation = new();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _addBotPending = new();
    private readonly ConcurrentDictionary<long, string> _removeBotPending = new();

    public TelegramControlBot(PromotionController controller, string token, long adminId)
    {
        _controller = controller;
        _adminId = adminId;
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                IPAddress ipv4Address = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4Address == null)
                    throw new SocketException((int)SocketError.HostNotFound);

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(ipv4Address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        _apiHttpClient = new HttpClient(handler, disposeHandler: true);
        _bot = new TelegramBotClient(token, _apiHttpClient);
    }

    public void Start()
    {
        _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery } }, _cts.Token);
        Helpers.Logger.Success("[CONTROL] Integrated Telegram control bot started.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        try
        {
        long? userId = update.Message?.From?.Id ?? update.CallbackQuery?.From.Id;
        long? chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
        if (userId != _adminId)
        {
            if (chatId.HasValue) await SafeSendAsync(chatId.Value, "Unauthorized.", null, ct);
            return;
        }
        if (!chatId.HasValue) return;
        if (update.CallbackQuery is { } callback)
        {
            try { await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct); }
            catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Callback acknowledgement failed: " + ex.Message); }
            await CallbackAsync(chatId.Value, callback.Message?.MessageId, callback.Data ?? "", ct);
            return;
        }

        string text = update.Message?.Text?.Trim() ?? "";
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/start@", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/panel", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/panel@", StringComparison.OrdinalIgnoreCase))
        {
            _addBotPending.TryRemove(chatId.Value, out DateTimeOffset ignored);
            await MainMenuAsync(chatId.Value, ct);
            return;
        }
        if (text.EndsWith("Configure Promotion", StringComparison.OrdinalIgnoreCase)) { await BeginAsync(chatId.Value, ct); return; }
        if (text.Equals("Main Menu", StringComparison.OrdinalIgnoreCase)) { await MainMenuAsync(chatId.Value, ct); return; }
        if (text.EndsWith("Instances", StringComparison.OrdinalIgnoreCase)) { await InstanceMenuAsync(chatId.Value, ct); return; }
        if (text.EndsWith("Accounts", StringComparison.OrdinalIgnoreCase) || text.Equals("Manage Accounts", StringComparison.OrdinalIgnoreCase)) { await BeginAsync(chatId.Value, ct); return; }
        if (text.EndsWith("Bots", StringComparison.OrdinalIgnoreCase) || text.Equals("Manage Bots", StringComparison.OrdinalIgnoreCase)) { await ManageBotsAsync(chatId.Value, ct); return; }
        if (text.EndsWith("Start", StringComparison.OrdinalIgnoreCase)) { await DashboardStartAsync(chatId.Value, ct); return; }
        if (text.EndsWith("Pause / Resume", StringComparison.OrdinalIgnoreCase))
        {
            PromotionStatus current = _controller.GetStatus();
            await ResultAsync(chatId.Value, current.IsPaused ? await _controller.ResumeAsync() : await _controller.PauseAsync(), ct);
            return;
        }
        if (text.EndsWith("Stop Promotion", StringComparison.OrdinalIgnoreCase)) { await ConfirmStopAsync(chatId.Value, ct); return; }
        if (text.EndsWith("Status", StringComparison.OrdinalIgnoreCase)) { await StatusAsync(chatId.Value, ct); return; }
        if (_addBotPending.TryGetValue(chatId.Value, out var expires))
        {
            if (DateTimeOffset.UtcNow > expires)
            {
                _addBotPending.TryRemove(chatId.Value, out DateTimeOffset ignored);
                await EditManagementAsync(chatId.Value, "Add Bot request expired. Open Manage Bots to try again.", InlineKeyboardMarkup.Empty(), ct);
                return;
            }
            if (text.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                _addBotPending.TryRemove(chatId.Value, out DateTimeOffset ignored);
                await ManageBotsAsync(chatId.Value, ct);
                return;
            }
            if (text.StartsWith("/", StringComparison.Ordinal))
            {
                _addBotPending.TryRemove(chatId.Value, out DateTimeOffset ignored);
            }
            else
            {
                if (_controller.TryAddBot(text, out var added, out var addError))
                {
                    _addBotPending.TryRemove(chatId.Value, out DateTimeOffset ignored);
                    await ManageBotsAsync(chatId.Value, ct);
                }
                else await EditManagementAsync(chatId.Value, addError, InlineKeyboardMarkup.Empty(), ct);
                return;
            }
        }

        if (text.StartsWith("/status", StringComparison.OrdinalIgnoreCase)) { await StatusAsync(chatId.Value, ct); return; }
        if (text.Equals("/instance", StringComparison.OrdinalIgnoreCase)) { await InstanceMenuAsync(chatId.Value, ct); return; }
        if (text.StartsWith("/pause", StringComparison.OrdinalIgnoreCase)) { await ResultAsync(chatId.Value, await _controller.PauseAsync(), ct); return; }
        if (text.StartsWith("/resume", StringComparison.OrdinalIgnoreCase)) { await ResultAsync(chatId.Value, await _controller.ResumeAsync(), ct); return; }
        if (text.StartsWith("/stop", StringComparison.OrdinalIgnoreCase)) { await ConfirmStopAsync(chatId.Value, ct); return; }
        if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase)) { await SafeSendAsync(chatId.Value, "/start \u2014 configure\n/status \u2014 current status\n/instance \u2014 manage Chromium instances\n/pause, /resume, /stop", null, ct); return; }
        await NumericAsync(chatId.Value, text, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Update handling failed safely: " + ex.Message); }
    }

    private static ReplyKeyboardMarkup MainReplyKeyboard() => new(new[]
    {
        new[] { new KeyboardButton("⚙️ Configure Promotion"), new KeyboardButton("🖥 Instances") },
        new[] { new KeyboardButton("👤 Accounts"), new KeyboardButton("🤖 Bots") },
        new[] { new KeyboardButton("▶️ Start"), new KeyboardButton("⏸ Pause / Resume") },
        new[] { new KeyboardButton("🛑 Stop Promotion"), new KeyboardButton("📊 Status") },
        new[] { new KeyboardButton("Main Menu") }
    }) { ResizeKeyboard = true, IsPersistent = true };

    private async Task MainMenuAsync(long chatId, CancellationToken ct)
    {
        await RenderDashboardAsync(chatId, ct);
        await SendMenuKeyboardAsync(chatId, ct);
    }

    private async Task RenderDashboardAsync(long chatId, CancellationToken ct)
    {
        PromotionStatus status = _controller.GetStatus();
        string state = status.IsStopping ? "🔴 STOPPING" : status.IsPaused ? "🟡 PAUSED" : status.IsRunning ? "🔵 RUNNING" : status.StartTime.HasValue ? "🔴 STOPPED" : "🟢 IDLE";
        string instances = status.SelectedInstanceIds.Count == 0 ? "—" : string.Join(", ", status.SelectedInstanceIds);
        int openBrowsers = status.RunningInstanceIds.Concat(_controller.GetOpenManualInstanceIds()).Distinct().Count();
        var lines = new List<string>
        {
            "🚀 Promotion Dashboard", "", state, "",
            "Selected instances: " + instances,
            $"Cycles: {status.CompletedCycles}/{status.TotalCycles}",
            $"Open browsers: {openBrowsers}"
        };
        string error = !string.IsNullOrWhiteSpace(status.LastError) ? status.LastError :
            status.Instances.Select(instance => instance.Error).FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
        if (!string.IsNullOrWhiteSpace(error)) lines.Add("\n⚠️ " + error);

        string token = Guid.NewGuid().ToString("N").Substring(0, 8);
        _panelCallbackTokens[chatId] = token;
        InlineKeyboardMarkup keyboard = DashboardKeyboard(token);
        string text = string.Join("\n", lines);
        if (_panelMessages.TryGetValue(chatId, out int messageId))
        {
            EditResult edit = await EditMessageSafeAsync(chatId, messageId, text, keyboard, ct, "main-dashboard");
            if (edit == EditResult.Success) return;
            if (edit == EditResult.Failed) return;
            _panelMessages.TryRemove(chatId, out _);
        }
        Message sent = await SafeSendAsync(chatId, text, keyboard, ct, "main-dashboard");
        if (sent != null) _panelMessages[chatId] = sent.MessageId;
    }

    private static InlineKeyboardMarkup DashboardKeyboard(string token) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("⚙️ Configure", TaggedCallback("panel", token, "configure")), InlineKeyboardButton.WithCallbackData("▶️ Start", TaggedCallback("panel", token, "start")) },
        new[] { InlineKeyboardButton.WithCallbackData("⏸ Pause / Resume", TaggedCallback("panel", token, "pause")), InlineKeyboardButton.WithCallbackData("🛑 Stop", TaggedCallback("panel", token, "stop")) },
        new[] { InlineKeyboardButton.WithCallbackData("🖥 Instances", TaggedCallback("panel", token, "instances")), InlineKeyboardButton.WithCallbackData("👤 Accounts", TaggedCallback("panel", token, "accounts")), InlineKeyboardButton.WithCallbackData("🤖 Bots", TaggedCallback("panel", token, "bots")) },
        new[] { InlineKeyboardButton.WithCallbackData("📊 Status", TaggedCallback("panel", token, "status")), InlineKeyboardButton.WithCallbackData("🔄 Refresh Panel", TaggedCallback("panel", token, "refresh")) }
    });

    private async Task SendMenuKeyboardAsync(long chatId, CancellationToken ct)
    {
        var gate = _messageApiLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await WaitForMessageRateLimitAsync(chatId, ct);
            try
            {
                Message menuMessage = await _bot.SendMessage(chatId, "🎛 Menu", replyMarkup: MainReplyKeyboard(), cancellationToken: ct);
                if (_menuKeyboardMessages.TryGetValue(chatId, out int previousMenuMessageId))
                {
                    try { await _bot.DeleteMessage(chatId, previousMenuMessageId, ct); }
                    catch (Exception ex) { Helpers.Logger.Info("[CONTROL] Previous menu message cleanup skipped: " + ex.Message); }
                }
                _menuKeyboardMessages[chatId] = menuMessage.MessageId;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Reply keyboard send failed: " + ex.Message); }
            finally { _lastMessageOperation[chatId] = DateTimeOffset.UtcNow; }
        }
        finally { gate.Release(); }
    }

    private async Task BeginAsync(long chatId, CancellationToken ct)
    {
        var bots = _controller.GetAvailableBots().ToList();
        if (bots.Count == 0) { await SafeSendAsync(chatId, "No promotion bots are configured.", null, ct); return; }
        _sessions.TryGetValue(chatId, out var previous);
        var session = new Session { AvailableBots = bots, SelectionRevision = previous?.SelectionRevision ?? 0 };
        if (previous?.SelectionMessageId is int previousId) session.SelectionMessageId = previousId;
        else if (_selectionMessages.TryGetValue(chatId, out int storedId)) session.SelectionMessageId = storedId;
        _sessions[chatId] = session;
        await BotPickerAsync(chatId, ct);
    }

    private async Task CallbackAsync(long chatId, int? messageId, string data, CancellationToken ct)
    {
        var chatLock = _chatLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await chatLock.WaitAsync(ct);
        try { await CallbackCoreAsync(chatId, messageId, data, ct); }
        finally { chatLock.Release(); }
    }

    private async Task CallbackCoreAsync(long chatId, int? messageId, string data, CancellationToken ct)
    {
        if (TryReadTaggedCallback(data, "panel", out string panelToken, out string panelAction))
        {
            if (!_panelMessages.TryGetValue(chatId, out int activePanelId) || messageId != activePanelId ||
                !_panelCallbackTokens.TryGetValue(chatId, out string currentPanelToken) || currentPanelToken != panelToken) return;
            if (panelAction == "configure") { await BeginAsync(chatId, ct); return; }
            if (panelAction == "start") { await DashboardStartAsync(chatId, ct); return; }
            if (panelAction == "pause")
            {
                PromotionStatus current = _controller.GetStatus();
                await ResultAsync(chatId, current.IsPaused ? await _controller.ResumeAsync() : await _controller.PauseAsync(), ct);
                return;
            }
            if (panelAction == "stop") { await ConfirmStopAsync(chatId, ct); return; }
            if (panelAction == "instances") { await InstanceMenuAsync(chatId, ct); return; }
            if (panelAction == "accounts") { await BeginAsync(chatId, ct); return; }
            if (panelAction == "bots") { await ManageBotsAsync(chatId, ct); return; }
            if (panelAction == "status") { await StatusAsync(chatId, ct); return; }
            if (panelAction == "refresh") { await RenderDashboardAsync(chatId, ct); return; }
            return;
        }
        if (data.StartsWith("sel:", StringComparison.Ordinal))
        {
            if (!_sessions.TryGetValue(chatId, out var session) || !TryReadSelectionCallback(data, session, out string action)) return;
            int? activeSelectionId = session.SelectionMessageId;
            if (_selectionMessages.TryGetValue(chatId, out int storedSelectionId)) activeSelectionId = storedSelectionId;
            if (activeSelectionId.HasValue && messageId != activeSelectionId) return;
            if (!activeSelectionId.HasValue && messageId.HasValue)
            {
                activeSelectionId = messageId;
                session.SelectionMessageId = messageId;
                _selectionMessages[chatId] = messageId.Value;
            }
            if (!activeSelectionId.HasValue) return;

            if (action.StartsWith("bot:", StringComparison.Ordinal)) { string bot = action.Substring(4); if (!session.SelectedBots.Add(bot)) session.SelectedBots.Remove(bot); await BotPickerAsync(chatId, ct); return; }
            if (action == "all") { session.SelectedBots = session.AvailableBots.ToHashSet(); await BotPickerAsync(chatId, ct); return; }
            if (action == "clear") { session.SelectedBots.Clear(); await BotPickerAsync(chatId, ct); return; }
            if (action == "continue") { if (session.SelectedBots.Count == 0) { await BotPickerAsync(chatId, ct); return; } session.Step = Step.Instances; await InstancePickerAsync(chatId, ct); return; }
            if (action.StartsWith("instance:", StringComparison.Ordinal))
            {
                int[] configuredIds = _controller.GetConfiguredInstanceIds().ToArray();
                if (int.TryParse(action.Substring(9), out int instance) && Array.BinarySearch(configuredIds, instance) >= 0)
                {
                    if (!session.SelectedInstances.Add(instance)) session.SelectedInstances.Remove(instance);
                    await InstancePickerAsync(chatId, ct);
                }
                return;
            }
            if (action.StartsWith("instances:page:", StringComparison.Ordinal) && int.TryParse(action.Substring("instances:page:".Length), out int page))
            {
                int totalInstances = _controller.GetConfiguredInstanceIds().Count;
                int totalPages = (int)(((long)totalInstances + InstancePageSize - 1) / InstancePageSize);
                if (page >= 0 && page < totalPages) { session.InstancePage = page; await InstancePickerAsync(chatId, ct); }
                return;
            }
            if (action == "instances:continue") { if (session.SelectedInstances.Count == 0) { await InstancePickerAsync(chatId, ct, "Select at least one instance to continue."); return; } session.InstanceCount = session.SelectedInstances.Count; session.CurrentAccountInstance = session.SelectedInstances.Min(); foreach (int id in session.SelectedInstances) if (!session.AccountsByInstance.ContainsKey(id)) session.AccountsByInstance[id] = new HashSet<int>(); session.Step = Step.Accounts; await AccountPickerAsync(chatId, ct); return; }
            if (action.StartsWith("account:", StringComparison.Ordinal)) { if (int.TryParse(action.Substring(8), out int account) && account is >= 1 and <= 3) { var accounts = session.AccountsForCurrentInstance; if (!accounts.Add(account)) accounts.Remove(account); await AccountPickerAsync(chatId, ct); } return; }
            if (action == "accounts:continue") { if (session.AccountsForCurrentInstance.Count == 0) { await AccountPickerAsync(chatId, ct, "Select at least one account to continue."); return; } if (session.AdvanceAccountInstance()) await AccountPickerAsync(chatId, ct); else { session.Step = Step.Cycles; await CyclePromptAsync(chatId, ct); } return; }
            if (action == "back") { await BackAsync(chatId, session, ct); return; }
            if (action == "edit") { session.Step = Step.Bots; await BotPickerAsync(chatId, ct); return; }
            if (action == "cancel") { _sessions.TryRemove(chatId, out _); await EditSelectionAsync(chatId, session, "Setup cancelled.", _ => InlineKeyboardMarkup.Empty(), ct); return; }
            if (action == "start") { if (Interlocked.Exchange(ref session.StartInProgress, 1) != 0) return; await StartAsync(chatId, session, ct); return; }
            return;
        }

        if (TryReadTaggedCallback(data, "status", out string statusToken, out string statusAction))
        {
            if (!_statusMessages.TryGetValue(chatId, out int activeStatusId) || messageId != activeStatusId ||
                !_statusCallbackTokens.TryGetValue(chatId, out string currentToken) || currentToken != statusToken) return;
            data = "status:" + statusAction;
        }
        else if (TryReadTaggedCallback(data, "manage", out string manageToken, out string manageAction))
        {
            if (!_managementMessages.TryGetValue(chatId, out int activeManagementId) || messageId != activeManagementId ||
                !_managementCallbackTokens.TryGetValue(chatId, out string currentToken) || currentToken != manageToken) return;
            data = manageAction.StartsWith("remove:", StringComparison.Ordinal) ? manageAction : "manage:" + manageAction;
        }
        else if (TryReadTaggedCallback(data, "instance", out string instanceToken, out string instanceAction))
        {
            if (!_instanceMessages.TryGetValue(chatId, out int activeInstanceId) || messageId != activeInstanceId ||
                !_instanceCallbackTokens.TryGetValue(chatId, out string currentToken) || currentToken != instanceToken) return;
            data = "instance:" + instanceAction;
        }
        else if (data.StartsWith("panel:", StringComparison.Ordinal) || data.StartsWith("status:", StringComparison.Ordinal) || data.StartsWith("manage:", StringComparison.Ordinal) || data.StartsWith("remove:", StringComparison.Ordinal) || data.StartsWith("instance:", StringComparison.Ordinal)) return;

        if (data == "manage:open") { await ManageBotsAsync(chatId, ct); return; }
        if (data == "instance:refresh" || data == "instance:back") { await InstanceMenuAsync(chatId, ct); return; }
        if (data == "instance:create")
        {
            ManualInstanceResult created = await _controller.CreateManualInstanceAsync(ct);
            await InstanceMenuAsync(chatId, created.Message, ct);
            return;
        }
        if (data == "instance:continue") { await OpenSelectedInstancesAsync(chatId, ct); return; }
        if (data.StartsWith("instance:toggle:", StringComparison.Ordinal) && int.TryParse(data.Substring("instance:toggle:".Length), out int toggleId))
        {
            HashSet<int> selected = _instanceSelections.GetOrAdd(chatId, _ => new HashSet<int>());
            if (_controller.CanManageInstance(toggleId)) { lock (selected) { if (!selected.Add(toggleId)) selected.Remove(toggleId); } }
            await InstanceMenuAsync(chatId, ct);
            return;
        }
        if (data.StartsWith("instance:open:", StringComparison.Ordinal) && int.TryParse(data.Substring("instance:open:".Length), out int openId))
        {
            ManualInstanceResult result = await _controller.OpenManualInstanceAsync(openId, ct);
            await InstanceStatusAsync(chatId, result.Message, ct);
            return;
        }
        if (data.StartsWith("instance:finish:", StringComparison.Ordinal) && int.TryParse(data.Substring("instance:finish:".Length), out int finishId))
        {
            ManualInstanceResult result = await _controller.CloseManualInstanceAsync(finishId, ct);
            await InstanceMenuAsync(chatId, result.Message, ct);
            return;
        }
        if (data == "manage:back") { _managementCallbackTokens[chatId] = Guid.NewGuid().ToString("N").Substring(0, 8); await EditManagementAsync(chatId, "Manage Bots closed.", InlineKeyboardMarkup.Empty(), ct); await StatusAsync(chatId, ct); return; }
        if (data == "manage:add") { _addBotPending[chatId] = DateTimeOffset.UtcNow.AddMinutes(2); await EditManagementAsync(chatId, "Send the Telegram username of the promotion bot.\nExample: @newbot\nSend cancel to stop.", InlineKeyboardMarkup.Empty(), ct); return; }
        if (data == "manage:list") { await ManageBotsAsync(chatId, ct); return; }
        if (data == "manage:remove") { await RemovePickerAsync(chatId, ct); return; }
        if (data.StartsWith("remove:ask:", StringComparison.Ordinal)) { string name = data.Substring("remove:ask:".Length); _removeBotPending[chatId] = name; string token = Guid.NewGuid().ToString("N").Substring(0, 8); _managementCallbackTokens[chatId] = token; await EditManagementAsync(chatId, $"Remove @{name}?", new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("✅ Confirm", TaggedCallback("manage", token, "remove:yes")), InlineKeyboardButton.WithCallbackData("❌ Cancel", TaggedCallback("manage", token, "open")) } }), ct); return; }
        if (data == "remove:yes") { if (_removeBotPending.TryRemove(chatId, out var name) && _controller.RemoveBot(name)) await ManageBotsAsync(chatId, ct); else await EditManagementAsync(chatId, "Bot is no longer configured.", InlineKeyboardMarkup.Empty(), ct); return; }
        if (data == "remove:no") { _removeBotPending.TryRemove(chatId, out _); await ManageBotsAsync(chatId, ct); return; }
        if (data == "status:pause") { await ResultAsync(chatId, await _controller.PauseAsync(), ct); return; }
        if (data == "status:resume") { await ResultAsync(chatId, await _controller.ResumeAsync(), ct); return; }
        if (data == "status:refresh") { await StatusAsync(chatId, ct); return; }
        if (data == "status:stop") { await ConfirmStopAsync(chatId, ct); return; }
        if (data == "status:stop_yes") { _stopConfirmations.TryRemove(chatId, out _); _stopRequested[chatId] = 0; await ResultAsync(chatId, await _controller.StopAsync(), ct); return; }
        if (data == "status:stop_no") { _stopConfirmations.TryRemove(chatId, out _); await StatusAsync(chatId, ct); return; }
        if (data == "status:manage") { await ManageBotsAsync(chatId, ct); return; }
        if (data == "status:new") { await BeginAsync(chatId, ct); return; }
        // Ignore callbacks from old selection keyboards, even when their message ID is reused.
        if (data == "pause" || data == "resume" || data == "refresh" || data == "stop:ask" || data == "stop:yes" || data == "stop:no") return;
    }

    private async Task BackAsync(long chatId, Session session, CancellationToken ct)
    {
        switch (session.Step)
        {
            case Step.Instances: session.Step = Step.Bots; await BotPickerAsync(chatId, ct); break;
            case Step.Cycles: session.Step = Step.Accounts; session.LastAccountInstance(); await AccountPickerAsync(chatId, ct); break;
            case Step.Accounts: if (session.PreviousAccountInstance()) await AccountPickerAsync(chatId, ct); else { session.Step = Step.Instances; await InstancePickerAsync(chatId, ct); } break;
            case Step.Confirm: session.Step = Step.Cycles; await CyclePromptAsync(chatId, ct); break;
            default: await BotPickerAsync(chatId, ct); break;
        }
    }

    private async Task NumericAsync(long chatId, string text, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(chatId, out var s)) return;
        if (s.Step != Step.Cycles) return;
        if (!int.TryParse(text, out int n) || n < 1 || n > 100000)
        {
            await EditSelectionAsync(chatId, s, "🔄 Enter cycles per bot as a number from 1 to 100000.", revision => BasicSelectionKeyboard(revision, "back", "cancel"), ct);
            return;
        }
        s.Cycles = n;
        s.Step = Step.Confirm;
        await ConfirmRunAsync(chatId, s, ct);
    }

    private async Task BotPickerAsync(long chatId, CancellationToken ct)
    {
        var s = _sessions[chatId];
        await EditSelectionAsync(chatId, s, "🤖 Select promotion bots", revision =>
        {
            var rows = s.AvailableBots.Select(x => new[] { InlineKeyboardButton.WithCallbackData((s.SelectedBots.Contains(x) ? "☑ " : "⬜ ") + "@" + x, SelectionCallback(revision, "bot:" + x)) }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Select All", SelectionCallback(revision, "all")), InlineKeyboardButton.WithCallbackData("Clear All", SelectionCallback(revision, "clear")) });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Continue", SelectionCallback(revision, "continue")), InlineKeyboardButton.WithCallbackData("Cancel", SelectionCallback(revision, "cancel")) });
            return new InlineKeyboardMarkup(rows);
        }, ct);
    }

    private async Task InstancePickerAsync(long chatId, CancellationToken ct, string notice = "")
    {
        var s = _sessions[chatId];
        int[] configuredIds = _controller.GetConfiguredInstanceIds().Distinct().OrderBy(id => id).ToArray();
        int totalPages = (int)(((long)configuredIds.Length + InstancePageSize - 1) / InstancePageSize);
        int page = totalPages == 0 ? 0 : Math.Clamp(s.InstancePage, 0, totalPages - 1);
        s.InstancePage = page;
        int pageCount = Math.Min(InstancePageSize, configuredIds.Length - page * InstancePageSize);
        int[] visibleIds = configuredIds.Skip(page * InstancePageSize).Take(pageCount).ToArray();
        string title = totalPages == 0
            ? "🖥 No configured instances are available. Create an instance from Instances or /instance, then return here."
            : $"🖥 Select promotion instances\nPage {page + 1}/{totalPages}";
        if (!string.IsNullOrEmpty(notice)) title = "⚠️ " + notice + "\n" + title;
        await EditSelectionAsync(chatId, s, title, revision =>
        {
            var rows = visibleIds.Chunk(2).Select(pair => pair.Select(id => InlineKeyboardButton.WithCallbackData((s.SelectedInstances.Contains(id) ? "✅ " : "⬜ ") + "Instance " + id, SelectionCallback(revision, "instance:" + id))).ToArray()).ToList();
            if (page > 0 || page < totalPages - 1)
            {
                var navigation = new List<InlineKeyboardButton>();
                if (page > 0) navigation.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", SelectionCallback(revision, "instances:page:" + (page - 1))));
                if (page < totalPages - 1) navigation.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", SelectionCallback(revision, "instances:page:" + (page + 1))));
                rows.Add(navigation.ToArray());
            }
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Back", SelectionCallback(revision, "back")), InlineKeyboardButton.WithCallbackData("Continue", SelectionCallback(revision, "instances:continue")), InlineKeyboardButton.WithCallbackData("Cancel", SelectionCallback(revision, "cancel")) });
            return new InlineKeyboardMarkup(rows);
        }, ct);
    }

    private async Task CyclePromptAsync(long chatId, CancellationToken ct)
    {
        var s = _sessions[chatId];
        await EditSelectionAsync(chatId, s, "🔄 Send cycles per bot (1–100000).", revision => BasicSelectionKeyboard(revision, "back", "cancel"), ct);
    }

    private async Task AccountPickerAsync(long chatId, CancellationToken ct, string notice = "")
    {
        var s = _sessions[chatId];
        int instanceId = s.CurrentAccountInstance;
        string title = s.SelectedInstances.Count == 1 ? "👤 Select Telegram accounts" : $"👤 Select accounts for Instance {instanceId}";
        if (!string.IsNullOrEmpty(notice)) title = "⚠️ " + notice + "\n" + title;
        await EditSelectionAsync(chatId, s, title, revision =>
        {
            var rows = Enumerable.Range(1, 3).Select(x => new[] { InlineKeyboardButton.WithCallbackData((s.AccountsForCurrentInstance.Contains(x) ? "✅ " : "⬜ ") + "Account " + x, SelectionCallback(revision, "account:" + x)) }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Back", SelectionCallback(revision, "back")), InlineKeyboardButton.WithCallbackData("Continue", SelectionCallback(revision, "accounts:continue")), InlineKeyboardButton.WithCallbackData("Cancel", SelectionCallback(revision, "cancel")) });
            return new InlineKeyboardMarkup(rows);
        }, ct);
    }

    private Task ConfirmRunAsync(long chatId, Session s, CancellationToken ct) => EditSelectionAsync(chatId, s,
        $"🚀 Confirm promotion\nBots: {string.Join(", ", s.SelectedBots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(bot => "@" + bot))}\nInstances to run: [{string.Join(",", s.SelectedInstances.OrderBy(x => x))}]\n{string.Join("\n", s.SelectedInstances.OrderBy(x => x).Select(id => $"Instance {id} accounts: [{string.Join(",", s.AccountsByInstance[id].OrderBy(x => x))}]"))}\nCycles per bot: {s.Cycles}",
        revision => new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("▶ START", SelectionCallback(revision, "start")), InlineKeyboardButton.WithCallbackData("✏️ Edit bots", SelectionCallback(revision, "edit")) },
            new[] { InlineKeyboardButton.WithCallbackData("Back", SelectionCallback(revision, "back")), InlineKeyboardButton.WithCallbackData("Cancel", SelectionCallback(revision, "cancel")) }
        }), ct);

    private async Task StartAsync(long chatId, Session s, CancellationToken ct)
    {
        _sessions.TryRemove(chatId, out var ignored);
        await EditSelectionAsync(chatId, s, "⏳ Preparing promotion run…", _ => InlineKeyboardMarkup.Empty(), ct);
        _ = StartRunAsync(chatId, s, ct);
    }

    private async Task StartRunAsync(long chatId, Session s, CancellationToken ct)
    {
        try { await StartRunCoreAsync(chatId, s, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Promotion start UI update failed safely: " + ex.Message); }
    }

    private async Task StartRunCoreAsync(long chatId, Session s, CancellationToken ct)
    {
        ControlResult result;
        try
        {
            var instanceIds = s.SelectedInstances.OrderBy(id => id).ToList();
            var accountMap = instanceIds.ToDictionary(id => id, id => s.AccountsByInstance[id].OrderBy(x => x).ToList());
            var configured = await _controller.ConfigureAsync(new PromotionRunConfiguration { MaxSupportedInstanceCount = _controller.MaxSupportedInstanceCount, SelectedBots = s.SelectedBots.ToList(), InstanceCount = instanceIds.Count, SelectedInstanceIds = instanceIds, CyclesPerBot = s.Cycles, AccountsByInstance = accountMap, SelectedAccounts = accountMap.Values.SelectMany(x => x).Distinct().OrderBy(x => x).ToList() });
            if (!configured.Success)
            {
                Volatile.Write(ref s.StartInProgress, 0);
                _sessions[chatId] = s;
                await EditSelectionAsync(chatId, s, "⚠️ " + configured.Message, revision => new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Back", SelectionCallback(revision, "back")), InlineKeyboardButton.WithCallbackData("Cancel", SelectionCallback(revision, "cancel")) } }), ct);
                return;
            }
            result = await _controller.StartAsync();
        }
        catch (Exception ex)
        {
            Helpers.Logger.Warning("[CONTROL] Promotion start request failed: " + ex.Message);
            result = new ControlResult { Success = false, Message = "Promotion start failed: " + ex.Message };
        }
        if (!result.Success)
        {
            Volatile.Write(ref s.StartInProgress, 0);
            _sessions[chatId] = s;
            await EditSelectionAsync(chatId, s, "⚠️ " + result.Message, revision => new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("Back", SelectionCallback(revision, "back")), InlineKeyboardButton.WithCallbackData("Cancel", SelectionCallback(revision, "cancel")) } }), ct);
            await StatusAsync(chatId, ct);
            return;
        }
        await EditSelectionAsync(chatId, s, "✅ Promotion started. Live status is shown below.", _ => InlineKeyboardMarkup.Empty(), ct);
        await StatusAsync(chatId, ct);
        StartStatusRefresh(chatId);
    }

    private async Task ResultAsync(long chatId, ControlResult result, CancellationToken ct)
    {
        await StatusAsync(chatId, ct);
        await RenderDashboardAsync(chatId, ct);
        if (!result.Success && (!_lastNotification.TryGetValue(chatId, out string previous) || previous != result.Message))
        {
            _lastNotification[chatId] = result.Message;
            await SafeSendAsync(chatId, "⚠️ " + result.Message, null, ct);
        }
    }

    private async Task DashboardStartAsync(long chatId, CancellationToken ct)
    {
        PromotionStatus current = _controller.GetStatus();
        if (current.IsRunning || current.IsPaused || current.IsStopping)
        {
            await RenderDashboardAsync(chatId, ct);
            return;
        }
        if (!current.IsConfigured)
        {
            await BeginAsync(chatId, ct);
            return;
        }
        await ResultAsync(chatId, await _controller.StartAsync(), ct);
        if (_controller.GetStatus().IsRunning) StartStatusRefresh(chatId);
    }
    private async Task ManageBotsAsync(long chatId, CancellationToken ct)
    {
        var bots = _controller.GetAvailableBots();
        string list = bots.Count == 0 ? "(none configured)" : string.Join("\n", bots.Select((b, i) => $"{i + 1}. @{b}"));
        string token = Guid.NewGuid().ToString("N").Substring(0, 8);
        _managementCallbackTokens[chatId] = token;
        await EditManagementAsync(chatId, $"🤖 Promotion Bots\n\nConfigured bots:\n{list}", new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Add Bot", TaggedCallback("manage", token, "add")), InlineKeyboardButton.WithCallbackData("🗑 Remove Bot", TaggedCallback("manage", token, "remove")) },
            new[] { InlineKeyboardButton.WithCallbackData("📋 List Bots", TaggedCallback("manage", token, "list")) },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Back", TaggedCallback("manage", token, "back")) }
        }), ct);
    }

    private async Task RemovePickerAsync(long chatId, CancellationToken ct)
    {
        var bots = _controller.GetAvailableBots();
        if (bots.Count == 0) { _managementCallbackTokens[chatId] = Guid.NewGuid().ToString("N").Substring(0, 8); await EditManagementAsync(chatId, "No promotion bots are configured.", InlineKeyboardMarkup.Empty(), ct); return; }
        string token = Guid.NewGuid().ToString("N").Substring(0, 8);
        _managementCallbackTokens[chatId] = token;
        var rows = bots.Select(b => new[] { InlineKeyboardButton.WithCallbackData("🗑 @" + b, TaggedCallback("manage", token, "remove:ask:" + b)) }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Back", TaggedCallback("manage", token, "open")) });
        await EditManagementAsync(chatId, "Select a promotion bot to remove:", new InlineKeyboardMarkup(rows), ct);
    }

    private async Task ConfirmStopAsync(long chatId, CancellationToken ct)
    {
        _stopConfirmations[chatId] = 0;
        await RenderStatusAsync(chatId, null, true, ct);
    }

    private async Task StatusAsync(long chatId, CancellationToken ct)
    {
        _stopConfirmations.TryRemove(chatId, out _);
        await RenderStatusAsync(chatId, null, false, ct);
    }

    private async Task RenderStatusAsync(long chatId, string suffix, bool stopConfirmation, CancellationToken ct)
    {
        var gate = _statusLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            PromotionStatus status = _controller.GetStatus();
            string text = Format(status) + (stopConfirmation ? "\n\nStop this promotion?" : suffix ?? "");
            string token = Guid.NewGuid().ToString("N");
            InlineKeyboardMarkup keyboard = stopConfirmation
                ? new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("YES, STOP", TaggedCallback("status", token, "stop_yes")), InlineKeyboardButton.WithCallbackData("CANCEL", TaggedCallback("status", token, "stop_no")) } })
                : Keyboard(status, token);
            string renderKey = text + "\n" + (stopConfirmation ? "stop-confirm" : status.IsRunning + ":" + status.IsPaused + ":" + status.IsStopping);
            if (_lastStatusRender.TryGetValue(chatId, out string last) && last == renderKey && _statusMessages.ContainsKey(chatId)) return;

            if (_statusMessages.TryGetValue(chatId, out int id))
            {
                EditResult edit = await EditMessageSafeAsync(chatId, id, text, keyboard, ct);
                if (edit == EditResult.Success) { _statusCallbackTokens[chatId] = token; _lastStatusRender[chatId] = renderKey; return; }
                if (edit == EditResult.Failed) return;
                _statusMessages.TryRemove(chatId, out _);
            }
            Message sent = await SafeSendAsync(chatId, text, keyboard, ct);
            if (sent != null) { _statusMessages[chatId] = sent.MessageId; _statusCallbackTokens[chatId] = token; _lastStatusRender[chatId] = renderKey; }
        }
        finally { gate.Release(); }
    }

    private void StartStatusRefresh(long chatId)
    {
        var next = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _statusRefreshTokens.AddOrUpdate(chatId, next, (_, old) => { old.Cancel(); old.Dispose(); return next; });
        _stopRequested.TryRemove(chatId, out _);
        string runId = Guid.NewGuid().ToString("N");
        _statusRefreshRunIds[chatId] = runId;
        _ = RefreshAsync(chatId, runId, next.Token);
    }

    private async Task RefreshAsync(long chatId, string runId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await RenderStatusAsync(chatId, null, _stopConfirmations.ContainsKey(chatId), ct);
                var status = _controller.GetStatus();
                if (!status.IsRunning && !status.IsPaused && !status.IsStopping)
                {
                    if (_statusRefreshRunIds.TryGetValue(chatId, out string activeRunId) && activeRunId == runId && _terminalNotifiedRuns.TryAdd(runId, 0))
                    {
                        string message = !string.IsNullOrWhiteSpace(status.LastError)
                            ? "⚠️ Promotion run ended with an error. See /status."
                            : _stopRequested.ContainsKey(chatId) ? "⏹ Promotion stopped." : "✅ Promotion run completed.";
                        await SafeSendAsync(chatId, message, null, ct);
                    }
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Status update failed: " + ex.Message); }
    }

    private static InlineKeyboardMarkup Keyboard(PromotionStatus s, string token) => !s.IsRunning && !s.IsPaused && !s.IsStopping
        ? new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("▶ START NEW RUN", TaggedCallback("status", token, "new")), InlineKeyboardButton.WithCallbackData("🤖 Manage Bots", TaggedCallback("status", token, "manage")) } })
        : new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData(s.IsPaused ? "▶ RESUME" : "⏸ PAUSE", TaggedCallback("status", token, s.IsPaused ? "resume" : "pause")), InlineKeyboardButton.WithCallbackData("⛔ STOP", TaggedCallback("status", token, "stop")), InlineKeyboardButton.WithCallbackData("🔄 REFRESH", TaggedCallback("status", token, "refresh")) }, new[] { InlineKeyboardButton.WithCallbackData("🤖 Manage Bots", TaggedCallback("status", token, "manage")) } });

    private async Task EditSelectionAsync(long chatId, Session session, string text, Func<int, InlineKeyboardMarkup> keyboardFactory, CancellationToken ct, string actionName = "selection-update")
    {
        var gate = _selectionLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            int revision = ++session.SelectionRevision;
            InlineKeyboardMarkup keyboard = keyboardFactory(revision);
            int? messageId = _selectionMessages.TryGetValue(chatId, out int storedMessageId) ? storedMessageId : session.SelectionMessageId;
            if (messageId.HasValue)
            {
                session.SelectionMessageId = messageId;
                Helpers.Logger.Info($"[CONTROL][Selection] chat={chatId} action={actionName} operation=EDIT message_id={messageId.Value}");
                EditResult edit = await EditMessageSafeAsync(chatId, messageId.Value, text, keyboard, ct, actionName);
                if (edit == EditResult.Success)
                {
                    session.SelectionMessageId = messageId;
                    _selectionMessages[chatId] = messageId.Value;
                    Helpers.Logger.Info($"[CONTROL][Selection] chat={chatId} action={actionName} operation=EDIT_OK message_id={messageId.Value}");
                    return;
                }
                if (edit == EditResult.Failed) return;
                Helpers.Logger.Warning($"[CONTROL][Selection] chat={chatId} action={actionName} message_id={messageId.Value} original_message_uneditable; recreating selection message");
                _selectionMessages.TryRemove(chatId, out _);
                session.SelectionMessageId = null;
            }
            else Helpers.Logger.Info($"[CONTROL][Selection] chat={chatId} action={actionName} operation=SEND reason=no_reusable_selection_message");
            Message sent = await SafeSendAsync(chatId, text, keyboard, ct, "selection:" + actionName);
            if (sent != null)
            {
                session.SelectionMessageId = sent.MessageId;
                _selectionMessages[chatId] = sent.MessageId;
                Helpers.Logger.Info($"[CONTROL][Selection] chat={chatId} action={actionName} operation=SEND_OK message_id={sent.MessageId}");
            }
        }
        finally { gate.Release(); }
    }

    private async Task EditManagementAsync(long chatId, string text, InlineKeyboardMarkup keyboard, CancellationToken ct)
    {
        if (!keyboard.InlineKeyboard.Any()) _managementCallbackTokens[chatId] = Guid.NewGuid().ToString("N").Substring(0, 8);
        var gate = _managementLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_managementMessages.TryGetValue(chatId, out int id))
            {
                EditResult edit = await EditMessageSafeAsync(chatId, id, text, keyboard, ct);
                if (edit == EditResult.Success) return;
                if (edit == EditResult.Failed) return;
            }
            Message sent = await SafeSendAsync(chatId, text, keyboard, ct);
            if (sent != null) _managementMessages[chatId] = sent.MessageId;
        }
        finally { gate.Release(); }
    }

    private async Task InstanceMenuAsync(long chatId, CancellationToken ct) => await InstanceMenuAsync(chatId, "", ct);

    private async Task InstanceMenuAsync(long chatId, string notice, CancellationToken ct)
    {
        HashSet<int> selected = _instanceSelections.GetOrAdd(chatId, _ => new HashSet<int>());
        int[] registeredIds = _controller.GetRegisteredManualInstanceIds().ToArray();
        string[] lines = registeredIds.Select(id =>
        {
            if (_controller.IsManualInstanceOpen(id)) return $"Instance {id}: open in manual mode";
            if (_controller.IsInstanceInActivePromotion(id)) return $"Instance {id}: in use by promotion";
            return $"Instance {id}: available";
        }).ToArray();
        if (lines.Length == 0) lines = new[] { "No instances are currently manageable. Check that Chromium and existing instance profiles are available." };
        if (!string.IsNullOrWhiteSpace(notice)) lines = new[] { notice, "" }.Concat(lines).ToArray();
        await EditInstanceMessageAsync(chatId, "🖥 Instance Manager\nSelect the instances to open on the VNC desktop:\n" + string.Join("\n", lines), revision =>
        {
            var rows = new List<InlineKeyboardButton[]>();
            foreach (int id in registeredIds)
            {
                int instanceId = id;
                if (_controller.IsManualInstanceOpen(instanceId))
                    rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"🟢 Instance {instanceId} — Open", TaggedCallback("instance", revision, "open:" + instanceId)) });
                else if (_controller.CanManageInstance(instanceId))
                {
                    bool isSelected;
                    lock (selected) isSelected = selected.Contains(instanceId);
                    rows.Add(new[] { InlineKeyboardButton.WithCallbackData((isSelected ? "✅ " : "⬜ ") + "Instance " + instanceId, TaggedCallback("instance", revision, "toggle:" + instanceId)) });
                }
                else if (_controller.IsInstanceInActivePromotion(instanceId))
                    rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"🔒 Instance {instanceId} — Promotion active", TaggedCallback("instance", revision, "refresh")) });
            }
            if (_controller.CanCreateManualInstance)
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Create New Instance", TaggedCallback("instance", revision, "create")) });
            else if (_controller.GetRegisteredManualInstanceIds().Count >= _controller.ManualInstanceLimit)
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"Maximum instances reached ({_controller.ManualInstanceLimit})", TaggedCallback("instance", revision, "refresh")) });
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Continue", TaggedCallback("instance", revision, "continue")), InlineKeyboardButton.WithCallbackData("Refresh", TaggedCallback("instance", revision, "refresh")), InlineKeyboardButton.WithCallbackData("Back", TaggedCallback("instance", revision, "back")) });
            return new InlineKeyboardMarkup(rows);
        }, ct);
    }

    private async Task OpenSelectedInstancesAsync(long chatId, CancellationToken ct)
    {
        HashSet<int> selected = _instanceSelections.GetOrAdd(chatId, _ => new HashSet<int>());
        int[] ids;
        lock (selected) ids = selected.OrderBy(id => id).ToArray();
        if (ids.Length == 0) { await InstanceMenuAsync(chatId, "Select at least one available instance first.", ct); return; }
        var results = new List<string>();
        foreach (int id in ids)
        {
            ManualInstanceResult result = await _controller.OpenManualInstanceAsync(id, ct);
            results.Add(result.Message);
            if (result.Success) lock (selected) selected.Remove(id);
        }
        await InstanceStatusAsync(chatId, string.Join("\n", results) + "\nConnect to the existing VNC desktop to manage these browser windows. Promotion actions are not started.", ct);
    }

    private async Task InstanceStatusAsync(long chatId, string notice, CancellationToken ct)
    {
        int[] openIds = _controller.GetOpenManualInstanceIds().ToArray();
        string body = openIds.Length == 0
            ? "No instances are currently open in manual mode."
            : string.Join("\n", openIds.Select(id => $"Instance {id}: open on the VNC desktop."));
        string text = "🖥 Manual Instance Status\n" + notice + "\n\n" + body + "\nConnect to the existing VNC desktop to manage Chromium manually. No promotion actions are running from this command.";
        await EditInstanceMessageAsync(chatId, text, revision =>
        {
            var rows = openIds.Select(id => new[] { InlineKeyboardButton.WithCallbackData("Finish", TaggedCallback("instance", revision, "finish:" + id)) }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("Refresh", TaggedCallback("instance", revision, "refresh")), InlineKeyboardButton.WithCallbackData("Back", TaggedCallback("instance", revision, "back")) });
            return new InlineKeyboardMarkup(rows);
        }, ct);
    }

    private async Task EditInstanceMessageAsync(long chatId, string text, Func<string, InlineKeyboardMarkup> keyboardFactory, CancellationToken ct)
    {
        SemaphoreSlim gate = _instanceMessageLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            string token = Guid.NewGuid().ToString("N").Substring(0, 8);
            _instanceCallbackTokens[chatId] = token;
            InlineKeyboardMarkup keyboard = keyboardFactory(token);
            if (_instanceMessages.TryGetValue(chatId, out int messageId))
            {
                EditResult edit = await EditMessageSafeAsync(chatId, messageId, text, keyboard, ct, "instance-manager");
                if (edit == EditResult.Success) return;
                if (edit == EditResult.Failed) return;
                _instanceMessages.TryRemove(chatId, out _);
            }
            Message sent = await SafeSendAsync(chatId, text, keyboard, ct, "instance-manager");
            if (sent != null) _instanceMessages[chatId] = sent.MessageId;
        }
        finally { gate.Release(); }
    }

    private async Task<Message> SafeSendAsync(long chatId, string text, InlineKeyboardMarkup keyboard, CancellationToken ct, string actionName = "message-send")
    {
        var gate = _messageApiLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await WaitForMessageRateLimitAsync(chatId, ct);
            try { return await _bot.SendMessage(chatId, text, replyMarkup: keyboard == null ? null : keyboard.InlineKeyboard.Any() ? keyboard : MainReplyKeyboard(), cancellationToken: ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { Helpers.Logger.Warning($"[CONTROL] Telegram send failed: chat_id={chatId} action={actionName} error={ex.Message}"); return null; }
            finally { _lastMessageOperation[chatId] = DateTimeOffset.UtcNow; }
        }
        finally { gate.Release(); }
    }

    private async Task<EditResult> EditMessageSafeAsync(long chatId, int messageId, string text, InlineKeyboardMarkup keyboard, CancellationToken ct, string actionName = "message-update")
    {
        var gate = _messageApiLocks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await WaitForMessageRateLimitAsync(chatId, ct);
            try { await _bot.EditMessageText(chatId, messageId, text, replyMarkup: keyboard, cancellationToken: ct); return EditResult.Success; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ApiRequestException ex) when (IsMessageNotModified(ex)) { return EditResult.Success; }
            catch (ApiRequestException ex) when (IsMessageMissing(ex)) { Helpers.Logger.Warning($"[CONTROL] Telegram edit failed: chat_id={chatId} message_id={messageId} action={actionName} error={ex.Message}"); return EditResult.Missing; }
            catch (Exception ex) { Helpers.Logger.Warning($"[CONTROL] Telegram edit failed: chat_id={chatId} message_id={messageId} action={actionName} error={ex.Message}"); return EditResult.Failed; }
            finally { _lastMessageOperation[chatId] = DateTimeOffset.UtcNow; }
        }
        finally { gate.Release(); }
    }

    private async Task WaitForMessageRateLimitAsync(long chatId, CancellationToken ct)
    {
        if (_lastMessageOperation.TryGetValue(chatId, out DateTimeOffset last))
        {
            TimeSpan remaining = TimeSpan.FromSeconds(1) - (DateTimeOffset.UtcNow - last);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
        }
    }

    private static bool IsMessageNotModified(ApiRequestException ex) => ex.ErrorCode == 400 && ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase);
    private static bool IsMessageMissing(ApiRequestException ex) => ex.ErrorCode == 400 &&
        (ex.Message.Contains("message to edit not found", StringComparison.OrdinalIgnoreCase) ||
         ex.Message.Contains("message can't be edited", StringComparison.OrdinalIgnoreCase) ||
         ex.Message.Contains("message_id_invalid", StringComparison.OrdinalIgnoreCase));

    private static string SelectionCallback(int revision, string action) => $"sel:{revision}:{action}";
    private static bool TryReadSelectionCallback(string data, Session session, out string action)
    {
        action = "";
        if (!data.StartsWith("sel:", StringComparison.Ordinal)) return false;
        int separator = data.IndexOf(':', 4);
        if (separator < 0 || !int.TryParse(data.AsSpan(4, separator - 4), out int revision) || revision != session.SelectionRevision) return false;
        action = data.Substring(separator + 1);
        return action.Length > 0;
    }

    private static string TaggedCallback(string category, string token, string action) => $"{category}:{token}:{action}";
    private static bool TryReadTaggedCallback(string data, string category, out string token, out string action)
    {
        token = "";
        action = "";
        string prefix = category + ":";
        if (!data.StartsWith(prefix, StringComparison.Ordinal)) return false;
        int separator = data.IndexOf(':', prefix.Length);
        if (separator < 0) return false;
        token = data.Substring(prefix.Length, separator - prefix.Length);
        action = data.Substring(separator + 1);
        return token.Length > 0 && action.Length > 0;
    }

    private static InlineKeyboardMarkup BasicSelectionKeyboard(int revision, params string[] actions) => new(new[]
    {
        actions.Select(action => InlineKeyboardButton.WithCallbackData(action == "back" ? "Back" : "Cancel", SelectionCallback(revision, action))).ToArray()
    });
    private string Format(PromotionStatus s)
    {
        string state = s.IsStopping ? "\U0001F6D1 STOPPING" : s.IsPaused ? "\u23F8 PROMOTION PAUSED" : s.IsRunning ? "\U0001F680 PROMOTION RUNNING" : "\U0001F6D1 PROMOTION STOPPED";
        var firstInstanceAccounts = s.SelectedInstanceIds.Count > 0 && s.AccountsByInstance.TryGetValue(s.SelectedInstanceIds[0], out var configuredAccounts) ? configuredAccounts : new List<int>();
        string accountProgress = s.SelectedInstanceIds.Count == 1 && firstInstanceAccounts.Count > 0 ? $"Account: {s.CurrentSelectedAccountIndex + 1}/{firstInstanceAccounts.Count} (Account {s.CurrentAccount})\n" : "";
        var b = new StringBuilder($"{state}\n\n{accountProgress}{s.AccountState}\n\U0001F916 Configured bots: {_controller.GetConfiguredBotCount()}\nSelected bots: {s.SelectedBots.Count}\nAccounts selected: [{string.Join(",", s.SelectedAccounts)}]\n\U0001F310 Instances selected: [{string.Join(",", s.SelectedInstanceIds)}]\nRunning instances: [{string.Join(",", s.RunningInstanceIds)}]\n\U0001F504 Progress: {s.CompletedCycles}/{s.TotalCycles}\n");
        foreach (var i in s.Instances) b.Append($"\nInstance {i.InstanceId}\nAccount: {i.SelectedAccountIndex + 1}/{i.SelectedAccountCount} (Account {i.CurrentAccount})\nAccount state: {i.AccountState}\nBot: @{i.Bot}\nCycle: {i.CurrentCycle}/{i.TotalCyclesForBot}\nStatus: {i.State}\n{(string.IsNullOrWhiteSpace(i.Error) ? "" : "Error: " + i.Error + "\n")}");
        if (!string.IsNullOrWhiteSpace(s.LastError)) b.Append($"\n⚠️ Last error: {s.LastError}\n");
        return b.Append($"\n\u23F1 Runtime: {TimeSpan.FromSeconds(s.RuntimeSeconds):hh\\:mm\\:ss}\n\u26A1 Status: {(s.IsPaused ? "PAUSED" : s.IsRunning ? "RUNNING" : "STOPPED")}").ToString();
    }
    private static Task HandleErrorAsync(ITelegramBotClient _, Exception ex, HandleErrorSource __, CancellationToken ___) { Helpers.Logger.Warning("[CONTROL] Telegram error: " + ex.Message); return Task.CompletedTask; }
    public void Dispose() { _cts.Cancel(); try { _controller.CloseAllManualInstancesAsync().GetAwaiter().GetResult(); } catch (Exception ex) { Helpers.Logger.Warning("[CONTROL] Manual instance shutdown cleanup failed: " + ex.Message); } _cts.Dispose(); _apiHttpClient.Dispose(); }

    private sealed class Session
    {
        public List<string> AvailableBots { get; set; } = new();
        public HashSet<string> SelectedBots { get; set; } = new();
        public HashSet<int> SelectedInstances { get; set; } = new();
        public Dictionary<int, HashSet<int>> AccountsByInstance { get; set; } = new();
        public int CurrentAccountInstance { get; set; }
        public HashSet<int> AccountsForCurrentInstance => AccountsByInstance.TryGetValue(CurrentAccountInstance, out var accounts) ? accounts : AccountsByInstance[CurrentAccountInstance] = new HashSet<int>();
        public bool AdvanceAccountInstance() { var ids = SelectedInstances.OrderBy(x => x).ToList(); int index = ids.IndexOf(CurrentAccountInstance); if (index < 0 || index + 1 >= ids.Count) return false; CurrentAccountInstance = ids[index + 1]; return true; }
        public bool PreviousAccountInstance() { var ids = SelectedInstances.OrderBy(x => x).ToList(); int index = ids.IndexOf(CurrentAccountInstance); if (index <= 0) return false; CurrentAccountInstance = ids[index - 1]; return true; }
        public void LastAccountInstance() => CurrentAccountInstance = SelectedInstances.OrderBy(x => x).Last();
        public int InstanceCount { get; set; }
        public int InstancePage { get; set; }
        public int Cycles { get; set; }
        public Step Step { get; set; }
        public int SelectionRevision { get; set; }
        public int? SelectionMessageId { get; set; }
        public int StartInProgress;
    }
    private enum Step { Bots, Instances, Cycles, Accounts, Confirm }
    private enum EditResult { Success, Missing, Failed }
}
