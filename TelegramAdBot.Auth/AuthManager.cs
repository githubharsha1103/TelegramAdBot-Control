using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TelegramAdBot.Core;
using TelegramAdBot.Helpers;

namespace TelegramAdBot.Auth;

public class AuthManager
{
	private readonly KeyAuthClient _client = new KeyAuthClient();

	private const int MAX_RETRIES = 3;

	public async Task<bool> AuthenticateAsync()
	{
		ConsoleUI.PrintAuthBanner();
		(string username, string password)? saved = LoadCredentials();
		if (saved.HasValue)
		{
			Logger.Info("Found saved credentials. Attempting auto-login...");
			if (await TryLoginWithRetryAsync(saved.Value.username, saved.Value.password, silent: true))
			{
				ConsoleUI.PrintAuthSuccess(saved.Value.username);
				return true;
			}
			Logger.Warning("Auto-login failed. Clearing saved credentials.");
			DeleteCredentials();
		}
		return await ManualLoginAsync();
	}

	private async Task<bool> ManualLoginAsync()
	{
		ConsoleUI.PrintLoginPrompt();
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.Write("  Username: ");
		Console.ResetColor();
		string username = Console.ReadLine()?.Trim() ?? "";
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.Write("  Password: ");
		Console.ResetColor();
		string password = ReadPassword();
		Console.WriteLine();
		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
		{
			Logger.Error("Username and password cannot be empty.");
			return false;
		}
		if (await TryLoginWithRetryAsync(username, password, silent: false))
		{
			SaveCredentials(username, password);
			ConsoleUI.PrintAuthSuccess(username);
			return true;
		}
		Logger.Error("Login failed after all retries. Please restart and try again.");
		return false;
	}

	private async Task<bool> TryLoginWithRetryAsync(string username, string password, bool silent)
	{
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			if (!silent)
			{
				Logger.Info($"Login attempt {attempt}/{3}...");
			}
			else
			{
				Logger.Info($"Auto-login attempt {attempt}/{3}...");
			}
			if (!(await _client.InitializeAsync()))
			{
				Logger.Warning("KeyAuth server unreachable.");
				if (attempt < 3)
				{
					Logger.Info("Retrying in 3 seconds...");
					await Task.Delay(3000);
				}
				continue;
			}
			var (success, message) = await _client.LoginAsync(username, password);
			if (success)
			{
				Logger.Success("Authenticated successfully!");
				return true;
			}
			Logger.Error("Login failed: " + message);
			if (attempt < 3)
			{
				Logger.Info($"Retrying in 3 seconds... ({3 - attempt} left)");
				await Task.Delay(3000);
			}
		}
		return false;
	}

	private static (string username, string password)? LoadCredentials()
	{
		try
		{
			string path = AppDataPaths.CredentialsFile;
			if (!File.Exists(path))
			{
				return null;
			}
			string[] array = File.ReadAllLines(path);
			string username = null;
			string password = null;
			string[] array2 = array;
			foreach (string line in array2)
			{
				if (line.StartsWith("username="))
				{
					username = line.Substring("username=".Length).Trim();
				}
				else if (line.StartsWith("password="))
				{
					password = line.Substring("password=".Length).Trim();
				}
			}
			if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
			{
				return (username, password);
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	private static void SaveCredentials(string username, string password)
	{
		try
		{
			AppDataPaths.EnsureDirectories();
			File.WriteAllLines(AppDataPaths.CredentialsFile, new string[2]
			{
				"username=" + username,
				"password=" + password
			});
			Logger.Success("Credentials saved.");
		}
		catch (Exception ex)
		{
			Logger.Warning("Could not save credentials: " + ex.Message);
		}
	}

	private static void DeleteCredentials()
	{
		try
		{
			if (File.Exists(AppDataPaths.CredentialsFile))
			{
				File.Delete(AppDataPaths.CredentialsFile);
			}
		}
		catch
		{
		}
	}

	private static string ReadPassword()
	{
		StringBuilder sb = new StringBuilder();
		ConsoleKeyInfo key;
		do
		{
			key = Console.ReadKey(intercept: true);
			if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
			{
				sb.Remove(sb.Length - 1, 1);
				Console.Write("\b \b");
			}
			else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
			{
				sb.Append(key.KeyChar);
				Console.Write('*');
			}
		}
		while (key.Key != ConsoleKey.Enter);
		return sb.ToString();
	}
}
