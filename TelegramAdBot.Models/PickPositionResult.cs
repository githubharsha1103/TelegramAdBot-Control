using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class PickPositionResult
{
	[JsonPropertyName("x")]
	public int X { get; set; }

	[JsonPropertyName("y")]
	public int Y { get; set; }
}
