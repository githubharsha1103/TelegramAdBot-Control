using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class ChromeTargetInfo
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("type")]
	public string Type { get; set; } = "";

	[JsonPropertyName("title")]
	public string Title { get; set; } = "";

	[JsonPropertyName("url")]
	public string Url { get; set; } = "";

	[JsonPropertyName("webSocketDebuggerUrl")]
	public string WebSocketDebuggerUrl { get; set; } = "";
}
