namespace TelegramAdBot.Models;

public sealed class BotMenuButtonPosition
{
	public string BotIdentifier { get; set; } = "";
	public int X { get; set; }
	public int Y { get; set; }
	public int ViewportWidth { get; set; }
	public int ViewportHeight { get; set; }
	public bool Configured { get; set; }
}

public enum ReplyKeyboardMenuState
{
	Unknown,
	Open,
	Closed
}
