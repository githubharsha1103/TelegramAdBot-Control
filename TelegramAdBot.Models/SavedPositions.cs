using System.Text.Json.Serialization;

namespace TelegramAdBot.Models;

public class SavedPositions
{
	[JsonPropertyName("TextboxX")]
	public int TextboxX { get; set; }

	[JsonPropertyName("TextboxY")]
	public int TextboxY { get; set; }

	[JsonPropertyName("EmojiButtonX")]
	public int EmojiButtonX { get; set; }

	[JsonPropertyName("EmojiButtonY")]
	public int EmojiButtonY { get; set; }

	[JsonPropertyName("StickerTabX")]
	public int StickerTabX { get; set; }

	[JsonPropertyName("StickerTabY")]
	public int StickerTabY { get; set; }

	[JsonPropertyName("StickerX")]
	public int StickerX { get; set; }

	[JsonPropertyName("StickerY")]
	public int StickerY { get; set; }
}
