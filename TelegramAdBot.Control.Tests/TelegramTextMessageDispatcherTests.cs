using System.Collections.Concurrent;
using TelegramAdBot.Control;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramAdBot.Control.Tests;

public sealed class TelegramTextMessageDispatcherTests
{
    [Fact]
    public async Task NewerScreenReplacesRateLimitedScreenAndHonorsLongServerDelay()
    {
        string path = TempFile();
        var sent = new ConcurrentQueue<string>();
        var firstDelayStarted = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long ticks = DateTimeOffset.UtcNow.Ticks;
        var dispatcher = new TelegramTextMessageDispatcher(path, (_, message, _) =>
        {
            sent.Enqueue(message.Screen);
            return Task.FromResult(sent.Count == 1 ? new TelegramTextSendResult(false, 2807) : new TelegramTextSendResult(true));
        }, delay: async (delay, token) =>
        {
            firstDelayStarted.TrySetResult(delay);
            await releaseDelay.Task.WaitAsync(token);
            Interlocked.Add(ref ticks, delay.Ticks);
        }, now: () => new DateTimeOffset(Interlocked.Read(ref ticks), TimeSpan.Zero));

        dispatcher.Enqueue(1, Message("dashboard"));
        TimeSpan requestedDelay = await firstDelayStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(requestedDelay >= TimeSpan.FromSeconds(2807), $"Requested only {requestedDelay}");
        dispatcher.Enqueue(1, Message("promotion"));
        releaseDelay.TrySetResult();
        await EventuallyAsync(() => sent.Count >= 2);
        Assert.Equal(new[] { "dashboard", "promotion" }, sent.ToArray());
    }

    [Fact]
    public async Task RepeatedRateLimitsDoNotAbandonLatestScreen()
    {
        string path = TempFile();
        var sent = new ConcurrentQueue<string>();
        int attempts = 0;
        long ticks = DateTimeOffset.UtcNow.Ticks;
        var dispatcher = new TelegramTextMessageDispatcher(path, (_, message, _) =>
        {
            sent.Enqueue(message.Screen);
            return Task.FromResult(Interlocked.Increment(ref attempts) <= 5
                ? new TelegramTextSendResult(false, 2)
                : new TelegramTextSendResult(true));
        }, delay: (delay, _) => { Interlocked.Add(ref ticks, delay.Ticks); return Task.CompletedTask; },
            now: () => new DateTimeOffset(Interlocked.Read(ref ticks), TimeSpan.Zero));

        dispatcher.Enqueue(2, Message("activity"));
        await EventuallyAsync(() => attempts == 6);
        Assert.Equal(6, sent.Count);
        Assert.All(sent, screen => Assert.Equal("activity", screen));
    }

    [Fact]
    public async Task CancellationDuringCooldownAllowsWorkerRecovery()
    {
        string path = TempFile();
        var sent = new ConcurrentQueue<string>();
        var firstDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long ticks = DateTimeOffset.UtcNow.Ticks;
        int calls = 0;
        var dispatcher = new TelegramTextMessageDispatcher(path, (_, message, _) =>
        {
            sent.Enqueue(message.Screen);
            return Task.FromResult(Interlocked.Increment(ref calls) == 1 ? new TelegramTextSendResult(false, 10) : new TelegramTextSendResult(true));
        }, delay: async (delay, token) =>
        {
            firstDelayStarted.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { Interlocked.Add(ref ticks, delay.Ticks); throw; }
        }, now: () => new DateTimeOffset(Interlocked.Read(ref ticks), TimeSpan.Zero));

        using var canceled = new CancellationTokenSource();
        dispatcher.Enqueue(3, Message("dashboard"), canceled.Token);
        await firstDelayStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        canceled.Cancel();
        dispatcher.Enqueue(3, Message("help"));
        await EventuallyAsync(() => sent.Contains("help"));
        Assert.Equal(new[] { "dashboard", "help" }, sent.ToArray());
    }

    private static TelegramTextMessage Message(string screen) => new(screen, screen, new ReplyKeyboardMarkup(Array.Empty<KeyboardButton[]>()));
    private static string TempFile() => Path.Combine(Path.GetTempPath(), "tg-control-" + Guid.NewGuid().ToString("N"), "cooldowns.json");
    private static async Task EventuallyAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(4);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), "Condition did not become true before timeout.");
    }
}
