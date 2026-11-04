using System;

namespace TelegramAdBot.Models;

public sealed class GlobalMenuButtonPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public bool Configured => X >= 0 && Y >= 0 && ViewportWidth > 0 && ViewportHeight > 0 && X < ViewportWidth && Y < ViewportHeight;
}
