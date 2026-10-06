using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Auth;

public class KeyAuthClient
{
	private const string APP_NAME = "telegramAdBot";

	private const string OWNER_ID = "2RrciWEGG2";

	private const string SECRET = "9931af1b6cce2bc79acdb0cba3da971035315d96ad38979e817d520f7ef7c15e";

	private const string VERSION = "1.1";

	private const string API_URL = "https://keyauth.win/api/1.2/";

	private readonly HttpClient _http;

	private string _sessionId = "";

	public KeyAuthClient()
	{
		_http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(15.0)
		};
	}

	public async Task<bool> InitializeAsync()
	{
		try
		{
			string body = BuildFormRequest(new KeyAuthInitRequest
			{
				Version = "1.1",
				Hash = "9931af1b6cce2bc79acdb0cba3da971035315d96ad38979e817d520f7ef7c15e",
				EncKey = Guid.NewGuid().ToString("N"),
				Name = "telegramAdBot",
				OwnerId = "2RrciWEGG2"
			});
			JsonElement? response = await PostAsync(body);
			if (!response.HasValue)
			{
				Logger.Error("KeyAuth: No response from server.");
				return false;
			}
			JsonElement root = response.Value;
			if (root.TryGetProperty("sessionid", out var sid))
			{
				_sessionId = sid.GetString() ?? "";
			}
			JsonElement s;
			bool num = root.TryGetProperty("success", out s) && s.GetBoolean();
			if (num)
			{
				Logger.Info("KeyAuth initialized successfully.");
			}
			else
			{
				JsonElement m;
				string msg = (root.TryGetProperty("message", out m) ? (m.GetString() ?? "Unknown error") : "Unknown error");
				Logger.Error("KeyAuth init failed: " + msg);
			}
			return num;
		}
		catch (Exception ex)
		{
			Logger.Error("KeyAuth init error: " + ex.Message);
			return false;
		}
	}

	public async Task<(bool success, string message)> LoginAsync(string username, string password)
	{
		_ = 1;
		try
		{
			if (string.IsNullOrEmpty(_sessionId) && !(await InitializeAsync()))
			{
				return (success: false, message: "Failed to initialize KeyAuth.");
			}
			string body = BuildFormRequest(new KeyAuthLoginRequest
			{
				Username = username,
				Password = password,
				Hwid = GetHWID(),
				SessionId = _sessionId,
				Name = "telegramAdBot",
				OwnerId = "2RrciWEGG2"
			});
			JsonElement? response = await PostAsync(body);
			if (!response.HasValue)
			{
				return (success: false, message: "No response from KeyAuth.");
			}
			JsonElement root = response.Value;
			JsonElement s;
			bool success = root.TryGetProperty("success", out s) && s.GetBoolean();
			JsonElement m;
			string message = (root.TryGetProperty("message", out m) ? (m.GetString() ?? "") : "");
			return (success: success, message: message);
		}
		catch (Exception ex)
		{
			Logger.Error("Login error: " + ex.Message);
			return (success: false, message: "Exception: " + ex.Message);
		}
	}

	private async Task<JsonElement?> PostAsync(string body)
	{
		_ = 1;
		try
		{
			StringContent content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
			string json = await (await _http.PostAsync("https://keyauth.win/api/1.2/", content)).Content.ReadAsStringAsync();
			Logger.Info("KeyAuth response: " + json);
			JsonDocument doc = JsonDocument.Parse(json);
			return doc.RootElement.Clone();
		}
		catch (Exception ex)
		{
			Logger.Error("HTTP error: " + ex.Message);
			return null;
		}
	}

	private static string GetHWID()
	{
		try
		{
			if (OperatingSystem.IsWindows())
			{
				RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography");
				if (key != null)
				{
					string guid = key.GetValue("MachineGuid")?.ToString();
					if (!string.IsNullOrEmpty(guid))
					{
						return guid;
					}
				}
			}
		}
		catch
		{
		}
		string raw = Environment.MachineName + "-" + Environment.UserName;
		using SHA256 sha = SHA256.Create();
		return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Substring(0, 32).ToLower();
	}

	private static string BuildFormRequest(KeyAuthInitRequest req)
	{
		StringBuilder stringBuilder2;
		StringBuilder stringBuilder = (stringBuilder2 = new StringBuilder());
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
		handler.AppendLiteral("type=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Type));
		stringBuilder3.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
		handler.AppendLiteral("&ver=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Version));
		stringBuilder4.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("&hash=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Hash));
		stringBuilder5.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
		handler.AppendLiteral("&enckey=");
		handler.AppendFormatted(Uri.EscapeDataString(req.EncKey));
		stringBuilder6.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("&name=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Name));
		stringBuilder7.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
		handler.AppendLiteral("&ownerid=");
		handler.AppendFormatted(Uri.EscapeDataString(req.OwnerId));
		stringBuilder8.Append(ref handler);
		return stringBuilder.ToString();
	}

	private static string BuildFormRequest(KeyAuthLoginRequest req)
	{
		StringBuilder stringBuilder2;
		StringBuilder stringBuilder = (stringBuilder2 = new StringBuilder());
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
		handler.AppendLiteral("type=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Type));
		stringBuilder3.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("&username=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Username));
		stringBuilder4.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("&pass=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Password));
		stringBuilder5.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("&hwid=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Hwid));
		stringBuilder6.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
		handler.AppendLiteral("&sessionid=");
		handler.AppendFormatted(Uri.EscapeDataString(req.SessionId));
		stringBuilder7.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("&name=");
		handler.AppendFormatted(Uri.EscapeDataString(req.Name));
		stringBuilder8.Append(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder9 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
		handler.AppendLiteral("&ownerid=");
		handler.AppendFormatted(Uri.EscapeDataString(req.OwnerId));
		stringBuilder9.Append(ref handler);
		return stringBuilder.ToString();
	}
}
