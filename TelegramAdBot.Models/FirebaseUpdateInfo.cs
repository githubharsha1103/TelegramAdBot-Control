using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class FirebaseUpdateInfo
{
	[JsonPropertyName("version")]
	public string Version { get; set; } = "";

	[JsonPropertyName("downloadUrl")]
	public string DownloadUrl { get; set; } = "";

	[JsonPropertyName("notes")]
	public string Notes { get; set; } = "";
}
