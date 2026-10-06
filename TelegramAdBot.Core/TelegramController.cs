using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Core;

public class TelegramController : IAsyncDisposable
{
	private IPlaywright? _playwright;

	private IBrowserContext? _context;

	private IPage? _page;

	private readonly BotConfig _config;

	private readonly int _instanceId;

	private const string SEL_CHAT_LIST = ".chatlist-container,.chat-list,#column-left";

	public TelegramController(BotConfig config, int instanceId)
	{
		_config = config;
		_instanceId = instanceId;
	}

	public async Task<bool> InitializeAsync()
	{
		_ = 3;
		try
		{
			Logger.Step("Starting Playwright...", _instanceId);
			_playwright = await Playwright.CreateAsync();
			string chromiumPath = PlaywrightHelper.FindChromiumPath();
			if (string.IsNullOrEmpty(chromiumPath))
			{
				Logger.Error("Chromium not found.", _instanceId);
				return false;
			}
			string sessionDir = AppDataPaths.ProfilePath(_instanceId);
			int xPos = 50 + (_instanceId - 1) * 60;
			int yPos = 50 + (_instanceId - 1) * 60;
			Logger.Step("Using Chromium: " + chromiumPath, _instanceId);
			Logger.Step($"Launching Chromium ({_config.BrowserWidth}x{_config.BrowserHeight})...", _instanceId);
			_context = await _playwright.Chromium.LaunchPersistentContextAsync(sessionDir, new BrowserTypeLaunchPersistentContextOptions
			{
				Headless = _config.Headless,
				ExecutablePath = chromiumPath,
				Args = new string[12]
				{
					"--no-sandbox",
					"--disable-blink-features=AutomationControlled",
					"--disable-infobars",
					"--disable-dev-shm-usage",
					"--no-first-run",
					"--no-default-browser-check",
					"--disable-features=Translate,AudioServiceOutOfProcess",
					"--disable-background-timer-throttling",
					"--disable-backgrounding-occluded-windows",
					"--disable-renderer-backgrounding",
					$"--window-size={_config.BrowserWidth},{_config.BrowserHeight}",
					$"--window-position={xPos},{yPos}"
				},
				IgnoreDefaultArgs = new string[1] { "--enable-automation" },
				ViewportSize = new ViewportSize
				{
					Width = _config.ViewportWidth,
					Height = _config.ViewportHeight
				}
			});
			IReadOnlyList<IPage> pages = _context.Pages;
			IPage page = ((pages.Count <= 0) ? (await _context.NewPageAsync()) : pages[0]);
			_page = page;
			Logger.Step("Loading Telegram Web...", _instanceId);
			await _page.GotoAsync(_config.TelegramWebUrl, new PageGotoOptions
			{
				WaitUntil = WaitUntilState.NetworkIdle,
				Timeout = 30000f
			});
			Logger.Success("Browser launched.", _instanceId);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Browser init error: " + ex.Message, _instanceId);
			return false;
		}
	}

	private string? GetChromiumPath()
	{
		string playwrightBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
		if (!Directory.Exists(playwrightBase))
		{
			Logger.Warning("Playwright folder not found: " + playwrightBase, _instanceId);
			return null;
		}
		string[] directories = Directory.GetDirectories(playwrightBase, "chromium-*");
		for (int i = 0; i < directories.Length; i++)
		{
			string exePath = Path.Combine(directories[i], "chrome-win", "chrome.exe");
			if (File.Exists(exePath))
			{
				Logger.Info("Found Chromium: " + exePath, _instanceId);
				return exePath;
			}
		}
		directories = Directory.GetDirectories(playwrightBase, "*", SearchOption.AllDirectories);
		for (int i = 0; i < directories.Length; i++)
		{
			string exePath2 = Path.Combine(directories[i], "chrome.exe");
			if (File.Exists(exePath2))
			{
				Logger.Info("Found Chromium: " + exePath2, _instanceId);
				return exePath2;
			}
		}
		Logger.Warning("No Chromium found in: " + playwrightBase, _instanceId);
		return null;
	}

