using System;
using System.Runtime.InteropServices;

namespace TelegramAdBot.Helpers;

public static class ConsoleFix
{
	private const int STD_INPUT_HANDLE = -10;

	private const uint ENABLE_QUICK_EDIT = 64u;

	private const uint ENABLE_EXTENDED_FLAGS = 128u;

	private const uint ENABLE_MOUSE_INPUT = 16u;

	private const uint ENABLE_INSERT_MODE = 32u;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint GetStdHandle(int nStdHandle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

	public static void DisableQuickEdit()
	{
		try
		{
			nint handle = GetStdHandle(-10);
			if (handle != IntPtr.Zero && handle != new IntPtr(-1) && GetConsoleMode(handle, out var mode))
			{
				mode &= 0xFFFFFFBFu;
				mode &= 0xFFFFFFDFu;
				mode |= 0x80;
				SetConsoleMode(handle, mode);
				Logger.Info("Console QuickEdit disabled (no more click-pause).");
			}
		}
		catch (Exception ex)
		{
			Logger.Warning("Could not disable QuickEdit: " + ex.Message);
		}
	}
}
