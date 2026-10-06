using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class CdpError
{
	[JsonPropertyName("code")]
	public int Code { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = "";
}
