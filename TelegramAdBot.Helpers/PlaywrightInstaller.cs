using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TelegramAdBot.Helpers;

public static class PlaywrightInstaller
{
	public static async Task<bool> InstallChromiumAsync()
	{
		_ = 3;
		try
		{
			Logger.Info("Installing Chromium via Playwright...");
			string dotnetPath = "dotnet";
			string toolArgs = "tool install --global Microsoft.Playwright.CLI";
			Process toolProcess = Process.Start(new ProcessStartInfo
			{
				FileName = dotnetPath,
				Arguments = toolArgs,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			});
			if (toolProcess != null)
			{
				await toolProcess.WaitForExitAsync();
			}
			string args = "playwright install chromium";
			Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			});
			if (process != null)
			{
				string output = await process.StandardOutput.ReadToEndAsync();
				string error = await process.StandardError.ReadToEndAsync();
				await process.WaitForExitAsync();
				Logger.Info("Playwright output: " + output);
				if (!string.IsNullOrEmpty(error))
				{
					Logger.Warning("Playwright errors: " + error);
				}
				return process.ExitCode == 0;
			}
			return false;
		}
		catch (Exception ex)
		{
			Logger.Error("Playwright install error: " + ex.Message);
			return false;
		}
	}
}
