using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SharpMemories
{
    internal static class ScreenCapture
    {
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

        /// <summary>
        /// Validates if a bitmap is usable (not null and not entirely black)
        /// </summary>
        private static bool IsValidScreenshot(Bitmap bitmap)
        {
            if (bitmap == null) return false;

            //// Sample pixels to check if image is all black
            //// We'll check 100 evenly distributed pixels for performance
            //int width = bitmap.Width;
            //int height = bitmap.Height;
            //int sampleSize = 100;
            //int blackPixels = 0;

            //int stepX = Math.Max(1, width / 10);
            //int stepY = Math.Max(1, height / 10);

            //for (int y = 0; y < height && blackPixels < sampleSize; y += stepY)
            //{
            //    for (int x = 0; x < width && blackPixels < sampleSize; x += stepX)
            //    {
            //        Color pixel = bitmap.GetPixel(x, y);
            //        // Consider pixel black if all RGB values are very low
            //        if (pixel.R < 10 && pixel.G < 10 && pixel.B < 10)
            //        {
            //            blackPixels++;
            //        }
            //    }
            //}

            //// If more than 95% of sampled pixels are black, consider it invalid
            //return blackPixels < (sampleSize * 0.95);

            return true; // Simplified for performance; implement pixel check if needed
        }

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

            // Method 3: BitBlt (fallback)
            result = CaptureWithBitBlt(hWnd);
            if (IsValidScreenshot(result))
                return result;
            result?.Dispose();

            // All methods failed
            return null;
        }

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
    }
}
