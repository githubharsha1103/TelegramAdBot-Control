using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TelegramAdBot.Models;

namespace TelegramAdBot.Helpers;

public static class UpdateChecker
{
	private const string FIREBASE_URL = "https://your-firebase-db.firebaseio.com/update.json";

	public static async Task<UpdateInfo?> CheckAsync()
	{
		_ = 1;
		try
		{
			Logger.Info("Current version: v" + VersionInfo.CurrentString);
			Logger.Info("Checking Firebase for updates...");
			using HttpClient http = new HttpClient();
			http.DefaultRequestHeaders.Add("User-Agent", "TelegramAdBot");
			http.Timeout = TimeSpan.FromSeconds(10.0);
			HttpResponseMessage response = await http.GetAsync("https://your-firebase-db.firebaseio.com/update.json");
			if (!response.IsSuccessStatusCode)
			{
				Logger.Warning($"Firebase returned {(int)response.StatusCode}. Skipping.");
				return null;
			}
			string json = await response.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(json) || json == "null")
			{
				Logger.Warning("Firebase returned no data.");
				return null;
			}
			FirebaseUpdateInfo info = JsonSerializer.Deserialize(json, AppJsonContext.Default.FirebaseUpdateInfo);
			if (info == null)
			{
				Logger.Warning("Failed to parse Firebase response.");
				return null;
			}
			if (string.IsNullOrEmpty(info.Version))
			{
				Logger.Warning("Firebase data missing 'version' field.");
				return null;
			}
			Version latestVer = VersionInfo.Parse(info.Version);
			if (latestVer == null)
			{
				Logger.Warning("Cannot parse version: " + info.Version);
				return null;
			}
			Logger.Info($"Latest version: v{latestVer}");
			if (latestVer <= VersionInfo.Current)
			{
				Logger.Success("Already on latest version.");
				return null;
			}
			if (string.IsNullOrEmpty(info.DownloadUrl))
			{
				Logger.Warning("Empty downloadUrl in Firebase.");
				return null;
			}
			Logger.Success($"New version available: v{latestVer}");
			if (!string.IsNullOrEmpty(info.Notes))
			{
				Logger.Info("Release notes: " + info.Notes);
			}
			return new UpdateInfo
			{
				NewVersion = latestVer,
				DownloadUrl = info.DownloadUrl,
				ReleaseNotes = (info.Notes ?? "")
			};
		}
		catch (Exception ex)
		{
			Logger.Warning("Update check failed: " + ex.Message);
			return null;
		}
	}
}
