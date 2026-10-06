using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class KeyAuthResponse
{
	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = "";

	[JsonPropertyName("sessionid")]
	public string SessionId { get; set; } = "";

	[JsonPropertyName("nonce")]
	public string Nonce { get; set; } = "";

	[JsonPropertyName("code")]
	public int Code { get; set; }
}
