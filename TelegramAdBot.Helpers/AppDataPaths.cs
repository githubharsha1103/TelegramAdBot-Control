using System;
using System.IO;

namespace TelegramAdBot.Helpers;

public static class AppDataPaths
{
	public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tgADbot");

	public static string ExeFolder => AppDomain.CurrentDomain.BaseDirectory;

	public static string CredentialsFile => Path.Combine(Root, "credentials.txt");

	public static string ConfigFile => Path.Combine(Root, "config.json");

	public static string BotConfigFile => Path.Combine(Root, "bots.json");

	public static string SetupFlagFile => Path.Combine(Root, "setup_done.flag");

	public static string BotsFile => Path.Combine(ExeFolder, "bots.txt");

	public static string LogsFolder => Path.Combine(Root, "logs");

	public static string ProfilesFolder => Path.Combine(Root, "profiles");

	public static string ProfilePath(int instanceId)
	{
		return Path.Combine(ProfilesFolder, $"profile_{instanceId}");
	}

	public static void EnsureDirectories()
	{
		Directory.CreateDirectory(Root);
		Directory.CreateDirectory(LogsFolder);
		Directory.CreateDirectory(ProfilesFolder);
	}
}
