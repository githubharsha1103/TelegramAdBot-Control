using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class CdpResponse
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("result")]
	public JsonElement Result { get; set; }

	[JsonPropertyName("error")]
	public CdpError? Error { get; set; }

	[JsonPropertyName("method")]
	public string Method { get; set; } = "";

	[JsonPropertyName("params")]
	public JsonElement? Params { get; set; }

	[JsonPropertyName("sessionId")]
	public string SessionId { get; set; } = "";
}
