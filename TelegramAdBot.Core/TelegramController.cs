using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Core;

public class TelegramController : IAsyncDisposable
{
    public enum ReplyKeyboardMenuState { Unknown, Open, Closed }
	    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XFree(nint data);
    [DllImport("libX11.so.6")] private static extern int XFetchName(nint display, nint window, out nint windowName);
    [DllImport("libX11.so.6")] private static extern nint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern int XQueryPointer(nint display, nint window, out nint root, out nint child, out int rootX, out int rootY, out int windowX, out int windowY, out uint mask);
    [DllImport("libX11.so.6")] private static extern int XTranslateCoordinates(nint display, nint source, nint destination, int srcX, int srcY, out int destX, out int destY, out nint child);
    private readonly nint _display = OperatingSystem.IsLinux() ? XOpenDisplay(nint.Zero) : nint.Zero;
	private IPlaywright? _playwright;

	private IBrowserContext? _context;

	private IPage? _page;
	private readonly System.Threading.SemaphoreSlim _disposeLock = new(1, 1);
	private bool _disposeCompleted;

	private readonly BotConfig _config;

	private readonly int _instanceId;

	private const string SEL_CHAT_LIST = ".chatlist-container,.chat-list,#column-left";

	public bool HasResources => _page != null || _context != null || _playwright != null;

	public bool IsDisposed => _disposeCompleted && !HasResources;

	public TelegramController(BotConfig config, int instanceId)
	{
		_config = config;
		_instanceId = instanceId;
	}

