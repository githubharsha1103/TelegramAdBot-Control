using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class ChromeVersion
{
	[JsonPropertyName("webSocketDebuggerUrl")]
	public string WebSocketDebuggerUrl { get; set; } = "";

	[JsonPropertyName("Browser")]
	public string Browser { get; set; } = "";
}