	public static void CloneSessionIfNeeded(int totalInstances)
	{
		string source = AppDataPaths.ProfilePath(1);
		if (!Directory.Exists(source))
		{
			return;
		}
		for (int i = 2; i <= totalInstances; i++)
		{
			string target = AppDataPaths.ProfilePath(i);
			if (!Directory.Exists(target))
			{
				Logger.Info($"Cloning profile -> profile_{i}");
				try
				{
					CopyDirectory(source, target);
				}
				catch (Exception ex)
				{
					Logger.Warning("Clone failed: " + ex.Message);
				}
			}
		}
	}

	private static void CopyDirectory(string src, string dst)
	{
		Directory.CreateDirectory(dst);
		string[] files = Directory.GetFiles(src);
		foreach (string file in files)
		{
			try
			{
				File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
			}
			catch
			{
			}
		}
		files = Directory.GetDirectories(src);
		foreach (string dir in files)
		{
			CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
		}
	}

	public async Task<bool> WaitForLoginAsync()
	{
		_ = 3;
		try
		{
			if (await IsLoggedInAsync())
			{
				Logger.Success("Already logged in.", _instanceId);
				return true;
			}
			Logger.Warning("Waiting for QR code scan...", _instanceId);
			for (int waited = 0; waited < 120000; waited += 1000)
			{
				if (await IsLoggedInAsync())
				{
					Logger.Success("Login detected!", _instanceId);
					await Task.Delay(2000);
					return true;
				}
				await Task.Delay(1000);
			}
			Logger.Error("Login timeout (120s).", _instanceId);
			return false;
		}
		catch (Exception ex)
		{
			Logger.Error("Login error: " + ex.Message, _instanceId);
			return false;
		}
	}

	private async Task<bool> IsLoggedInAsync()
	{
		_ = 1;
		try
		{
			IElementHandle el = await _page.QuerySelectorAsync(".chatlist-container,.chat-list,#column-left");
			bool flag = el != null;
			if (flag)
			{
				flag = await el.IsVisibleAsync();
			}
			return flag;
		}
		catch
		{
			return false;
		}
	}

	public async Task NavigateToBotChatAsync(string botUsername)
	{
		_ = 14;
		try
		{
			if (string.IsNullOrWhiteSpace(botUsername) || _page == null || _page.IsClosed)
			{
				return;
			}
			string clean = botUsername.TrimStart('@');
			Logger.Step("Opening chat: @" + clean, _instanceId);
			try
			{
				if (!_page.Url.Contains("web.telegram.org"))
				{
					await _page.GotoAsync(_config.TelegramWebUrl, new PageGotoOptions
					{
						WaitUntil = WaitUntilState.DOMContentLoaded,
						Timeout = 15000f
					});
					await Task.Delay(2000);
				}
			}
			catch
			{
			}
			for (int i = 0; i < 2; i++)
			{
				try
				{
					await _page.Keyboard.PressAsync("Escape");
				}
				catch
				{
				}
				await Task.Delay(250);
			}
			bool searchClicked = false;
			try
			{
				IElementHandle searchInput = await _page.QuerySelectorAsync("input[type='text']");
				bool flag = searchInput != null;
				if (flag)
				{
					flag = await searchInput.IsVisibleAsync();
				}
				if (flag)
				{
					await searchInput.ClickAsync(new ElementHandleClickOptions
					{
						Timeout = 5000f
					});
					await _page.Keyboard.PressAsync("Control+a");
					await Task.Delay(100);
					await _page.Keyboard.PressAsync("Delete");
					await Task.Delay(200);
					searchClicked = true;
				}
			}
			catch (Exception ex)
			{
				Logger.Warning("Search click failed: " + ex.Message, _instanceId);
			}
			if (!searchClicked)
			{
				Logger.Warning("Search not found — falling back to keyboard shortcut.", _instanceId);
			}
			await _page.Keyboard.TypeAsync("t.me/" + clean, new KeyboardTypeOptions
			{
				Delay = 50f
			});
			await Task.Delay(500);
			await _page.Keyboard.PressAsync("Enter");
			await Task.Delay(2000);
			Logger.Success("Opened @" + clean, _instanceId);
		}
		catch (Exception ex2)
		{
			Logger.Warning("Navigation failed: " + ex2.Message, _instanceId);
		}
	}

