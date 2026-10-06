using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace TelegramAdBot.Helpers;

public static class Updater
{
	private const string UPDATE_SUBFOLDER = "updated_exe";

	private const string EXE_NAME = "TelegramAdBot.exe";

	public static async Task<bool> PerformUpdateAsync(UpdateInfo info)
	{
		_ = 1;
		try
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("╔══════════════════════════════════════════════════════╗");
			Console.WriteLine("║              UPDATE AVAILABLE                        ║");
			Console.WriteLine("╠══════════════════════════════════════════════════════╣");
			Console.WriteLine($"║ Current : v{VersionInfo.CurrentString,-40}║");
			Console.WriteLine($"║ New     : v{info.NewVersion,-40}║");
			Console.WriteLine("║                                                      ║");
			Console.WriteLine("║ Downloading and installing automatically...          ║");
			Console.WriteLine("╚══════════════════════════════════════════════════════╝");
			Console.ResetColor();
			Console.WriteLine();
			string updateDir = Path.Combine(AppDataPaths.Root, "updated_exe");
			if (Directory.Exists(updateDir))
			{
				try
				{
					Directory.Delete(updateDir, recursive: true);
				}
				catch
				{
				}
			}
			Directory.CreateDirectory(updateDir);
			string zipPath = Path.Combine(AppDataPaths.Root, "update.zip");
			if (!(await DownloadWithProgressAsync(info.DownloadUrl, zipPath)))
			{
				Logger.Error("Download failed. Continuing with current version.");
				return false;
			}
			Logger.Info("Extracting update...");
			if (!ExtractZip(zipPath, updateDir))
			{
				Logger.Error("Extraction failed. Continuing with current version.");
				return false;
			}
			try
			{
				File.Delete(zipPath);
				Logger.Info("Deleted downloaded zip.");
			}
			catch
			{
			}
			string newExePath = Path.Combine(updateDir, "TelegramAdBot.exe");
			if (!File.Exists(newExePath))
			{
				string found = Directory.GetFiles(updateDir, "TelegramAdBot.exe", SearchOption.AllDirectories).FirstOrDefault();
				if (found == null)
				{
					Logger.Error("TelegramAdBot.exe not found in zip.");
					return false;
				}
				newExePath = found;
			}
			Logger.Success("Update ready. Launching updater...");
			string currentExe = Process.GetCurrentProcess().MainModule?.FileName;
			if (string.IsNullOrEmpty(currentExe))
			{
				Logger.Error("Cannot get current exe path.");
				return false;
			}
			int currentPid = Process.GetCurrentProcess().Id;
			Process.Start(new ProcessStartInfo
			{
				FileName = newExePath,
				UseShellExecute = true,
				Arguments = $"--update --target=\"{currentExe}\" --pid={currentPid}"
			});
			Logger.Success("Updater launched. Closing current app...");
			await Task.Delay(1500);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Update error: " + ex.Message);
			return false;
		}
	}

	public static async Task RunUpdaterModeAsync(string targetExe, int oldPid)
	{
		try
		{
			Console.Title = "Telegram Ad Bot — Updater";
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine();
			Console.WriteLine("╔══════════════════════════════════════════════════════╗");
			Console.WriteLine("║                                                      ║");
			Console.WriteLine("║              INSTALLING UPDATE                       ║");
			Console.WriteLine("║                                                      ║");
			Console.WriteLine("╚══════════════════════════════════════════════════════╝");
			Console.WriteLine($"[1/4] Waiting for old app to close (PID {oldPid})...");
			try
			{
				Process oldProcess = Process.GetProcessById(oldPid);
				if (!oldProcess.HasExited && !oldProcess.WaitForExit(30000))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("✗ Old app did not exit in time.");
					Console.ResetColor();
					await Task.Delay(5000);
					return;
				}
			}
			catch (ArgumentException)
			{
			}
			await Task.Delay(1500);
			Console.WriteLine("✓ Old app closed.");
			Console.WriteLine("[2/4] Replacing exe at: " + targetExe);
			string myPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
			bool replaced = false;
			for (int attempt = 1; attempt <= 5; attempt++)
			{
				try
				{
					string backup = targetExe + ".bak";
					if (File.Exists(backup))
					{
						try
						{
							File.Delete(backup);
						}
						catch
						{
						}
					}
					if (File.Exists(targetExe))
					{
						File.Move(targetExe, backup);
					}
					File.Copy(myPath, targetExe, overwrite: true);
					try
					{
						File.Delete(backup);
					}
					catch
					{
					}
					replaced = true;
				}
				catch (Exception ex2)
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"Attempt {attempt}/5 failed: {ex2.Message}");
					Console.ResetColor();
					await Task.Delay(2000);
					continue;
				}
				break;
			}
			if (!replaced)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("✗ Could not replace exe after 5 attempts.");
				Console.WriteLine("Update aborted. Old version still active.");
				Console.ResetColor();
				await Task.Delay(5000);
				return;
			}
			Console.WriteLine("✓ Exe replaced.");
			Console.WriteLine("[3/4] Launching updated app...");
			Process.Start(new ProcessStartInfo
			{
				FileName = targetExe,
				UseShellExecute = true
			});
			await Task.Delay(2000);
			Console.WriteLine("✓ Updated app launched.");
			Console.WriteLine("[4/4] Cleaning up...");
			ScheduleSelfDelete(myPath);
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine();
			Console.WriteLine("╔══════════════════════════════════════════════════════╗");
			Console.WriteLine("║ ✓ UPDATE COMPLETE!                                   ║");
			Console.WriteLine("║ Closing updater in 3 seconds...                      ║");
			Console.WriteLine("╚══════════════════════════════════════════════════════╝");
			Console.ResetColor();
			await Task.Delay(3000);
		}
		catch (Exception ex3)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Updater error: " + ex3.Message);
			Console.ResetColor();
			await Task.Delay(5000);
		}
	}

	private static async Task<bool> DownloadWithProgressAsync(string url, string destPath)
	{
		_ = 3;
		try
		{
			using HttpClient http = new HttpClient();
			http.DefaultRequestHeaders.Add("User-Agent", "TelegramAdBot-Updater");
			http.Timeout = TimeSpan.FromMinutes(5.0);
			using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
			if (!response.IsSuccessStatusCode)
			{
				Logger.Error($"Download HTTP {(int)response.StatusCode}");
				return false;
			}
			long? totalBytes = response.Content.Headers.ContentLength;
			using Stream stream = await response.Content.ReadAsStreamAsync();
			using FileStream fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
			byte[] buffer = new byte[81920];
			long totalRead = 0L;
			DateTime lastPrint = DateTime.MinValue;
			while (true)
			{
				int num;
				int read = (num = await stream.ReadAsync(buffer));
				if (num <= 0)
				{
					break;
				}
				await fileStream.WriteAsync(buffer.AsMemory(0, read));
				totalRead += read;
				if ((DateTime.Now - lastPrint).TotalMilliseconds > 200.0)
				{
					DrawProgressBar(totalRead, totalBytes);
					lastPrint = DateTime.Now;
				}
			}
			DrawProgressBar(totalRead, totalBytes);
			Console.WriteLine();
			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine();
			Logger.Error("Download error: " + ex.Message);
			return false;
		}
	}

	private static void DrawProgressBar(long current, long? total)
	{
		string currentMB = $"{(double)current / 1024.0 / 1024.0:F1} MB";
		if (total.HasValue && total.Value > 0)
		{
			double pct = (double)current / (double)total.Value;
			int filled = (int)(pct * 40.0);
			string bar = new string('█', filled) + new string('░', 40 - filled);
			string totalMB = $"{(double)total.Value / 1024.0 / 1024.0:F1} MB";
			Console.Write($"\r [{bar}] {pct * 100.0:F0}% {currentMB}/{totalMB}");
		}
		else
		{
			Console.Write("\r Downloading... " + currentMB);
		}
	}

	private static bool ExtractZip(string zipPath, string extractDir)
	{
		try
		{
			ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
			Logger.Success("Extraction complete.");
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Extract error: " + ex.Message);
			return false;
		}
	}

	private static void ScheduleSelfDelete(string exePath)
	{
		try
		{
			string exeDir = Path.GetDirectoryName(exePath) ?? "";
			string batPath = Path.Combine(exeDir, "_cleanup.bat");
			string batContent = "@echo off\r\ntimeout /t 3 /nobreak >nul\r\ndel /f /q \"" + exePath + "\"\r\nrmdir /s /q \"" + exeDir + "\" 2>nul\r\ndel \"%~f0\"\r\n";
			File.WriteAllText(batPath, batContent);
			Process.Start(new ProcessStartInfo
			{
				FileName = batPath,
				UseShellExecute = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
		}
		catch
		{
		}
	}
}
