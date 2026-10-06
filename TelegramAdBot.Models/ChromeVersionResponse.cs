using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class ChromeVersionResponse
{
	[JsonPropertyName("Browser")]
	public string Browser { get; set; } = "";

	[JsonPropertyName("Protocol-Version")]
	public string ProtocolVersion { get; set; } = "";

	[JsonPropertyName("User-Agent")]
	public string UserAgent { get; set; } = "";

	[JsonPropertyName("V8-Version")]
	public string V8Version { get; set; } = "";

	[JsonPropertyName("WebKit-Version")]
	public string WebKitVersion { get; set; } = "";

	[JsonPropertyName("webSocketDebuggerUrl")]
	public string WebSocketDebuggerUrl { get; set; } = "";
}
