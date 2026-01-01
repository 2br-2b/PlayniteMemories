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
    internal static class ScreenCapture
    {
        #region Imports
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, System.Int32 dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int SRCCOPY = 0x00CC0020;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const uint PW_CLIENTONLY = 0x00000001;
        #endregion

        #region Fields
        private static HashSet<string> _imageHashes = new HashSet<string>();
        private static object _lock = new object();
        private static readonly ILogger _logger = LogManager.GetLogger();
        #endregion

        #region Workers
        /// <summary>
        /// Captures ONLY the primary monitor.
        /// Used as a fallback if the specific game window cannot be found.
        /// </summary>
        public static Bitmap CapturePrimaryScreen()
        {
            // .Bounds includes the taskbar area (Fullscreen), which is what we want for games.
            // .WorkingArea would exclude the taskbar.
            return CaptureRegion(Screen.PrimaryScreen.Bounds);
        }

        [Obsolete("Use CapturePrimaryScreen instead.")]
        public static Bitmap CaptureScreen()
        {
            var hWnd = GetDesktopWindow();
            return CaptureWindow(hWnd);
        }

        /// <summary>
        /// Captures a window using multiple methods with fallback logic
        /// Tries PrintWindow first (best for hardware-accelerated apps), then BitBlt
        /// </summary>
        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            // Try multiple capture methods in order of effectiveness
            // 1. PrintWindow with PW_RENDERFULLCONTENT (best for hardware-accelerated apps like browsers)
            // 2. PrintWindow with PW_CLIENTONLY (alternative for some apps)
            // 3. BitBlt (traditional method, works for non-accelerated apps)

            Bitmap result = null;

            // Method 1: PrintWindow with PW_RENDERFULLCONTENT
            result = CaptureWithPrintWindow(hWnd, PW_RENDERFULLCONTENT);
            if (IsValidScreenshot(result))
                return result;
            result?.Dispose();

            // Method 2: PrintWindow with PW_CLIENTONLY
            result = CaptureWithPrintWindow(hWnd, PW_CLIENTONLY);
            if (IsValidScreenshot(result))
                return result;
            result?.Dispose();

            // Method 3: CaptureWindowArea
            result = CaptureWindowArea(hWnd);
            if (IsValidScreenshot(result))
                return result;
            result?.Dispose();

            // Method 4: BitBlt (fallback)
            result = CaptureWithBitBlt(hWnd);
            if (IsValidScreenshot(result))
                return result;
            result?.Dispose();

            // All methods failed
            return null;
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Captures window using PrintWindow API (works better with hardware-accelerated apps)
        /// </summary>
        private static Bitmap CaptureWithPrintWindow(IntPtr hWnd, uint flags)
        {
            IntPtr hdcSrc = IntPtr.Zero;
            IntPtr hdcDest = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                // Get window dimensions
                Rectangle rect = GetWindowRectangle(hWnd);
                if (rect.Width <= 0 || rect.Height <= 0) return null;

                hdcSrc = GetWindowDC(hWnd);
                if (hdcSrc == IntPtr.Zero) return null;

                hdcDest = CreateCompatibleDC(hdcSrc);
                hBitmap = CreateCompatibleBitmap(hdcSrc, rect.Width, rect.Height);
                hOld = SelectObject(hdcDest, hBitmap);

                // PrintWindow renders the window into the DC
                bool success = PrintWindow(hWnd, hdcDest, flags);
                if (!success) return null;

                var img = Image.FromHbitmap(hBitmap);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hOld != IntPtr.Zero) SelectObject(hdcDest, hOld);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hdcDest != IntPtr.Zero) DeleteDC(hdcDest);
                if (hdcSrc != IntPtr.Zero) ReleaseDC(hWnd, hdcSrc);
            }
        }

        /// <summary>
        /// Captures the specific area of the game window.
        /// This ensures we don't capture the second monitor, just the game.
        /// </summary>
        private static Bitmap CaptureWindowArea(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            RECT rect;
            if (GetWindowRect(hWnd, out rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0) return null;

                // Create a rectangle for the specific game window position
                var bounds = new Rectangle(rect.Left, rect.Top, width, height);

                return CaptureRegion(bounds);
            }

            return null;
        }

        /// <summary>
        /// Captures window using BitBlt (traditional method)
        /// </summary>
        private static Bitmap CaptureWithBitBlt(IntPtr hWnd)
        {
            IntPtr hdcSrc = IntPtr.Zero;
            IntPtr hdcDest = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                hdcSrc = GetWindowDC(hWnd);
                if (hdcSrc == IntPtr.Zero) return null;

                Rectangle rect = GetWindowRectangle(hWnd);
                if (rect.Width <= 0 || rect.Height <= 0) return null;

                hdcDest = CreateCompatibleDC(hdcSrc);
                hBitmap = CreateCompatibleBitmap(hdcSrc, rect.Width, rect.Height);
                hOld = SelectObject(hdcDest, hBitmap);

                var success = BitBlt(hdcDest, 0, 0, rect.Width, rect.Height, hdcSrc, 0, 0, SRCCOPY);
                if (!success) return null;

                var img = Image.FromHbitmap(hBitmap);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hOld != IntPtr.Zero) SelectObject(hdcDest, hOld);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hdcDest != IntPtr.Zero) DeleteDC(hdcDest);
                if (hdcSrc != IntPtr.Zero) ReleaseDC(hWnd, hdcSrc);
            }
        }

        /// <summary>
        /// Gets window rectangle, falling back to screen bounds if needed
        /// </summary>
        private static Rectangle GetWindowRectangle(IntPtr hWnd)
        {
            if (hWnd == GetDesktopWindow())
            {
                return System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            }

            RECT rect;
            if (GetWindowRect(hWnd, out rect))
            {
                return new Rectangle(
                    0,
                    0,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top
                );
            }

            // Fallback to primary screen
            return System.Windows.Forms.Screen.PrimaryScreen.Bounds;
        }

        /// <summary>
        /// Captures a specific region on the screen.
        /// </summary>
        private static Bitmap CaptureRegion(Rectangle bounds)
        {
            try
            {
                // Create a bitmap of the exact size we need (Game size or Primary Monitor size)
                var bmp = new Bitmap(bounds.Width, bounds.Height);

                using (var g = Graphics.FromImage(bmp))
                {
                    // CopyFromScreen grabs pixels directly from the graphics card output.
                    // This bypasses the "Black Screen" issue common with hardware-accelerated games.
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validates if a bitmap is usable (not null and not entirely black)
        /// </summary>
        private static bool IsValidScreenshot(Bitmap bitmap)
        {
            if (bitmap == null) return false;

            // Sample pixels to check if image is all black
            // We'll check 100 evenly distributed pixels for performance
            int width = bitmap.Width;
            int height = bitmap.Height;
            int sampleSize = 100;
            int blackPixels = 0;
            int whitePixels = 0;

            int stepX = Math.Max(1, width / 10);
            int stepY = Math.Max(1, height / 10);

            for (int y = 0; y < height && blackPixels < sampleSize; y += stepY)
            {
                for (int x = 0; x < width && blackPixels < sampleSize; x += stepX)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    // Consider pixel black if all RGB values are very low
                    if (pixel.R < 10 && pixel.G < 10 && pixel.B < 10)
                    {
                        blackPixels++;
                    }
                    // Consider pixel white if all RGB values are very high
                    else if (pixel.R > 245 && pixel.G > 245 && pixel.B > 245)
                    {
                        whitePixels++;
                    }
                }
            }
            
            bool isValid = false;

            // If more than 95% of sampled pixels are black or white, consider it invalid
            if (blackPixels < (sampleSize * 0.95))
                _logger.Warn("The captured image was invalid as it was all black.");
            else if (whitePixels < (sampleSize * 0.95))
                _logger.Warn("The captured image was invalid as it was all white.");
            // Check if the bitmap is unique
            else if (!IsUniqueImage(bitmap))
                _logger.Warn("The captured image was invalid as it was not unique.");
            else
                isValid = true;

            return isValid;
        }

        /// <summary>
        /// Tries caching a MD5 Hash of the Bitmap to know if the same screenshot was already taken.
        /// Checks raw pixel data for high performance.
        /// </summary>
        /// <returns>A <see cref="bool"/> representing if the screenshot was unique (true) or duplicate (false).</returns>
        private static bool IsUniqueImage(Bitmap bmp)
        {
            if (bmp == null) return false;

            // Lock bits to access raw pixel data directly (much faster than Save())
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

            try
            {
                // Calculate bytes needed
                int byteCount = Math.Abs(bmpData.Stride) * bmp.Height;
                byte[] pixelData = new byte[byteCount];

                // Copy from unmanaged memory to managed array
                Marshal.Copy(bmpData.Scan0, pixelData, 0, byteCount);

                using (MD5 md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(pixelData);

                    // Convert to Hex String to ensure correct comparison in HashSet
                    // (byte[] arrays are compared by reference, not content!)
                    string hashString = BitConverter.ToString(hashBytes);

                    lock (_lock)
                    {
                        // HashSet.Add returns true if the element is new
                        return _imageHashes.Add(hashString);
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }
        #endregion
    }
}
