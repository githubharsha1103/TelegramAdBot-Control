using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

/// <summary>Telegram text-message control surface. Promotion and browser work stays in PromotionController.</summary>
public sealed class TelegramControlBot : IDisposable, IAsyncDisposable
{
    private static readonly string[][] KeyboardRows =
    {
        new[] { "🏠 Dashboard", "🚀 Promotion" },
        new[] { "🤖 Bots", "👥 Accounts" },
        new[] { "🖥 Instances", "📊 Activity" },
        new[] { "⚙️ Settings", "❓ Help" }
    };

    private static readonly ReplyKeyboardMarkup PersistentKeyboard = new(
        KeyboardRows.Select(row => row.Select(label => new KeyboardButton(label)).ToArray()))
    { ResizeKeyboard = true, IsPersistent = true, InputFieldPlaceholder = "Choose an option or send a command" };

    private readonly PromotionController _controller;
    private readonly long _adminId;
    private readonly TelegramBotClient _bot;
    private readonly HttpClient _apiHttpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly TelegramTextMessageDispatcher _sender;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _updateGates = new();
    private readonly ConcurrentDictionary<long, Workflow> _workflows = new();
    private readonly ConcurrentDictionary<long, byte> _seenUpdates = new();
    private readonly ConcurrentQueue<int> _seenOrder = new();
    private readonly ConcurrentQueue<string> _activity = new();
    private readonly ConcurrentDictionary<string, byte> _operations = new();
    private int _receiverStarted;
    private int _disposed;

