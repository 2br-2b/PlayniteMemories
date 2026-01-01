using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace SharpMemories
{
    /// <summary>
    /// Handles the logical operations for capturing screenshots from windows or screens.
    /// Utilizes multiple fallback strategies (PrintWindow, BitBlt, CopyFromScreen) to ensure capture success.
    /// </summary>
    internal static class ScreenCapture
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();
        #endregion

        #region Public Methods
        /// <summary>
        /// Captures ONLY the primary monitor.
        /// Used as a fallback if the specific game window cannot be found or captured.
        /// </summary>
        /// <returns>A Bitmap containing the screenshot, or null on failure.</returns>
        public static Bitmap CapturePrimaryScreen()
        {
            // .Bounds includes the taskbar area (Fullscreen), which is preferred for games.
            return CaptureRegion(Screen.PrimaryScreen.Bounds);
        }

        /// <summary>
        /// Attempts to capture a specific window using a sequence of strategies.
        /// Tries PrintWindow (best for hardware accel), then BitBlt, then window area extraction.
        /// </summary>
        /// <param name="hWnd">The handle of the window to capture.</param>
        /// <returns>A Bitmap containing the screenshot, or null on failure.</returns>
        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                _logger.Warn("Attempted to capture a window with a Zero handle.");
                return null;
            }

            Bitmap result = null;

            // Strategy 1: PrintWindow with PW_RENDERFULLCONTENT
            // Best for modern apps (Chrome, Electron, some DirectX windows)
            result = CaptureWithPrintWindow(hWnd, NativeMethods.PW_RENDERFULLCONTENT);
            if (ScreenshotValidator.IsValidScreenshot(result))
            {
                return result;
            }
            result?.Dispose();

            // Strategy 2: PrintWindow with PW_CLIENTONLY
            // Fallback for apps where full content rendering fails
            result = CaptureWithPrintWindow(hWnd, NativeMethods.PW_CLIENTONLY);
            if (ScreenshotValidator.IsValidScreenshot(result))
            {
                return result;
            }
            result?.Dispose();

            // Strategy 3: CaptureWindowArea (CopyFromScreen)
            // Uses the screen coordinates to scrape the pixels directly from the GPU buffer.
            // This is often the most reliable for exclusive fullscreen games.
            result = CaptureWindowArea(hWnd);
            if (ScreenshotValidator.IsValidScreenshot(result))
            {
                return result;
            }
            result?.Dispose();

            // Strategy 4: BitBlt (GDI Transfer)
            // The traditional method. Fast, but often produces black screens on hardware-accelerated windows.
            result = CaptureWithBitBlt(hWnd);
            if (ScreenshotValidator.IsValidScreenshot(result))
            {
                return result;
            }
            result?.Dispose();

            _logger.Error($"Failed to capture window {hWnd} using all available methods.");
            return null;
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// Captures window using the PrintWindow API.
        /// </summary>
        private static Bitmap CaptureWithPrintWindow(IntPtr hWnd, uint flags)
        {
            IntPtr hdcSrc = IntPtr.Zero;
            IntPtr hdcDest = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                Rectangle rect = GetWindowRectangle(hWnd);
                if (rect.Width <= 0 || rect.Height <= 0) return null;

                hdcSrc = NativeMethods.GetWindowDC(hWnd);
                if (hdcSrc == IntPtr.Zero) return null;

                hdcDest = NativeMethods.CreateCompatibleDC(hdcSrc);
                hBitmap = NativeMethods.CreateCompatibleBitmap(hdcSrc, rect.Width, rect.Height);
                hOld = NativeMethods.SelectObject(hdcDest, hBitmap);

                bool success = NativeMethods.PrintWindow(hWnd, hdcDest, flags);
                if (!success) return null;

                return Image.FromHbitmap(hBitmap);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, $"PrintWindow capture failed for handle {hWnd}.");
                return null;
            }
            finally
            {
                if (hOld != IntPtr.Zero) NativeMethods.SelectObject(hdcDest, hOld);
                if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
                if (hdcDest != IntPtr.Zero) NativeMethods.DeleteDC(hdcDest);
                if (hdcSrc != IntPtr.Zero) NativeMethods.ReleaseDC(hWnd, hdcSrc);
            }
        }

        /// <summary>
        /// Captures window using the BitBlt API.
        /// </summary>
        private static Bitmap CaptureWithBitBlt(IntPtr hWnd)
        {
            IntPtr hdcSrc = IntPtr.Zero;
            IntPtr hdcDest = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                hdcSrc = NativeMethods.GetWindowDC(hWnd);
                if (hdcSrc == IntPtr.Zero) return null;

                Rectangle rect = GetWindowRectangle(hWnd);
                if (rect.Width <= 0 || rect.Height <= 0) return null;

                hdcDest = NativeMethods.CreateCompatibleDC(hdcSrc);
                hBitmap = NativeMethods.CreateCompatibleBitmap(hdcSrc, rect.Width, rect.Height);
                hOld = NativeMethods.SelectObject(hdcDest, hBitmap);

                bool success = NativeMethods.BitBlt(hdcDest, 0, 0, rect.Width, rect.Height, hdcSrc, 0, 0, NativeMethods.SRCCOPY);
                if (!success) return null;

                return Image.FromHbitmap(hBitmap);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, $"BitBlt capture failed for handle {hWnd}.");
                return null;
            }
            finally
            {
                if (hOld != IntPtr.Zero) NativeMethods.SelectObject(hdcDest, hOld);
                if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
                if (hdcDest != IntPtr.Zero) NativeMethods.DeleteDC(hdcDest);
                if (hdcSrc != IntPtr.Zero) NativeMethods.ReleaseDC(hWnd, hdcSrc);
            }
        }

        /// <summary>
        /// Determines the screen coordinates of the window and captures that region using GDI+.
        /// </summary>
        private static Bitmap CaptureWindowArea(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            if (NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0) return null;

                var bounds = new Rectangle(rect.Left, rect.Top, width, height);
                return CaptureRegion(bounds);
            }

            return null;
        }

        /// <summary>
        /// Captures a specific region of the screen using the Graphics.CopyFromScreen method.
        /// </summary>
        private static Bitmap CaptureRegion(Rectangle bounds)
        {
            try
            {
                var bmp = new Bitmap(bounds.Width, bounds.Height);

                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }
                return bmp;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to capture screen region: {bounds}");
                return null;
            }
        }

        /// <summary>
        /// Helper to retrieve the Rectangle dimensions of a Window Handle.
        /// </summary>
        private static Rectangle GetWindowRectangle(IntPtr hWnd)
        {
            if (hWnd == NativeMethods.GetDesktopWindow())
            {
                return Screen.PrimaryScreen.Bounds;
            }

            if (NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect))
            {
                return new Rectangle(
                    0,
                    0,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top
                );
            }

            return Screen.PrimaryScreen.Bounds;
        }
        #endregion
    }
}
