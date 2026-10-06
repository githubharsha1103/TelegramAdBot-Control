using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TelegramAdBot.Helpers;

public static class VirtualDesktopHelper
{
	private const int SW_MINIMIZE = 6;

	private const int SW_HIDE = 0;

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	public static void MinimizeAllChromium()
	{
		try
		{
			Process[] procs = Process.GetProcessesByName("chrome");
			Process[] array = procs;
			foreach (Process p in array)
			{
				if (p.MainWindowHandle != IntPtr.Zero)
				{
					ShowWindow(p.MainWindowHandle, 6);
				}
			}
			Logger.Info($"Minimized {procs.Length} Chrome window(s).");
		}
		catch (Exception ex)
		{
			Logger.Warning("Minimize failed: " + ex.Message);
		}
	}
}
