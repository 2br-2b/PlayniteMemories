using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SharpMemories
{
    public class KeyboardHookManager : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private Action _hotkeyCallback;

        private Key _targetKey;
        private bool _requireCtrl;
        private bool _requireAlt;
        private bool _requireShift;
        private bool _isEnabled;
        private bool _suppressKey;

        public KeyboardHookManager()
        {
            _proc = HookCallback;
        }

        public void RegisterHotkey(Key key, bool ctrl, bool alt, bool shift, bool suppressKey, Action callback)
        {
            logger.Info($"Registering hotkey: {(ctrl ? "Ctrl+" : "")}{(alt ? "Alt+" : "")}{(shift ? "Shift+" : "")}{key} (suppress: {suppressKey})");

            _targetKey = key;
            _requireCtrl = ctrl;
            _requireAlt = alt;
            _requireShift = shift;
            _suppressKey = suppressKey;
            _hotkeyCallback = callback;
            _isEnabled = true;

            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
                logger.Debug($"Keyboard hook installed: {_hookID}");
            }
        }

        public void UnregisterHotkey()
        {
            logger.Info("Unregistering hotkey");
            _isEnabled = false;
            _hotkeyCallback = null;

            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
                logger.Debug("Keyboard hook removed");
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN && _isEnabled && _hotkeyCallback != null)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                // Check if this is our target key
                if (key == _targetKey)
                {
                    // Check modifiers
                    bool ctrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                    bool altPressed = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
                    bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                    bool modifiersMatch =
                        ctrlPressed == _requireCtrl &&
                        altPressed == _requireAlt &&
                        shiftPressed == _requireShift;

                    if (modifiersMatch)
                    {
                        logger.Debug("Hotkey pressed, triggering callback");
                        try
                        {
                            _hotkeyCallback?.Invoke();
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, "Error in hotkey callback");
                        }

                        // If suppress is enabled, return non-zero to prevent the key from reaching the application
                        if (_suppressKey)
                        {
                            logger.Debug("Suppressing key event");
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            UnregisterHotkey();
        }

        #region Win32 API

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion
    }
}