	private async Task BrowserDiagnosticAsync(string action, string stage, bool screenshot = false)
	{
		try
		{
			string url = _page == null || _page.IsClosed ? "<unavailable>" : _page.Url;
			string focus = "<unavailable>";
			string chatVisible = "unknown";
			if (_page != null && !_page.IsClosed)
			{
				focus = await _page.EvaluateAsync<string>("() => { const e=document.activeElement; return JSON.stringify({pageHasFocus:document.hasFocus(),active:e?{tag:e.tagName,id:e.id||'',className:typeof e.className==='string'?e.className:'',name:e.getAttribute('aria-label')||e.getAttribute('title')||e.innerText||e.getAttribute('placeholder')||''}:null}); }");
				chatVisible = await _page.Locator(SEL_CHAT_LIST).First.IsVisibleAsync() ? "visible" : "not-visible";
			}
			string stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
			Logger.Info($"[BrowserDiag] {stamp} | {action} | {stage} | URL={url} | active={focus} | chatList={chatVisible}", _instanceId);
			if (screenshot && _page != null && !_page.IsClosed)
			{
				Directory.CreateDirectory(AppDataPaths.LogsFolder);
				string safe = string.Concat(action.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
				string path = Path.Combine(AppDataPaths.LogsFolder, $"browser_{_instanceId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safe}_{stage}.png");
				await _page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = false });
				Logger.Info($"[BrowserDiag] Screenshot {stage}: {path}", _instanceId);
			}
		}
		catch (Exception ex)
		{
			Logger.Warning($"[BrowserDiag] Diagnostic capture failed for {action} ({stage}): {ex.Message}", _instanceId);
		}
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
			int xPos = 50 + ((_instanceId - 1) % 10) * 60;
			int yPos = 50 + (((_instanceId - 1) / 10) % 10) * 60;
			Logger.Step("Using Chromium: " + chromiumPath, _instanceId);
			Logger.Step($"Launching Chromium ({_config.BrowserWidth}x{_config.BrowserHeight})...", _instanceId);
			await BrowserDiagnosticAsync("Browser launch", "before");
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
			Logger.Info("[Browser] Chromium started.", _instanceId);
			IReadOnlyList<IPage> pages = _context.Pages;
			IPage page = ((pages.Count <= 0) ? (await _context.NewPageAsync()) : pages[0]);
			_page = page;
			await BrowserDiagnosticAsync("Browser launched", "after");
			Logger.Step("Loading Telegram Web...", _instanceId);
			await _page.GotoAsync(_config.TelegramWebUrl, new PageGotoOptions
			{
				WaitUntil = WaitUntilState.NetworkIdle,
				Timeout = 30000f
			});
			Logger.Info("[Browser] Telegram Web navigation completed.", _instanceId);
			await BrowserDiagnosticAsync("Telegram opened", "after", screenshot: true);
			Logger.Info("[Browser] Waiting 5 seconds for Telegram Web.", _instanceId);
			await Task.Delay(TimeSpan.FromSeconds(5));
			Logger.Success("[Browser] Telegram Web startup wait completed.", _instanceId);
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
		await TryNavigateToBotChatAsync(botUsername);
	}

	public async Task<bool> TryNavigateToBotChatAsync(string botUsername, bool waitAfterEnter = true)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(botUsername) || _page == null || _page.IsClosed)
			{
				return false;
			}
			string clean = botUsername.TrimStart('@');
			Logger.Step("Opening chat: @" + clean, _instanceId);
			await BrowserDiagnosticAsync($"Bot search started @{clean}", "before");
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
					await BrowserDiagnosticAsync("Bot search input click locator=input[type='text']", "before");
					await searchInput.ClickAsync(new ElementHandleClickOptions
					{
						Timeout = 5000f
					});
					await BrowserDiagnosticAsync("Bot search input click locator=input[type='text']", "after");
					await BrowserDiagnosticAsync("Bot search select all Control+a", "before");
					await _page.Keyboard.PressAsync("Control+a");
					await BrowserDiagnosticAsync("Bot search select all Control+a", "after");
					await Task.Delay(100);
					await BrowserDiagnosticAsync("Bot search clear Delete", "before");
					await _page.Keyboard.PressAsync("Delete");
					await BrowserDiagnosticAsync("Bot search clear Delete", "after");
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
			await BrowserDiagnosticAsync($"Bot search text typed t.me/{clean}", "before");
			await _page.Keyboard.TypeAsync("t.me/" + clean, new KeyboardTypeOptions
			{
				Delay = 50f
			});
			await BrowserDiagnosticAsync($"Bot search text typed t.me/{clean}", "after");
			await Task.Delay(500);
			await BrowserDiagnosticAsync("Bot search submitted Enter", "before");
			await _page.Keyboard.PressAsync("Enter");
			await BrowserDiagnosticAsync("Bot search submitted Enter", "after");
			await _page.Locator(SEL_CHAT_LIST).First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
			if (waitAfterEnter) await Task.Delay(2000);
			await BrowserDiagnosticAsync($"Bot search completed @{clean}; expected chat list visible", "after");
			await BrowserDiagnosticAsync($"Bot conversation entered @{clean}; expected chat list visible", "after", screenshot: true);
			Logger.Success("Opened @" + clean, _instanceId);
			return true;
		}
		catch (Exception ex2)
		{
			Logger.Warning("Navigation failed: " + ex2.Message, _instanceId);
			return false;
		}
	}

	public static bool IsExpectedBotConversationUrl(string actualUrl, string telegramWebUrl, string botUsername)
	{
		if (string.IsNullOrWhiteSpace(actualUrl) || string.IsNullOrWhiteSpace(telegramWebUrl) || string.IsNullOrWhiteSpace(botUsername)) return false;
		if (!Uri.TryCreate(actualUrl, UriKind.Absolute, out Uri? actual) || !Uri.TryCreate(telegramWebUrl, UriKind.Absolute, out Uri? webBase)) return false;
		string expectedIdentifier = "@" + botUsername.Trim().TrimStart('@');
		string actualIdentifier;
		try { actualIdentifier = Uri.UnescapeDataString(actual.Fragment.TrimStart('#')); }
		catch { return false; }
		return string.Equals(actual.Scheme, webBase.Scheme, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(actual.Host, webBase.Host, StringComparison.OrdinalIgnoreCase) &&
			actual.Port == webBase.Port &&
			string.Equals(actual.AbsolutePath.TrimEnd('/'), webBase.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
			string.Equals(actualIdentifier, expectedIdentifier, StringComparison.OrdinalIgnoreCase);
	}

	public async Task<bool> VerifyExpectedBotConversationAsync(string botUsername, bool requireMessageInput)
	{
		string actualUrl = _page == null || _page.IsClosed ? "<unavailable>" : _page.Url;
		string expectedUrl = BuildExpectedBotConversationUrl(botUsername);
		Logger.Info($"[Recovery] URL verification started. Expected={expectedUrl}; Actual={actualUrl}", _instanceId);
		bool matches = IsExpectedBotConversationUrl(actualUrl, _config.TelegramWebUrl, botUsername);
		if (!matches)
		{
			Logger.Warning($"[Recovery] Conversation URL mismatch. Expected={expectedUrl}; Actual={actualUrl}", _instanceId);
			return false;
		}

		if (requireMessageInput)
		{
			try
			{
				if (_page == null || _page.IsClosed) return false;
				await _page.Locator(".input-message-input[contenteditable='true']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
			}
			catch (Exception ex)
			{
				Logger.Warning($"[Recovery] Expected conversation URL matched but message input is unavailable: {ex.Message}", _instanceId);
				return false;
			}
		}

		Logger.Success($"[Recovery] Conversation verified successfully: {expectedUrl}", _instanceId);
		return true;
	}

	public async Task<bool> VerifyStickerPickerReadyAsync()
	{
		try
		{
			if (_page == null || _page.IsClosed) return false;
			await _page.Locator(".emoji-dropdown").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
			await _page.Locator(".emoji-dropdown .emoji-tabs-stickers.active").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
			Logger.Success("[Recovery] Sticker picker is visible and the Stickers tab is active.", _instanceId);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warning($"[Recovery] Sticker picker readiness check failed: {ex.Message}", _instanceId);
			return false;
		}
	}

	public async Task CaptureRecoveryFailureAsync(string reason)
	{
		await BrowserDiagnosticAsync("Conversation recovery failed: " + reason, "recovery-failure", screenshot: true);
	}

	private string BuildExpectedBotConversationUrl(string botUsername)
	{
		if (!Uri.TryCreate(_config.TelegramWebUrl, UriKind.Absolute, out Uri? webBase)) return "<invalid TelegramWebUrl>";
		return webBase.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/#@" + botUsername.Trim().TrimStart('@');
	}

	public async Task<bool> VerifyStartupChatAndFocusMessageInputAsync(string botUsername)
	{
		ILocator? messageInput = null;
		string urlBeforeFocus = "<unavailable>";
		try
		{
			if (_page == null || _page.IsClosed) return false;
			Logger.Info($"[StartupDiag] {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | Waiting exactly 3 seconds for @{botUsername} conversation.", _instanceId);
			await Task.Delay(TimeSpan.FromSeconds(3));
			await _page.BringToFrontAsync();
			messageInput = _page.Locator(".input-message-input[contenteditable='true']");
			await messageInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
			urlBeforeFocus = _page.Url;
			await BrowserDiagnosticAsync($"Startup bot conversation @{botUsername} active; message input visible", "before-focus", screenshot: true);
			await messageInput.FocusAsync();
			await BrowserDiagnosticAsync("Startup message input FocusAsync completed (active element captured immediately)", "after-focus", screenshot: true);
			bool inputHasFocus = await messageInput.EvaluateAsync<bool>("element => document.activeElement === element");
			bool conversationStillOpen = !_page.IsClosed && _page.Url == urlBeforeFocus && await messageInput.IsVisibleAsync();
			bool expectedBotRoute = Uri.TryCreate(urlBeforeFocus, UriKind.Absolute, out Uri? activeUri) &&
				string.Equals(activeUri.Fragment, "#@" + botUsername.TrimStart('@'), StringComparison.OrdinalIgnoreCase);
			if (!inputHasFocus || !conversationStillOpen || !expectedBotRoute)
			{
				await BrowserDiagnosticAsync($"Startup message-input focus or conversation verification failed for @{botUsername}; startup halted", "failure", screenshot: true);
				Logger.Error($"[StartupDiag] Message-input focus or conversation verification failed for @{botUsername}. Startup halted; no recovery click will be attempted.", _instanceId);
				return false;
			}
			Logger.Info($"[StartupDiag] Startup chat @{botUsername} remains open; message input is active.", _instanceId);
			return true;
		}
		catch (Exception ex)
		{
			await BrowserDiagnosticAsync($"Startup chat/input verification failed for @{botUsername}: {ex.Message}", "failure", screenshot: true);
			Logger.Error($"[StartupDiag] Startup chat/input verification failed for @{botUsername}: {ex.Message}", _instanceId);
			return false;
		}
	}

	public async Task<bool> WaitForTelegramReadyAsync(int timeoutMs = 15000)
	{
		try
		{
			if (_page == null || _page.IsClosed) return false;
			await _page.Locator(SEL_CHAT_LIST).First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
			return true;
		}
		catch { return false; }
	}

	public async Task<bool> SwitchAccountAsync(int accountNumber)
	{
		if (accountNumber < 1 || accountNumber > 3)
		{
			Logger.Error($"[AccountSwitch] Invalid account number {accountNumber}.", _instanceId);
			return false;
		}
		int x = accountNumber == 1 ? _config.Account1X : accountNumber == 2 ? _config.Account2X : _config.Account3X;
		int y = accountNumber == 1 ? _config.Account1Y : accountNumber == 2 ? _config.Account2Y : _config.Account3Y;
		try
		{
			if (_page == null || _page.IsClosed) throw new InvalidOperationException("Telegram page is unavailable.");
			Logger.Info($"[AccountSwitch] Switching to Account {accountNumber}.", _instanceId);
			Logger.Info($"[AccountSwitch] Opening account menu at X={_config.AccountMenuX} Y={_config.AccountMenuY}.", _instanceId);
			await BrowserDiagnosticAsync($"Account menu click ({_config.AccountMenuX},{_config.AccountMenuY})", "before", screenshot: true);
			await _page.Mouse.ClickAsync(_config.AccountMenuX, _config.AccountMenuY);
			await BrowserDiagnosticAsync($"Account menu click ({_config.AccountMenuX},{_config.AccountMenuY})", "after", screenshot: true);
			Logger.Info("[AccountSwitch] Account menu click completed.", _instanceId);
			Logger.Info("[AccountSwitch] Waiting for account menu to open.", _instanceId);
			await Task.Delay(750);
			Logger.Info($"[AccountSwitch] Clicking Account {accountNumber} at X={x} Y={y}.", _instanceId);
			await BrowserDiagnosticAsync($"Account {accountNumber} click ({x},{y})", "before", screenshot: true);
			await _page.Mouse.ClickAsync(x, y);
			await BrowserDiagnosticAsync($"Account {accountNumber} click ({x},{y})", "after", screenshot: true);
			Logger.Info($"[AccountSwitch] Account {accountNumber} click completed.", _instanceId);
			Logger.Info("[AccountSwitch] Waiting 5 seconds for Telegram account switch.", _instanceId);
			await Task.Delay(TimeSpan.FromSeconds(5));
			Logger.Info($"[AccountSwitch] Account {accountNumber} switch wait completed.", _instanceId);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error($"[AccountSwitch] Account {accountNumber} switch failed: {ex.Message}", _instanceId);
			return false;
		}
	}

	public async Task<(int x, int y)?> PickPositionAsync(string label, CancellationToken cancellationToken = default)
	{
		_ = 5;
		bool trackerInstalled = false;
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
			trackerInstalled = true;
			for (int i = 4; i > 0; i--)
			{
				Logger.Info($"Move mouse now... {i}", _instanceId);
				await Task.Delay(1000, cancellationToken);
			}
			PickPositionResult pos = JsonSerializer.Deserialize(await _page.EvaluateAsync<string>("() => JSON.stringify({x: window.__lastMouseX, y: window.__lastMouseY})"), AppJsonContext.Default.PickPositionResult);
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
		finally
		{
			if (trackerInstalled && _page != null && !_page.IsClosed)
			{
				try
				{
					await _page.EvaluateAsync("() => { if (window.__mouseTracker) { window.removeEventListener('mousemove', window.__mouseTracker, true); document.removeEventListener('mousemove', window.__mouseTracker, true); window.removeEventListener('pointermove', window.__mouseTracker, true); delete window.__mouseTracker; } }");
				}
				catch (Exception ex) { Logger.Warning("Could not remove coordinate picker mouse tracker: " + ex.Message, _instanceId); }
			}
		}
	}

	public async Task<(int x, int y, int width, int height)?> PickPositionWithViewportAsync(string label, CancellationToken cancellationToken = default)
	{
		(int x, int y)? position = await PickPositionAsync(label, cancellationToken);
		(int width, int height)? viewport = await GetViewportSizeAsync();
		return position.HasValue && viewport.HasValue ? (position.Value.x, position.Value.y, viewport.Value.width, viewport.Value.height) : null;
	}

	    	public async Task<(int x, int y, int width, int height, int scaleX, int scaleY)?> CaptureCurrentPagePointerAsync(int rawX, int rawY)
	{
		try
		{
			if (_page == null || _page.IsClosed || _display == nint.Zero || rawX < 0 || rawY < 0) return null;
			nint root = XDefaultRootWindow(_display);
			if (XQueryPointer(_display, root, out _, out nint window, out int pointerX, out int pointerY, out _, out _, out _) == 0 || pointerX != rawX || pointerY != rawY) return null;
			if (XFetchName(_display, window, out nint namePtr) == 0 || namePtr == nint.Zero) return null;
			string title = Marshal.PtrToStringAnsi(namePtr) ?? ""; XFree(namePtr);
			if (!title.Contains("Telegram", StringComparison.OrdinalIgnoreCase)) return null;
			if (XTranslateCoordinates(_display, window, root, 0, 0, out int winX, out int winY, out _) == 0) return null;
			var page = await _page.EvaluateAsync<PointerCaptureResult>("raw => { const x=Math.floor((raw.x-screenX)/devicePixelRatio),y=Math.floor((raw.y-screenY)/devicePixelRatio); const e=document.elementFromPoint(x,y); return {width:innerWidth,height:innerHeight,dpr:devicePixelRatio||1,x,y,valid:!!e && x<innerWidth && y<innerHeight}; }", new { x = pointerX, y = pointerY });
			int x = page.X, y = page.Y;
			if (!page.Valid || x < 0 || y < 0 || x >= page.Width || y >= page.Height) return null;
			int offsetX = pointerX - winX - (int)Math.Round(page.X * page.Dpr);
			int offsetY = pointerY - winY - (int)Math.Round(page.Y * page.Dpr);
			Logger.Info($"[MenuCapture] raw X11=({rawX},{rawY}); windowOrigin=({winX},{winY}); measured browser content offset=({offsetX},{offsetY}); deviceScaleFactor={page.Dpr}; Playwright=({x},{y}) viewport={page.Width}x{page.Height}.", _instanceId);
			return (x, y, page.Width, page.Height, 1, 1);
		}
		catch (Exception ex) { Logger.Warning("Could not convert X11 pointer to Playwright coordinates: " + ex.Message, _instanceId); return null; }
	}
public async Task<bool> StartMenuButtonPointerTrackingAsync() => _page != null && !_page.IsClosed;

	private sealed class PointerCaptureResult { public int Width { get; set; } public int Height { get; set; } public double Dpr { get; set; } public int X { get; set; } public int Y { get; set; } public int WindowX { get; set; } public int WindowY { get; set; } public bool Valid { get; set; } }

	public async Task<(int width, int height)?> GetViewportSizeAsync()
	{
		try
		{
			if (_page == null || _page.IsClosed) return null;
			ViewportResult result = await _page.EvaluateAsync<ViewportResult>("() => ({width: window.innerWidth, height: window.innerHeight})");
			return result.Width > 0 && result.Height > 0 ? (result.Width, result.Height) : null;
		}
		catch (Exception ex)
		{
			Logger.Warning("Could not read browser viewport dimensions: " + ex.Message, _instanceId);
			return null;
		}
	}

	public async Task<ReplyKeyboardMenuState> GetReplyKeyboardMenuStateAsync()
	{
		try
		{
			if (_page == null || _page.IsClosed) return ReplyKeyboardMenuState.Unknown;
			string state = await _page.EvaluateAsync<string>("() => { const items=[...document.querySelectorAll('[aria-controls^=reply-keyboard-][aria-expanded]')].filter(e => { const r=e.getBoundingClientRect(); return r.width>0 && r.height>0 && getComputedStyle(e).visibility!=='hidden'; }); if(items.length!==1) return 'unknown'; return items[0].getAttribute('aria-expanded')==='true' ? 'open' : items[0].getAttribute('aria-expanded')==='false' ? 'closed' : 'unknown'; }");
			return state switch { "open" => ReplyKeyboardMenuState.Open, "closed" => ReplyKeyboardMenuState.Closed, _ => ReplyKeyboardMenuState.Unknown };
		}
		catch (Exception ex)
		{
			Logger.Warning("Could not determine Telegram reply keyboard state: " + ex.Message, _instanceId);
			return ReplyKeyboardMenuState.Unknown;
		}
	}

	public async Task<bool> SetReplyKeyboardMenuStateAsync(int x, int y, ReplyKeyboardMenuState desiredState)
	{
		if (_page == null || _page.IsClosed || desiredState == ReplyKeyboardMenuState.Unknown) return false;
		ReplyKeyboardMenuState currentState = await GetReplyKeyboardMenuStateAsync();
		if (currentState == ReplyKeyboardMenuState.Unknown) return false;
		if (currentState == desiredState) return true;
		if (!await ClickAsync(x, y, "Telegram reply keyboard menu button", captureScreenshots: true)) return false;
		bool expected = desiredState == ReplyKeyboardMenuState.Open;
		try
		{
			await _page.WaitForFunctionAsync("expected => { const items=[...document.querySelectorAll('[aria-controls^=reply-keyboard-][aria-expanded]')].filter(e => { const r=e.getBoundingClientRect(); return r.width>0 && r.height>0 && getComputedStyle(e).visibility!=='hidden'; }); return items.length===1 && items[0].getAttribute('aria-expanded')===(expected ? 'true' : 'false'); }", expected, new PageWaitForFunctionOptions { Timeout = 5000 });
		}
		catch (Exception ex)
		{
			Logger.Warning($"Reply keyboard state did not become {desiredState} after the configured menu button click: {ex.Message}", _instanceId);
			return false;
		}
		return await GetReplyKeyboardMenuStateAsync() == desiredState;
	}

	private sealed class ViewportResult
	{
		public int Width { get; set; }
		public int Height { get; set; }
	}

	public async Task<bool> ClickAsync(int x, int y, string action = "Mouse click", bool captureScreenshots = false)
	{
		try
		{
			if (_page == null)
			{
				return false;
			}
			await BrowserDiagnosticAsync($"{action} coordinates=({x},{y})", "before", screenshot: captureScreenshots);
			await _page.Mouse.ClickAsync(x, y);
			await BrowserDiagnosticAsync($"{action} coordinates=({x},{y})", "after", screenshot: captureScreenshots);
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
			await BrowserDiagnosticAsync($"Keyboard type text={text}", "before");
			await _page.Keyboard.TypeAsync(text, new KeyboardTypeOptions
			{
				Delay = _config.TypeDelayMs
			});
			await BrowserDiagnosticAsync($"Keyboard type text={text}", "after");
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
			await BrowserDiagnosticAsync("Keyboard Enter", "before");
			await _page.Keyboard.PressAsync("Enter");
			await BrowserDiagnosticAsync("Keyboard Enter", "after");
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
		await _disposeLock.WaitAsync();
		try
		{
			if (_disposeCompleted) return;
			Logger.Info("[Cleanup] Closing Playwright pages/contexts...", _instanceId);
			var context = _context;
			var pages = context?.Pages ?? Array.Empty<IPage>();
			var cleanupErrors = new List<string>();
			foreach (var page in pages)
			{
				try
				{
					if (!page.IsClosed) await page.CloseAsync();
				}
				catch (Exception ex)
				{
					if (!page.IsClosed) cleanupErrors.Add("Page close: " + ex.Message);
				}
			}

			IBrowser? browser = context?.Browser;
			if (context != null)
			{
				try
				{
					await context.CloseAsync();
					_context = null;
				}
				catch (Exception ex)
				{
					cleanupErrors.Add("Context close: " + ex.Message);
				}
			}

			Logger.Info("[Cleanup] Closing Chromium browser...", _instanceId);
			if (browser != null && browser.IsConnected)
			{
				try { await browser.CloseAsync(); }
				catch (Exception ex) { cleanupErrors.Add("Browser close: " + ex.Message); }
			}
			// LaunchPersistentContextAsync owns its Chromium process; closing that context closes Chromium.
			try { _playwright?.Dispose(); _playwright = null; }
			catch (Exception ex) { cleanupErrors.Add("Playwright dispose: " + ex.Message); }

			bool pagesClosed = true;
			foreach (var page in pages)
			{
				try { pagesClosed &= page.IsClosed; }
				catch { pagesClosed = false; }
			}
			bool browserClosed = browser == null || !browser.IsConnected;
			if (_context == null && pagesClosed && browserClosed && _playwright == null)
			{
				_page = null;
				_disposeCompleted = true;
				Logger.Info("[Cleanup] Browser cleanup completed", _instanceId);
				Logger.Info("[Cleanup] Browser verified closed", _instanceId);
			}
			else
			{
				cleanupErrors.Add("Browser resources remain alive after disposal.");
			}
			if (cleanupErrors.Count > 0)
			{
				string reason = string.Join("; ", cleanupErrors);
				Logger.Error("[Cleanup] Browser cleanup failed: " + reason, _instanceId);
				throw new InvalidOperationException(reason);
			}
		}
		finally { _disposeLock.Release(); }
	}
}