	public async Task<(int x, int y)?> PickPositionAsync(string label)
	{
		_ = 5;
		try
		{
			if (_page == null)
			{
				return null;
			}
			Logger.Warning("PICK [" + label + "] — move mouse over target in browser. Capturing in 4s...", _instanceId);
			try
			{
				await _page.BringToFrontAsync();
			}
			catch
			{
			}
			await _page.EvaluateAsync("() => {\r\n                window.__lastMouseX = -1;\r\n                window.__lastMouseY = -1;\r\n                window.__mouseTracker = (e) => {\r\n                    window.__lastMouseX = e.clientX;\r\n                    window.__lastMouseY = e.clientY;\r\n                };\r\n                window.addEventListener('mousemove', window.__mouseTracker, true);\r\n                document.addEventListener('mousemove', window.__mouseTracker, true);\r\n                window.addEventListener('pointermove', window.__mouseTracker, true);\r\n            }");
			for (int i = 4; i > 0; i--)
			{
				Logger.Info($"Move mouse now... {i}", _instanceId);
				await Task.Delay(1000);
			}
			PickPositionResult pos = JsonSerializer.Deserialize(await _page.EvaluateAsync<string>("() => JSON.stringify({x: window.__lastMouseX, y: window.__lastMouseY})"), AppJsonContext.Default.PickPositionResult);
			await _page.EvaluateAsync("() => {\r\n                if (window.__mouseTracker) {\r\n                    window.removeEventListener('mousemove', window.__mouseTracker, true);\r\n                    document.removeEventListener('mousemove', window.__mouseTracker, true);\r\n                    window.removeEventListener('pointermove', window.__mouseTracker, true);\r\n                }\r\n            }");
			if (pos == null || pos.X < 0 || pos.Y < 0)
			{
				Logger.Error("Mouse not detected. Move mouse in browser during countdown!", _instanceId);
				return null;
			}
			await _page.EvaluateAsync("(pos) => {\r\n                const old = document.getElementById('__pickDot');\r\n                if (old) old.remove();\r\n                const dot = document.createElement('div');\r\n                dot.id = '__pickDot';\r\n                dot.style.cssText = 'position:fixed;left:' + pos.x + 'px;top:' + pos.y + 'px;width:20px;height:20px;background:red;border:3px solid yellow;border-radius:50%;z-index:999999;pointer-events:none;box-shadow:0 0 10px red;';\r\n                document.body.appendChild(dot);\r\n                setTimeout(() => dot.remove(), 2500);\r\n            }", new
			{
				x = pos.X,
				y = pos.Y
			});
			Logger.Success($"[{label}] -> ({pos.X},{pos.Y})", _instanceId);
			return (pos.X, pos.Y);
		}
		catch (Exception ex)
		{
			Logger.Error("Pick error: " + ex.Message, _instanceId);
			return null;
		}
	}

	public async Task<bool> ClickAsync(int x, int y)
	{
		try
		{
			if (_page == null)
			{
				return false;
			}
			await _page.Mouse.ClickAsync(x, y);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Click error: " + ex.Message, _instanceId);
			return false;
		}
	}

	public async Task<bool> TypeAsync(string text)
	{
		try
		{
			if (_page == null)
			{
				return false;
			}
			await _page.Keyboard.TypeAsync(text, new KeyboardTypeOptions
			{
				Delay = _config.TypeDelayMs
			});
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Type error: " + ex.Message, _instanceId);
			return false;
		}
	}

	public async Task<bool> PressEnterAsync()
	{
		try
		{
			if (_page == null)
			{
				return false;
			}
			await _page.Keyboard.PressAsync("Enter");
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Enter error: " + ex.Message, _instanceId);
			return false;
		}
	}

	public bool IsBrowserOpen()
	{
		try
		{
			return _page != null && !_page.IsClosed;
		}
		catch
		{
			return false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			Logger.Info($"Closing browser {_instanceId}...", _instanceId);
			if (_context != null)
			{
				await _context.CloseAsync();
				_context = null;
			}
			_playwright?.Dispose();
			_playwright = null;
			_page = null;
			Logger.Info($"Browser {_instanceId} closed.", _instanceId);
		}
		catch (Exception ex)
		{
			Logger.Warning("Dispose error: " + ex.Message, _instanceId);
		}
	}
}
