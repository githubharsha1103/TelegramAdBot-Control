using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class KeyAuthLoginRequest
{
	[JsonPropertyName("type")]
	public string Type { get; set; } = "login";

	[JsonPropertyName("username")]
	public string Username { get; set; } = "";

	[JsonPropertyName("pass")]
	public string Password { get; set; } = "";

	[JsonPropertyName("hwid")]
	public string Hwid { get; set; } = "";

	[JsonPropertyName("sessionid")]
	public string SessionId { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("ownerid")]
	public string OwnerId { get; set; } = "";
}