    public TelegramControlBot(PromotionController controller, string token, long adminId)
    {
        _controller = controller;
        _adminId = adminId;
        // Preserve the VPS IPv4/DNS workaround used by Telegram's HTTP transport.
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                IPAddress? ipv4 = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 == null) throw new SocketException((int)SocketError.HostNotFound);
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(ipv4, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        _apiHttpClient = new HttpClient(handler, disposeHandler: true);
        _bot = new TelegramBotClient(token, _apiHttpClient);
        _sender = new TelegramTextMessageDispatcher(
            Path.Combine(Helpers.AppDataPaths.Root, "telegram_control_cooldowns.json"), SendMessageAsync,
            message => Helpers.Logger.Warning("[CONTROL] " + message));
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _receiverStarted, 1) != 0) return;
        _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message } }, _cts.Token);
        Helpers.Logger.Success("[CONTROL] Integrated Telegram text control bot started.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message) return;
        long chatId = message.Chat.Id;
        long? senderId = message.From?.Id;
        if (senderId != _adminId)
        {
            Enqueue(chatId, "Unauthorized. This control panel is restricted to its administrator.", "authorization", ct);
            return;
        }
        if (!_seenUpdates.TryAdd(update.Id, 0)) return;
        _seenOrder.Enqueue(update.Id);
        while (_seenOrder.Count > 2048 && _seenOrder.TryDequeue(out int old)) _seenUpdates.TryRemove(old, out _);

        string text = (message.Text ?? string.Empty).Trim();
        if (text.Length == 0) return;
        Helpers.Logger.Info($"[CONTROL] update received type=message chat={chatId} action={DescribeAction(text)}");
        SemaphoreSlim gate = _updateGates.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct))
        {
            Enqueue(chatId, "A request is being processed. Please send the next command in a moment.", "busy", ct);
            return;
        }
        try { await RouteAsync(chatId, text, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Helpers.Logger.Warning($"[CONTROL] update failed chat={chatId} input={DescribeAction(text)} error={ex}");
            Enqueue(chatId, "The request failed: " + Clean(ex.Message), "error", ct);
        }
        finally { gate.Release(); }
    }

    private async Task RouteAsync(long chatId, string raw, CancellationToken ct)
    {
        string text = Normalize(raw);
        string? route = ControlTextNavigation.Resolve(raw);
        if (route == "dashboard") { Screen(chatId, DashboardText(), "dashboard", ct); return; }
        if (route == "help") { Screen(chatId, HelpText(), "help", ct); return; }
        if (route == "promotion") { PromotionScreen(chatId, ct); return; }
        if (route == "bots") { Screen(chatId, BotsText(), "bots", ct); return; }
        if (route == "accounts") { Screen(chatId, AccountsText(), "accounts", ct); return; }
        if (route == "instances") { Screen(chatId, InstancesText(), "instances", ct); return; }
        if (route == "activity") { Screen(chatId, ActivityText(), "activity", ct); return; }
        if (route == "settings") { Screen(chatId, SettingsText(), "settings", ct); return; }
        if (text is "/configure" or "configure promotion" or "configure") { _workflows[chatId] = Workflow.SelectBots; Screen(chatId, "CONFIGURE PROMOTION · 1/4\n\nSend bot usernames separated by commas, or ALL for every configured bot.\n" + string.Join(", ", _controller.GetAvailableBots()), "promotion", ct); return; }
        if (text is "start promotion" or "/startpromotion") { StartConfiguredPromotion(chatId, ct); return; }
        if (text is "/pause" or "pause") { await RespondResultAsync(chatId, await _controller.PauseAsync(), "pause", ct); return; }
        if (text is "/resume" or "resume") { await RespondResultAsync(chatId, await _controller.ResumeAsync(), "resume", ct); return; }
        if (text is "/stop" or "stop") { _workflows[chatId] = Workflow.StopConfirm; Screen(chatId, "STOP PROMOTION?\n\nSend CONFIRM STOP to stop and clean up the active run, or CANCEL to keep it running.", "stop-confirm", ct); return; }
        if (text is "confirm stop")
        {
            if (!_workflows.TryGetValue(chatId, out Workflow flow) || flow != Workflow.StopConfirm) { Screen(chatId, "There is no pending stop confirmation.", "stop", ct); return; }
            _workflows.TryRemove(chatId, out _);
            RunBackground(chatId, "stop", token => _controller.StopAsync(), "stop", ct); return;
        }
        if (text is "cancel")
        {
            _workflows.TryRemove(chatId, out _);
            _pendingBotRemoval.TryRemove(chatId, out _);
            _selectedBots.TryRemove(chatId, out _);
            _selectedInstances.TryRemove(chatId, out _);
            _selectedCycles.TryRemove(chatId, out _);
            Screen(chatId, DashboardText(), "dashboard", ct);
            return;
        }
        if (text is "/createinstance" or "create instance") { RunBackground(chatId, "create-instance", async token => ToControlResult(await _controller.CreateManualInstanceAsync(token)), "instance", ct); return; }
        if (TryCommandArgument(text, "/open", "open ", out string argument) && int.TryParse(argument, out int openId)) { RunBackground(chatId, "open-" + openId, async token => ToControlResult(await _controller.OpenManualInstanceAsync(openId, token)), "instance", ct); return; }
        if (TryCommandArgument(text, "/close", "close ", out argument) && int.TryParse(argument, out int closeId)) { RunBackground(chatId, "close-" + closeId, async token => ToControlResult(await _controller.CloseManualInstanceAsync(closeId, token)), "instance", ct); return; }
        if (text is "/addbot" or "add bot") { _workflows[chatId] = Workflow.AddBot; Screen(chatId, "ADD BOT\n\nSend the bot username. Send CANCEL to return.", "bots", ct); return; }
        if (TryCommandArgument(text, "/removebot ", "remove bot ", out string botName))
        {
            string normalized = botName.Trim().TrimStart('@');
            if (!_controller.GetAvailableBots().Contains(normalized, StringComparer.OrdinalIgnoreCase)) { Screen(chatId, "That bot is not configured. Open Bots to see configured usernames.", "bots", ct); return; }
            _workflows[chatId] = Workflow.RemoveBotConfirm;
            _pendingBotRemoval[chatId] = normalized;
            Screen(chatId, $"REMOVE BOT @{normalized}?\n\nSend CONFIRM REMOVE to continue, or CANCEL.", "bots", ct); return;
        }
        if (text is "confirm remove")
        {
            if (!_workflows.TryGetValue(chatId, out Workflow flow) || flow != Workflow.RemoveBotConfirm || !_pendingBotRemoval.TryRemove(chatId, out string? pending)) { Screen(chatId, "There is no pending removal.", "bots", ct); return; }
            _workflows.TryRemove(chatId, out _);
            Screen(chatId, _controller.RemoveBot(pending) ? $"Removed @{pending}.\n\n{BotsText()}" : $"Could not remove @{pending}.", "bots", ct); return;
        }
        if (_workflows.TryGetValue(chatId, out Workflow workflow))
        {
            await HandleWorkflowInputAsync(chatId, workflow, raw, ct);
            return;
        }
        if (text.StartsWith("/open ", StringComparison.Ordinal) || text.StartsWith("/close ", StringComparison.Ordinal)) { Screen(chatId, "Use /open <registered instance ID> or /close <registered instance ID>.", "instances", ct); return; }
        Screen(chatId, "I didn't recognize that input. Choose an option from the keyboard or send /help.", "unknown", ct);
    }

    private readonly ConcurrentDictionary<long, string> _pendingBotRemoval = new();
    private readonly ConcurrentDictionary<long, List<string>> _selectedBots = new();
    private readonly ConcurrentDictionary<long, List<int>> _selectedInstances = new();
    private readonly ConcurrentDictionary<long, int> _selectedCycles = new();

    private async Task HandleWorkflowInputAsync(long chatId, Workflow flow, string raw, CancellationToken ct)
    {
        string value = raw.Trim();
        if (flow == Workflow.AddBot)
        {
            if (_controller.TryAddBot(value, out string added, out string error)) { _workflows.TryRemove(chatId, out _); Screen(chatId, $"Added @{added}.\n\n{BotsText()}", "bots", ct); }
            else Screen(chatId, error + "\n\nSend another username or CANCEL.", "bots", ct);
            return;
        }
        if (flow == Workflow.SelectBots)
        {
            List<string> available = _controller.GetAvailableBots().ToList();
            List<string> chosen = value.Equals("all", StringComparison.OrdinalIgnoreCase) ? available : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item => item.TrimStart('@')).ToList();
            if (chosen.Count == 0 || chosen.Any(item => !available.Contains(item, StringComparer.OrdinalIgnoreCase))) { Screen(chatId, "One or more bot names are not configured. Send configured usernames separated by commas.", "promotion", ct); return; }
            _selectedBots[chatId] = chosen;
            _workflows[chatId] = Workflow.SelectInstances;
            Screen(chatId, "CONFIGURE PROMOTION · 2/4\n\nSend registered instance IDs separated by commas.\nAvailable: " + string.Join(", ", _controller.GetConfiguredInstanceIds()), "promotion", ct); return;
        }
        if (flow == Workflow.SelectInstances)
        {
            if (!TryParseIds(value, out List<int> ids) || ids.Count == 0 || ids.Any(id => !_controller.GetConfiguredInstanceIds().Contains(id))) { Screen(chatId, "Select only registered instances with valid profiles. Available: " + string.Join(", ", _controller.GetConfiguredInstanceIds()), "promotion", ct); return; }
            _selectedInstances[chatId] = ids;
            _workflows[chatId] = Workflow.SelectCycles;
            Screen(chatId, "CONFIGURE PROMOTION · 3/4\n\nSend cycles per bot as a positive integer.", "promotion", ct); return;
        }
        if (flow == Workflow.SelectCycles)
        {
            if (!int.TryParse(value, out int cycles) || cycles < 1) { Screen(chatId, "Cycles must be a positive integer.", "promotion", ct); return; }
            _selectedCycles[chatId] = cycles;
            _workflows[chatId] = Workflow.SelectAccounts;
            Screen(chatId, "CONFIGURE PROMOTION · 4/4\n\nSend account mapping as instance=account,account;instance=account. Example: 2=1,3;11=2\nUse 1 for every selected instance if all should use Account 1.", "promotion", ct); return;
        }
        if (flow == Workflow.SelectAccounts)
        {
            List<int> instances = _selectedInstances.GetValueOrDefault(chatId) ?? new();
            if (!TryParseAccountMap(value, instances, out Dictionary<int, List<int>> mapping)) { Screen(chatId, "Invalid account mapping. Use 2=1,3;11=2, or send 1 to use Account 1 for each selected instance.", "promotion", ct); return; }
            var config = new PromotionRunConfiguration { SelectedBots = _selectedBots.GetValueOrDefault(chatId) ?? new(), SelectedInstanceIds = instances, InstanceCount = instances.Count, CyclesPerBot = _selectedCycles.GetValueOrDefault(chatId), AccountsByInstance = mapping, SelectedAccounts = mapping.Values.SelectMany(x => x).Distinct().ToList(), AccountCount = mapping.Values.Max(x => x.Count), MaxSupportedInstanceCount = _controller.MaxSupportedInstanceCount };
            ControlResult result = await _controller.ConfigureAsync(config);
            _workflows.TryRemove(chatId, out _);
            _selectedBots.TryRemove(chatId, out _); _selectedInstances.TryRemove(chatId, out _); _selectedCycles.TryRemove(chatId, out _);
            Screen(chatId, (result.Success ? "CONFIGURED\n\n" : "CONFIGURATION FAILED\n\n") + result.Message + "\n\nSend START PROMOTION to start, or choose another screen.", "promotion", ct);
        }
    }

    private void StartConfiguredPromotion(long chatId, CancellationToken ct)
    {
        PromotionStatus status = _controller.GetStatus();
        if (!status.IsConfigured) { _workflows[chatId] = Workflow.SelectBots; Screen(chatId, "No promotion is configured. Send configured bot usernames separated by commas.", "promotion", ct); return; }
        RunBackground(chatId, "start", token => StartWithResultAsync(token), "promotion", ct);
    }

    private async Task<ControlResult> StartWithResultAsync(CancellationToken ct) => await _controller.StartAsync();

    private static ControlResult ToControlResult(ManualInstanceResult result) => new() { Success = result.Success, Message = result.Message };

    private void RunBackground(long chatId, string operationId, Func<CancellationToken, Task<ControlResult>> operation, string screen, CancellationToken ct)
    {
        string operationKey = chatId + ":" + operationId;
        if (!_operations.TryAdd(operationKey, 0)) { Screen(chatId, "That operation is already in progress.", screen, ct); return; }
        Screen(chatId, $"{operationId.Replace('-', ' ')} started. You can continue using the control panel while it runs.", screen, ct);
        _ = Task.Run(async () =>
        {
            try
            {
                ControlResult result = await operation(_cts.Token);
                RecordActivity($"{operationId}: {(result.Success ? "success" : "failed")} — {result.Message}");
                Enqueue(chatId, (result.Success ? "Completed: " : "Failed: ") + result.Message, screen, _cts.Token);
            }
            catch (Exception ex) { Helpers.Logger.Warning($"[CONTROL] operation={operationId} chat={chatId} error={ex}"); Enqueue(chatId, "Operation failed: " + Clean(ex.Message), screen, _cts.Token); }
            finally { _operations.TryRemove(operationKey, out _); }
        }, CancellationToken.None);
    }

    private Task RespondResultAsync(long chatId, ControlResult result, string screen, CancellationToken ct)
    {
        RecordActivity($"{screen}: {(result.Success ? "success" : "failed")} — {result.Message}");
        Enqueue(chatId, (result.Success ? "Done: " : "Not completed: ") + result.Message + "\n\n" + PromotionSummary(), screen, ct);
        return Task.CompletedTask;
    }

    private void PromotionScreen(long chatId, CancellationToken ct) => Screen(chatId,
        "PROMOTION\n\n" + PromotionSummary() + "\n\nCommands:\n/configure — configure bots, registered instances, cycles, and account mapping\nSTART PROMOTION — start configured run\n/pause · /resume · /stop — run controls", "promotion", ct);

    private string DashboardText() => "TELEGRAM AD BOT\n────────────────────\n" + PromotionSummary() +
        "\n\nConfigured bots: " + _controller.GetAvailableBots().Count +
        "\nRegistered instances: " + _controller.GetConfiguredInstanceIds().Count +
        "\nOpen browsers: " + _controller.GetOpenManualInstanceIds().Count +
        "\n\nChoose a section below. Use /help for commands.";

    private string PromotionSummary()
    {
        PromotionStatus s = _controller.GetStatus();
        string bots = s.SelectedBots.Count == 0 ? "—" : string.Join(", ", s.SelectedBots.Select(x => "@" + x));
        string ids = s.SelectedInstanceIds.Count == 0 ? "—" : string.Join(", ", s.SelectedInstanceIds);
        string activeBot = string.Join(", ", s.Instances.Where(instance => instance.State == "Running" && !string.IsNullOrWhiteSpace(instance.Bot)).Select(instance => "@" + instance.Bot).Distinct());
        return $"Automation: {s.RunState}\nBots: {bots}\nActive bot: {(activeBot.Length == 0 ? "—" : activeBot)}\nInstances: {ids}\nAccount: {s.AccountState}\nCycles: {s.CompletedCycles}/{s.TotalCycles}" +
               (string.IsNullOrWhiteSpace(s.LastError) ? "" : "\nLast error: " + Clean(s.LastError));
    }

    private string BotsText()
    {
        IReadOnlyList<string> bots = _controller.GetAvailableBots();
        return "BOTS\n────────────────────\n" + (bots.Count == 0 ? "No bots are configured." : string.Join("\n", bots.Select((bot, i) => $"{i + 1}. @{bot}"))) +
               "\n\n/addbot — add a username\n/removebot <username> — request removal confirmation";
    }

    private string AccountsText()
    {
        PromotionStatus s = _controller.GetStatus();
        int[] ids = _controller.GetConfiguredInstanceIds().ToArray();
        if (ids.Length == 0) return "ACCOUNTS\n────────────────────\nNo registered instance profiles are available.";
        return "ACCOUNTS\n────────────────────\n" + string.Join("\n", ids.Select(id => s.AccountsByInstance.TryGetValue(id, out List<int>? accounts) && accounts.Count > 0
            ? $"Instance {id}: {string.Join(", ", accounts.Select(account => $"Account {account}"))}"
            : $"Instance {id}: no promotion mapping configured")) + "\n\nUse /configure to update mappings for a promotion run.";
    }

    private string InstancesText()
    {
        var registered = _controller.GetRegisteredManualInstanceIds();
        var open = _controller.GetOpenManualInstanceIds().ToHashSet();
        string rows = registered.Count == 0 ? "No registered instances with valid profiles." : string.Join("\n", registered.Select(id => $"{id}: {(open.Contains(id) ? "open" : "closed")}"));
        return "INSTANCES\n────────────────────\n" + rows + $"\n\nManual instance limit: {_controller.ManualInstanceLimit}" +
               "\n/createinstance — create profile\n/open <id> — open registered profile\n/close <id> — close manually managed instance";
    }

    private string ActivityText()
    {
        string[] entries = _activity.ToArray();
        return "ACTIVITY\n────────────────────\n" + (entries.Length == 0 ? "No recent control operations." : string.Join("\n", entries.Reverse().Take(30)));
    }

    private string SettingsText()
    {
        PromotionStatus status = _controller.GetStatus();
        return $"SETTINGS\n────────────────────\nConfigured instance capacity: {_controller.MaxSupportedInstanceCount}\nConfigured bots: {_controller.GetConfiguredBotCount()}\nPromotion configured: {(status.IsConfigured ? "Yes" : "No")}\n\nAccount mappings and run options are configured through Promotion.\nThe Telegram transport retains the application's IPv4/DNS workaround.";
    }

    private static string HelpText() => "HELP\n────────────────────\n/start or /dashboard — dashboard\n/promotion — promotion status and controls\n/configure — configure a promotion run\nSTART PROMOTION — start configured run\n/pause · /resume · /stop — pause, resume, or confirm stop\n/bots · /accounts · /instances · /activity (/status) · /settings — screens\n/addbot · /removebot <username> — bot configuration\n/createinstance · /open <id> · /close <id> — manual profile controls\n\nKeyboard options are ordinary text messages. Send CANCEL to exit a pending prompt.";

    private void Screen(long chatId, string text, string name, CancellationToken ct) => Enqueue(chatId, text, name, ct);
    private void Enqueue(long chatId, string text, string screen, CancellationToken ct)
    {
        Helpers.Logger.Info($"[CONTROL] text response queued chat={chatId} screen={screen}");
        string boundedText = text.Length <= 4000 ? text : text[..3990] + "…\n[truncated]";
        _sender.Enqueue(chatId, new TelegramTextMessage(screen, boundedText, PersistentKeyboard), ct);
    }

    private async Task<TelegramTextSendResult> SendMessageAsync(long chatId, TelegramTextMessage message, CancellationToken ct)
    {
        try
        {
            await _bot.SendMessage(chatId, message.Text, replyMarkup: message.Keyboard, cancellationToken: ct);
            Helpers.Logger.Info($"[CONTROL] message sent chat={chatId} screen={message.Screen}");
            return new TelegramTextSendResult(true);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 429)
        {
            int retryAfter = TelegramRetryAfter.Parse(ex);
            Helpers.Logger.Warning($"[CONTROL] Telegram send rate limited chat={chatId} screen={message.Screen} retry_after={retryAfter}s error={ex}");
            return new TelegramTextSendResult(false, retryAfter);
        }
        catch (Exception ex)
        {
            Helpers.Logger.Warning($"[CONTROL] Telegram send failed chat={chatId} screen={message.Screen} error={ex}");
            return new TelegramTextSendResult(false);
        }
    }

    private void RecordActivity(string entry)
    {
        _activity.Enqueue($"{DateTimeOffset.Now:HH:mm:ss} {entry}");
        while (_activity.Count > 100) _activity.TryDequeue(out _);
    }

    private static bool TryParseIds(string text, out List<int> ids)
    {
        ids = new List<int>();
        foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out int id) || id <= 0) return false;
            ids.Add(id);
        }
        return ids.Count > 0 && ids.Distinct().Count() == ids.Count;
    }

    private static bool TryParseAccountMap(string value, IReadOnlyList<int> selected, out Dictionary<int, List<int>> map)
    {
        map = new Dictionary<int, List<int>>();
        if (value == "1") { foreach (int id in selected) map[id] = new List<int> { 1 }; return true; }
        foreach (string assignment in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pieces = assignment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length != 2 || !int.TryParse(pieces[0], out int instance) || !selected.Contains(instance) || !TryParseIds(pieces[1], out List<int> accounts)) return false;
            map[instance] = accounts;
        }
        return map.Count == selected.Count && selected.All(map.ContainsKey);
    }

    private static bool TryCommandArgument(string text, string slashPrefix, string labelPrefix, out string value)
    {
        string prefix = text.StartsWith(slashPrefix, StringComparison.Ordinal) ? slashPrefix : labelPrefix;
        if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length) { value = text[prefix.Length..].Trim(); return value.Length > 0; }
        value = string.Empty; return false;
    }

    private static string Normalize(string text) => string.Join(' ', text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    private static string DescribeAction(string text) => text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "empty";
    private static string Clean(string text) => text.Replace('\r', ' ').Replace('\n', ' ').Trim();
    private static Task HandleErrorAsync(ITelegramBotClient _, Exception ex, HandleErrorSource __, CancellationToken ___) { Helpers.Logger.Warning("[CONTROL] Telegram receiver error: " + ex); return Task.CompletedTask; }

    private enum Workflow { SelectBots, SelectInstances, SelectCycles, SelectAccounts, AddBot, RemoveBotConfirm, StopConfirm }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _cts.CancelAsync();
        _bot.Close();
        _apiHttpClient.Dispose();
        _cts.Dispose();
        foreach (SemaphoreSlim gate in _updateGates.Values) gate.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
