using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class KeyAuthInitRequest
{
	[JsonPropertyName("type")]
	public string Type { get; set; } = "init";

	[JsonPropertyName("ver")]
	public string Version { get; set; } = "";

	[JsonPropertyName("hash")]
	public string Hash { get; set; } = "";

	[JsonPropertyName("enckey")]
	public string EncKey { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("ownerid")]
	public string OwnerId { get; set; } = "";
}
