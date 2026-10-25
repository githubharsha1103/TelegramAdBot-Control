using TelegramAdBot.Control;

namespace TelegramAdBot.Control.Tests;

public sealed class TelegramControlStateStoreTests
{
    [Fact]
    public void MessageIdsAndCooldownSurviveStoreReload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "control-state.json");
        try
        {
            DateTimeOffset cooldown = DateTimeOffset.UtcNow.AddMinutes(46);
            var store = new TelegramControlStateStore(path);
            store.SetPrimaryMessage(123, 456);
            store.SetMenuKeyboardMessage(123, 457);
            store.SetRateLimitedUntil(123, cooldown);

            var restored = new TelegramControlStateStore(path).Snapshot()[123];

            Assert.Equal(456, restored.PrimaryMessageId);
            Assert.Equal(457, restored.MenuKeyboardMessageId);
            Assert.Equal(cooldown, restored.RateLimitedUntil);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EarlierCooldownCannotShortenExistingServerCooldown()
    {
        string directory = Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "control-state.json");
        try
        {
            DateTimeOffset later = DateTimeOffset.UtcNow.AddHours(1);
            var store = new TelegramControlStateStore(path);
            store.SetRateLimitedUntil(123, later);
            store.SetRateLimitedUntil(123, DateTimeOffset.UtcNow.AddSeconds(5));

            Assert.Equal(later, store.Snapshot()[123].RateLimitedUntil);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
