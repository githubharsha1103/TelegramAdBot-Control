using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace TelegramAdBot.Helpers;

public class GlobalHotkeyListener : IDisposable
{
    private record HotkeyRegistration(
        int Id,
        uint Mods,
        uint Key,
        string Name
    );

    private readonly List<HotkeyRegistration> _pending = new();
    private readonly Dictionary<int, Action> _handlers = new();

    private int _nextId = 1;
    private Thread? _msgThread;
    private volatile bool _running;

    private nint _display;

    // Application modifier definitions
    public const uint MOD_ALT = 1u;
    public const uint MOD_CONTROL = 2u;
    public const uint MOD_SHIFT = 4u;
    public const uint MOD_WIN = 8u;

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryKeymap(
        nint display,
        byte[] keys
    );

    [DllImport("libX11.so.6")]
    private static extern byte XKeysymToKeycode(
        nint display,
        nint keysym
    );

    [DllImport("libX11.so.6")]
    private static extern int XFlush(
        nint display
    );

    public void Register(
        uint modifiers,
        uint key,
        string name,
        Action callback)
    {
        int id = _nextId++;

        _pending.Add(
            new HotkeyRegistration(
                id,
                modifiers,
                key,
                name
            )
        );

        _handlers[id] = callback;
    }

    public void Start()
    {
        if (OperatingSystem.IsWindows())
        {
            Logger.Warning(
                "Windows hotkey support is not included in this Linux build."
            );
            return;
        }

        if (!OperatingSystem.IsLinux())
        {
            Logger.Warning(
                "Global hotkeys are not supported on this operating system."
            );
            return;
        }

        _running = true;

        _msgThread = new Thread(X11KeyboardLoop)
        {
            IsBackground = true,
            Name = "GlobalHotkeyThread"
        };

        _msgThread.Start();
    }

    private void X11KeyboardLoop()
    {
        try
        {
            _display = XOpenDisplay(nint.Zero);

            if (_display == nint.Zero)
            {
                Logger.Error(
                    "Could not open X11 display. Make sure DISPLAY is set."
                );
                return;
            }

            Logger.Info(
                "X11 keyboard monitor connected successfully."
            );

            int valid = 0;

            foreach (HotkeyRegistration hk in _pending)
            {
                byte keycode = GetLinuxKeycode(hk.Key);

                if (keycode == 0)
                {
                    Logger.Warning(
                        $"Could not map Linux keycode: {hk.Name}"
                    );
                    continue;
                }

                valid++;

                Logger.Info(
                    $"Linux hotkey ready: {hk.Name} " +
                    $"(keycode={keycode})"
                );
            }

            Logger.Success(
                $"Linux global hotkeys ready: {valid}/{_pending.Count}"
            );

            byte[] keyboard = new byte[32];

            // Prevent a key being held down from triggering repeatedly.
            HashSet<int> previouslyTriggered = new();

            while (_running)
            {
                Array.Clear(keyboard, 0, keyboard.Length);

                XQueryKeymap(
                    _display,
                    keyboard
                );

                foreach (HotkeyRegistration hk in _pending)
                {
                    byte keycode =
                        GetLinuxKeycode(hk.Key);

                    if (keycode == 0)
                        continue;

                    bool keyDown =
                        IsKeyDown(
                            keyboard,
                            keycode
                        );

                    if (!keyDown)
                    {
                        previouslyTriggered.Remove(hk.Id);
                        continue;
                    }

                    uint requiredModifiers =
                        hk.Mods;

                    bool modifiersDown =
                        AreModifiersDown(
                            keyboard,
                            requiredModifiers
                        );

                    if (!modifiersDown)
                        continue;

                    if (previouslyTriggered.Contains(hk.Id))
                        continue;

                    previouslyTriggered.Add(hk.Id);

                    Logger.Info(
                        $"Global hotkey detected: {hk.Name}"
                    );

                    if (_handlers.TryGetValue(
                        hk.Id,
                        out Action? callback))
                    {
                        try
                        {
                            callback();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(
                                "Hotkey callback error: " +
                                ex.Message
                            );
                        }
                    }
                }

                Thread.Sleep(25);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                "Linux hotkey listener error: " +
                ex.Message
            );
        }
        finally
        {
            if (_display != nint.Zero)
            {
                XCloseDisplay(_display);
                _display = nint.Zero;
            }
        }
    }

    private bool AreModifiersDown(
        byte[] keyboard,
        uint modifiers)
    {
        if ((modifiers & MOD_CONTROL) != 0)
        {
            if (!IsKeyDown(keyboard, 37) &&
                !IsKeyDown(keyboard, 105))
            {
                return false;
            }
        }

        if ((modifiers & MOD_SHIFT) != 0)
        {
            if (!IsKeyDown(keyboard, 50) &&
                !IsKeyDown(keyboard, 62))
            {
                return false;
            }
        }

        if ((modifiers & MOD_ALT) != 0)
        {
            if (!IsKeyDown(keyboard, 64) &&
                !IsKeyDown(keyboard, 108))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsKeyDown(
        byte[] keyboard,
        int keycode)
    {
        if (keycode < 0 || keycode >= 256)
            return false;

        int index = keycode / 8;
        int bit = keycode % 8;

        return
            (keyboard[index] &
             (1 << bit)) != 0;
    }

    private byte GetLinuxKeycode(
        uint windowsVirtualKey)
    {
        if (_display == nint.Zero)
            return 0;

        // Windows VK_A..VK_Z are 0x41..0x5A.
        // X11 letter keysyms are lowercase ASCII.
        if (windowsVirtualKey >= 0x41 &&
            windowsVirtualKey <= 0x5A)
        {
            nint keysym =
                (nint)(windowsVirtualKey + 0x20);

            return XKeysymToKeycode(
                _display,
                keysym
            );
        }

        // X11 digit keysyms share the ASCII codes used by Windows VK_0..VK_9.
        if (windowsVirtualKey >= 0x30 &&
            windowsVirtualKey <= 0x39)
        {
            return XKeysymToKeycode(
                _display,
                (nint)windowsVirtualKey
            );
        }

        return 0;
    }

    public void Dispose()
    {
        _running = false;

        if (_display != nint.Zero)
        {
            XCloseDisplay(
                _display
            );

            _display = nint.Zero;
        }

        try
        {
            if (_msgThread != null &&
                _msgThread.IsAlive)
            {
                _msgThread.Join(500);
            }
        }
        catch
        {
            // Ignore shutdown errors.
        }
    }
}
