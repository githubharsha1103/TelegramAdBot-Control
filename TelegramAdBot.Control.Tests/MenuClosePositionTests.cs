using TelegramAdBot.Control;
using TelegramAdBot.Core;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Control.Tests;

public sealed class MenuClosePositionTests
{
    [Fact]
    public void CapturesSaveIndependentlyAndPersistAcrossStoreInstances()
    {
        string path = NewPath();
        try
        {
            var store = new MenuClosePositionStore(path);
            var outChat = Position(91, 111);
            var inChat = Position(733, 588);
            Assert.True(store.Save(MenuCaptureTarget.OutOfChat, outChat));
            Assert.True(store.Save(MenuCaptureTarget.InChat, inChat));

            MenuClosePositions loaded = new MenuClosePositionStore(path).Load();
            AssertPosition(outChat, loaded.OutOfChatMenuClosePosition);
            AssertPosition(inChat, loaded.InChatMenuClosePosition);

            Assert.True(store.Save(MenuCaptureTarget.OutOfChat, Position(123, 145)));
            loaded = new MenuClosePositionStore(path).Load();
            Assert.Equal(123, loaded.OutOfChatMenuClosePosition.X);
            Assert.Equal(733, loaded.InChatMenuClosePosition.X);
        }
        finally { DeleteDirectory(path); }
    }

    [Fact]
    public void LegacySinglePositionLoadsAsInChatPosition()
    {
        string path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "{\"x\":12,\"y\":34,\"viewportWidth\":800,\"viewportHeight\":600,\"capturedAt\":\"2026-11-05T00:00:00+00:00\"}");
            MenuClosePositions loaded = new MenuClosePositionStore(path).Load();
            Assert.False(loaded.OutOfChatMenuClosePosition.Configured);
            Assert.Equal(12, loaded.InChatMenuClosePosition.X);
        }
        finally { DeleteDirectory(path); }
    }

    [Theory]
    [InlineData(-1, 0, 100, 100)]
    [InlineData(100, 0, 100, 100)]
    [InlineData(0, 100, 100, 100)]
    [InlineData(0, 0, 0, 100)]
    public void SaveRejectsInvalidViewportPosition(int x, int y, int width, int height)
    {
        var store = new MenuClosePositionStore(NewPath());
        Assert.False(store.Save(MenuCaptureTarget.InChat, new MenuClosePosition { X = x, Y = y, ViewportWidth = width, ViewportHeight = height }));
    }

    [Theory]
    [InlineData(400, 1280, 640, 200)]
    [InlineData(320, 640, 1280, 640)]
    [InlineData(0, 100, 200, 0)]
    public void ViewportScalingUsesCapturedDimensions(int coordinate, int capturedExtent, int currentExtent, int expected) =>
        Assert.Equal(expected, BotEngine.ScaleMenuCoordinate(coordinate, capturedExtent, currentExtent));

    [Fact]
    public void CaptureTargetSelectsOnlyMatchingStoredPosition()
    {
        string path = NewPath();
        try
        {
            var store = new MenuClosePositionStore(path);
            Assert.True(store.Save(MenuCaptureTarget.OutOfChat, Position(80, 90)));
            Assert.True(store.Save(MenuCaptureTarget.InChat, Position(700, 500)));
            MenuClosePositions stored = store.Load();

            Assert.Equal(80, BotEngine.SelectMenuClosePosition(stored, MenuCaptureTarget.OutOfChat).X);
            Assert.Equal(700, BotEngine.SelectMenuClosePosition(stored, MenuCaptureTarget.InChat).X);
            Assert.Equal("F8", Hotkey(MenuCaptureTarget.OutOfChat));
            Assert.Equal("F9", Hotkey(MenuCaptureTarget.InChat));
        }
        finally { DeleteDirectory(path); }
    }

    private static string Hotkey(MenuCaptureTarget target) => target == MenuCaptureTarget.OutOfChat ? "F8" : "F9";

    private static MenuClosePosition Position(int x, int y) => new() { X = x, Y = y, ViewportWidth = 1280, ViewportHeight = 720, CapturedAt = DateTimeOffset.UtcNow };

    private static void AssertPosition(MenuClosePosition expected, MenuClosePosition actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.ViewportWidth, actual.ViewportWidth);
        Assert.Equal(expected.ViewportHeight, actual.ViewportHeight);
        Assert.Equal(expected.CapturedAt, actual.CapturedAt);
    }

    private static string NewPath() => Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"), "menu.json");
    private static void DeleteDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
