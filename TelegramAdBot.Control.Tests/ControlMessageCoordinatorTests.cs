using Telegram.Bot.Types.ReplyMarkups;
using TelegramAdBot.Control;

namespace TelegramAdBot.Control.Tests;

public sealed class ControlMessageCoordinatorTests
{
    [Fact]
    public async Task DashboardInstancesDashboardEditsSameTrackedMessage()
    {
        var fake = new FakeTelegram();
        var coordinator = fake.CreateCoordinator();
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.LastText == "Instances");
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.LastText == "Dashboard" && fake.Edits.Count >= 2);

        Assert.Equal(1, fake.Sends);
        Assert.Equal(new[] { 100, 100 }, fake.Edits.Select(edit => edit.MessageId));
    }

    [Fact]
    public async Task RapidNavigationCoalescesToNewestScreen()
    {
        var fake = new FakeTelegram { BlockFirstEdit = true };
        var coordinator = fake.CreateCoordinator();
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await fake.FirstEditEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Enqueue(1, "accounts", "Accounts", InlineKeyboardMarkup.Empty());
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        fake.ReleaseFirstEdit.TrySetResult();
        await Eventually(() => fake.LastText == "Dashboard" && fake.Edits.Count == 2);

        Assert.Equal(new[] { "Instances", "Dashboard" }, fake.Edits.Select(edit => edit.Text));
        Assert.Equal(1, fake.Sends);
    }

    [Fact]
    public async Task RateLimitWaitsOffCallerAndUsesNewestPendingRender()
    {
        var fake = new FakeTelegram { RateLimitFirstEdit = true };
        var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requestedDelay = 0;
        var coordinator = fake.CreateCoordinator((seconds, _) => { requestedDelay = seconds; return delay.Task; });
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        long started = Environment.TickCount64;
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await fake.FirstEditEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Enqueue(1, "settings", "Settings", InlineKeyboardMarkup.Empty());

        Assert.True(Environment.TickCount64 - started < 500);
        await Eventually(() => requestedDelay == 60);
        delay.TrySetResult();
        await Eventually(() => fake.LastText == "Settings");
        Assert.Equal(new[] { "Instances", "Settings" }, fake.Edits.Select(edit => edit.Text));
        Assert.Equal(1, fake.Sends);
    }

    [Fact]
    public async Task LongCooldownIsPassedThroughWithoutShortening()
    {
        var fake = new FakeTelegram { RateLimitEdits = 1 };
        int requestedDelay = 0;
        var coordinator = fake.CreateCoordinator((seconds, _) => { requestedDelay = seconds; return Task.CompletedTask; });
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.LastText == "Instances");
        Assert.Equal(900, requestedDelay);
        Assert.Equal(2, fake.Edits.Count);
    }

    [Fact]
    public async Task NewestScreenWinsAfterRateLimitedInitialSend()
    {
        var fake = new FakeTelegram { RateLimitFirstSend = true };
        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = fake.CreateCoordinator(async (_, ct) =>
        {
            delayEntered.TrySetResult();
            await releaseDelay.Task.WaitAsync(ct);
        });

        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Enqueue(1, "promotion-workflow", "Promotion", InlineKeyboardMarkup.Empty());
        releaseDelay.TrySetResult();
        await Eventually(() => fake.LastText == "Promotion");

        Assert.Equal(new[] { "Dashboard", "Promotion" }, fake.SentTexts);
        Assert.Equal(2, fake.Sends);
        Assert.Equal(101, coordinator.GetMessageId(1));
    }

    [Fact]
    public async Task RepeatedRateLimitsDoNotAbandonLatestScreen()
    {
        var fake = new FakeTelegram { RateLimitEdits = 5 };
        var coordinator = fake.CreateCoordinator((_, _) => Task.CompletedTask);
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.LastText == "Instances");
        Assert.Equal(6, fake.Edits.Count);
        Assert.Equal("Instances", fake.Edits.Last().Text);
        Assert.Null(coordinator.GetDesiredRevision(1));
    }

    [Fact]
    public async Task CancellationLeavesWorkerRecoverableByNewRequest()
    {
        var fake = new FakeTelegram { RateLimitEdits = 1 };
        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = fake.CreateCoordinator(async (_, ct) =>
        {
            delayEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        using var cancel = new CancellationTokenSource();
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty(), cancel.Token);
        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancel.Cancel();
        coordinator.Enqueue(1, "help", "Help", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.LastText == "Help");
        Assert.Equal("Help", fake.Edits.Last().Text);
        Assert.Null(coordinator.GetDesiredRevision(1));
    }

    [Fact]
    public async Task DeletedMessageCreatesOnlyOneReplacement()
    {
        var fake = new FakeTelegram { DeleteFirstMessage = true };
        var coordinator = fake.CreateCoordinator();
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 101);
        await Task.Delay(30);

        Assert.Equal(2, fake.Sends);
        Assert.Equal(1, fake.Edits.Count);
        Assert.Equal(101, coordinator.GetMessageId(1));
    }

    [Fact]
    public async Task UnchangedRenderSkipsTelegramEdit()
    {
        var fake = new FakeTelegram();
        var coordinator = fake.CreateCoordinator();
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Task.Delay(30);

        Assert.Empty(fake.Edits);
    }

    [Fact]
    public async Task RenderWithChangedKeyboardIsNotSkipped()
    {
        var fake = new FakeTelegram();
        var coordinator = fake.CreateCoordinator();
        coordinator.Enqueue(1, "dashboard", "Dashboard", new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("A", "a") } }));
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "dashboard", "Dashboard", new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("B", "b") } }));
        await Eventually(() => fake.Edits.Count == 1);

        Assert.Equal("Dashboard", fake.Edits[0].Text);
    }

    [Fact]
    public async Task TransientFailureDoesNotPreventLaterScreenRequest()
    {
        var fake = new FakeTelegram { FailFirstEdit = true };
        var coordinator = fake.CreateCoordinator();
        coordinator.Enqueue(1, "dashboard", "Dashboard", InlineKeyboardMarkup.Empty());
        await Eventually(() => coordinator.GetMessageId(1) == 100);
        coordinator.Enqueue(1, "instances", "Instances", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.Edits.Count == 1);
        coordinator.Enqueue(1, "help", "Help", InlineKeyboardMarkup.Empty());
        await Eventually(() => fake.LastText == "Help");

        Assert.Equal(1, fake.Sends);
        Assert.Equal("Help", fake.Edits.Last().Text);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), "Condition did not become true before timeout.");
    }

    private sealed class FakeTelegram
    {
        public readonly List<(int MessageId, string Text)> Edits = new();
        public readonly List<string> SentTexts = new();
        public readonly TaskCompletionSource FirstEditEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource ReleaseFirstEdit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Sends;
        public string LastText = "";
        public bool BlockFirstEdit;
        public bool RateLimitFirstEdit;
        public bool RateLimitFirstSend;
        public int RateLimitEdits;
        public bool DeleteFirstMessage;
        public bool FailFirstEdit;

        public ControlMessageCoordinator CreateCoordinator(Func<int, CancellationToken, Task>? delay = null) => new(Edit, Send, delay: delay);

        private Task<ControlMessageResult> Send(long chatId, ControlScreen screen, CancellationToken ct)
        {
            int sendNumber = Interlocked.Increment(ref Sends);
            SentTexts.Add(screen.Text);
            if (RateLimitFirstSend && sendNumber == 1)
                return Task.FromResult(new ControlMessageResult(ControlMessageOperation.RateLimited, RetryAfterSeconds: 2807));
            int id = sendNumber == 1 ? 100 : 101;
            LastText = screen.Text;
            return Task.FromResult(new ControlMessageResult(ControlMessageOperation.Applied, id));
        }

        private async Task<ControlMessageResult> Edit(long chatId, int messageId, ControlScreen screen, CancellationToken ct)
        {
            if (RateLimitEdits > 0)
            {
                RateLimitEdits--;
                Edits.Add((messageId, screen.Text));
                if (Edits.Count == 1) FirstEditEntered.TrySetResult();
                return new ControlMessageResult(ControlMessageOperation.RateLimited, RetryAfterSeconds: 900);
            }
            if (FailFirstEdit && Edits.Count == 0)
            {
                Edits.Add((messageId, screen.Text));
                return new ControlMessageResult(ControlMessageOperation.TransientFailure);
            }
            if (BlockFirstEdit && Edits.Count == 0)
            {
                FirstEditEntered.TrySetResult();
                await ReleaseFirstEdit.Task.WaitAsync(ct);
            }
            if (RateLimitFirstEdit && Edits.Count == 0)
            {
                Edits.Add((messageId, screen.Text));
                FirstEditEntered.TrySetResult();
                return new ControlMessageResult(ControlMessageOperation.RateLimited, RetryAfterSeconds: 60);
            }
            Edits.Add((messageId, screen.Text));
            if (DeleteFirstMessage && messageId == 100) return new ControlMessageResult(ControlMessageOperation.Missing);
            LastText = screen.Text;
            return new ControlMessageResult(ControlMessageOperation.Applied);
        }
    }
}
