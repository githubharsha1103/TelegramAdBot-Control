using System;

namespace TelegramAdBot.Helpers;

public class UpdateInfo
{
	public required Version NewVersion { get; init; }

	public required string DownloadUrl { get; init; }

	public required string ReleaseNotes { get; init; }
}
