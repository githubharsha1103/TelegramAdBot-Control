using TelegramAdBot.Helpers;
using TelegramAdBot.Models;
using TelegramAdBot.Core;

namespace TelegramAdBot.Control.Tests;

public sealed class GlobalMenuButtonPositionTests
{
    [Fact]
    public void SaveAndLoad_PreservesGlobalCoordinateAcrossStoreInstances()
    {
        string path = Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"), "menu.json");
        try
        {
            var first = new GlobalMenuButtonPositionStore(path);
            var expected = new GlobalMenuButtonPosition { X = 314, Y = 522, ViewportWidth = 1280, ViewportHeight = 720, CapturedAt = DateTimeOffset.UtcNow };
            Assert.True(first.Save(expected));

            GlobalMenuButtonPosition? loaded = new GlobalMenuButtonPositionStore(path).Load();
            Assert.NotNull(loaded);
            Assert.Equal(expected.X, loaded.X);
            Assert.Equal(expected.Y, loaded.Y);
            Assert.Equal(expected.ViewportWidth, loaded.ViewportWidth);
            Assert.Equal(expected.ViewportHeight, loaded.ViewportHeight);
            Assert.Equal(expected.CapturedAt, loaded.CapturedAt);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(-1, 0, 100, 100)]
    [InlineData(100, 0, 100, 100)]
    [InlineData(0, 100, 100, 100)]
    [InlineData(0, 0, 0, 100)]
    public void Save_RejectsInvalidViewportCoordinate(int x, int y, int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"), "menu.json");
        var store = new GlobalMenuButtonPositionStore(path);
        Assert.False(store.Save(new GlobalMenuButtonPosition { X = x, Y = y, ViewportWidth = width, ViewportHeight = height }));
        Assert.Null(store.Load());
    }

    [Theory]
    [InlineData(400, 1280, 640, 200)]
    [InlineData(320, 640, 1280, 640)]
    [InlineData(0, 100, 200, 0)]
    public void ScaleMenuCoordinate_UsesCapturedAndCurrentViewportRatio(int coordinate, int capturedExtent, int currentExtent, int expected)
    {
        Assert.Equal(expected, BotEngine.ScaleMenuCoordinate(coordinate, capturedExtent, currentExtent));
    }
}
