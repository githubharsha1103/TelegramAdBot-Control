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

	[JsonPropertyName("AccountMenuX")] public int AccountMenuX { get; set; } = 80;
	[JsonPropertyName("AccountMenuY")] public int AccountMenuY { get; set; } = 274;
	[JsonPropertyName("Account1X")] public int Account1X { get; set; } = 134;
	[JsonPropertyName("Account1Y")] public int Account1Y { get; set; } = 321;
	[JsonPropertyName("Account2X")] public int Account2X { get; set; } = 142;
	[JsonPropertyName("Account2Y")] public int Account2Y { get; set; } = 358;
	[JsonPropertyName("Account3X")] public int Account3X { get; set; } = 152;
	[JsonPropertyName("Account3Y")] public int Account3Y { get; set; } = 392;
}
