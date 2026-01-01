using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SharpMemories
{
    /// <summary>
    /// Manages a global low-level keyboard hook to detect specific hotkey combinations
    /// even when the application is not in focus.
    /// </summary>
    public class KeyboardHookManager : IDisposable
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();

        // We must keep a reference to this delegate to prevent the Garbage Collector
        // from cleaning it up while the unmanaged Windows hook is still active.
        private readonly NativeMethods.LowLevelKeyboardProc _hookProc;

        private IntPtr _hookID = IntPtr.Zero;
        private Action _hotkeyCallback;

        private Key _targetKey;
        private bool _requireCtrl;
        private bool _requireAlt;
        private bool _requireShift;
        private bool _isEnabled;
        private bool _suppressKey;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyboardHookManager"/> class.
        /// </summary>
        public KeyboardHookManager()
        {
            _hookProc = HookCallback;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Registers a global hotkey with specific modifier requirements.
        /// </summary>
        /// <param name="key">The primary key to listen for.</param>
        /// <param name="ctrl">Whether the Control key must be held.</param>
        /// <param name="alt">Whether the Alt key must be held.</param>
        /// <param name="shift">Whether the Shift key must be held.</param>
        /// <param name="suppressKey">If true, the key event will be swallowed and not passed to other applications.</param>
        /// <param name="callback">The action to execute when the hotkey is triggered.</param>
        public void RegisterHotkey(Key key, bool ctrl, bool alt, bool shift, bool suppressKey, Action callback)
        {
            var modifiers = $"{(ctrl ? "Ctrl+" : "")}{(alt ? "Alt+" : "")}{(shift ? "Shift+" : "")}";
            _logger.Info($"Registering global hotkey: {modifiers}{key} | Suppress input: {suppressKey}");

            _targetKey = key;
            _requireCtrl = ctrl;
            _requireAlt = alt;
            _requireShift = shift;
            _suppressKey = suppressKey;
            _hotkeyCallback = callback;
            _isEnabled = true;

            // Only install the Windows hook if it hasn't been installed yet
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_hookProc);
                _logger.Debug($"Low-level keyboard hook installed successfully. Hook ID: {_hookID}");
            }
        }

        /// <summary>
        /// Unregisters the current hotkey and removes the Windows hook.
        /// </summary>
        public void UnregisterHotkey()
        {
            _logger.Info("Unregistering hotkey and disabling hook.");
            _isEnabled = false;
            _hotkeyCallback = null;

            if (_hookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
                _logger.Debug("Keyboard hook removed from system.");
            }
        }

        /// <summary>
        /// Disposes the manager and ensures the hook is removed to prevent memory leaks or system instability.
        /// </summary>
        public void Dispose()
        {
            UnregisterHotkey();
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// Installs the keyboard hook into the current process module.
        /// </summary>
        /// <param name="proc">The callback delegate to execute when a key is pressed.</param>
        /// <returns>A pointer to the hook handle.</returns>
        private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_KEYBOARD_LL,
                    proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName),
                    0);
            }
        }

        /// <summary>
        /// The actual callback method invoked by the Windows API when a key event occurs.
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // nCode >= 0 indicates a valid message. WM_KEYDOWN indicates a key press.
            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_KEYDOWN && _isEnabled && _hotkeyCallback != null)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                if (IsTargetHotkey(key))
                {
                    _logger.Debug($"Hotkey detected: {_targetKey}");

                    try
                    {
                        _hotkeyCallback.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "An exception occurred while executing the hotkey callback.");
                    }

                    // If suppression is requested, we return a non-zero value (1).
                    // This tells Windows to stop propagating the event to other applications.
                    if (_suppressKey)
                    {
                        _logger.Debug("Suppressing key event from other applications.");
                        return (IntPtr)1;
                    }
                }
            }

            // Pass the event to the next hook in the chain
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Checks if the pressed key and currently held modifiers match the configured hotkey.
        /// </summary>
        /// <param name="pressedKey">The key that was just pressed.</param>
        /// <returns>True if the combination matches; otherwise, false.</returns>
        private bool IsTargetHotkey(Key pressedKey)
        {
            if (pressedKey != _targetKey)
            {
                return false;
            }

            // Check current state of modifier keys
            bool isCtrlDown = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isAltDown = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            bool isShiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            return isCtrlDown == _requireCtrl &&
                   isAltDown == _requireAlt &&
                   isShiftDown == _requireShift;
        }
        #endregion
    }
}
