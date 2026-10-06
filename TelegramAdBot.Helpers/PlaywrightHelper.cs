using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TelegramAdBot.Helpers;

public static class PlaywrightHelper
{
    private static readonly object _lock = new object();
    private static bool _installed = false;

    public static async Task<bool> EnsureChromiumInstalledAsync()
    {
        string? existingPath = FindChromiumPath();

        if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath))
        {
            Logger.Success("Chromium found at: " + existingPath);
            return true;
        }

        lock (_lock)
        {
            if (_installed)
            {
                Logger.Info("Chromium installation already attempted.");
                return FindChromiumPath() != null;
            }

            _installed = true;
        }

        Logger.Warning("Chromium not found.");

        // Linux VPS: don't try the Windows PowerShell installer.
        if (OperatingSystem.IsLinux())
        {
            Logger.Error("Chromium was not found in the Linux Playwright cache.");
            Logger.Error("Expected path: /root/.cache/ms-playwright/");
            return false;
        }

        Logger.Warning("Installing Chromium via PowerShell...");
        Logger.Info("This will download Chromium (~150 MB)");

        try
        {
            if (await InstallViaPowerShellAsync())
            {
                Logger.Success("Chromium installed successfully!");
                return true;
            }

            Logger.Error("Chromium installation failed.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Installation error: " + ex.Message);
            return false;
        }
    }

    private static async Task<bool> InstallViaPowerShellAsync()
    {
        try
        {
            string script = @"
$env:PLAYWRIGHT_BROWSERS_PATH = $env:USERPROFILE + '\AppData\Local\ms-playwright'
if (-not (Get-Command playwright -ErrorAction SilentlyContinue)) {
    dotnet tool install --global Microsoft.Playwright.CLI --version 1.48.0 2>$null
}
playwright install chromium
";

            string tempScript = Path.GetTempFileName() + ".ps1";
            await File.WriteAllTextAsync(tempScript, script);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-ExecutionPolicy Bypass -File \"" + tempScript + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Logger.Info("Running PowerShell installer...");

            using Process process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            try
            {
                File.Delete(tempScript);
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(output))
                Logger.Info("Installer output: " + output.Trim());

            if (!string.IsNullOrEmpty(error))
                Logger.Warning("Installer errors: " + error.Trim());

            return FindChromiumPath() != null;
        }
        catch (Exception ex)
        {
            Logger.Error("PowerShell install failed: " + ex.Message);
            return false;
        }
    }

    public static string? FindChromiumPath()
    {
        try
        {
            // Linux Playwright browser location
            if (OperatingSystem.IsLinux())
            {
                string home = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);

                string[] linuxBases =
                {
                    Path.Combine(home, ".cache", "ms-playwright"),
                    "/root/.cache/ms-playwright"
                };

                foreach (string playwrightBase in linuxBases)
                {
                    if (!Directory.Exists(playwrightBase))
                        continue;

                    string[] directories =
                        Directory.GetDirectories(
                            playwrightBase,
                            "chromium-*");

                    foreach (string dir in directories)
                    {
                        string chromePath = Path.Combine(
                            dir,
                            "chrome-linux",
                            "chrome");

                        if (File.Exists(chromePath))
                        {
                            return chromePath;
                        }

                        // Also support newer Playwright Linux layouts.
                        string chromePath2 = Path.Combine(
                            dir,
                            "chrome-linux64",
                            "chrome");

                        if (File.Exists(chromePath2))
                        {
                            return chromePath2;
                        }
                    }
                }

                return null;
            }

            // Windows Playwright browser location
            string windowsBase = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ms-playwright");

            if (!Directory.Exists(windowsBase))
                return null;

            string[] windowsDirectories =
                Directory.GetDirectories(
                    windowsBase,
                    "chromium-*");

            foreach (string dir in windowsDirectories)
            {
                string exePath = Path.Combine(
                    dir,
                    "chrome-win",
                    "chrome.exe");

                if (File.Exists(exePath))
                    return exePath;

                exePath = Path.Combine(dir, "chrome.exe");

                if (File.Exists(exePath))
                    return exePath;
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning("Error finding Chromium: " + ex.Message);
            return null;
        }
    }
}
