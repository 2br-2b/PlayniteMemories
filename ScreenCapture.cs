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

        private const int SRCCOPY = 0x00CC0020;

        public static Bitmap CaptureScreen()
        {
            var hWnd = GetDesktopWindow();
            return CaptureWindow(hWnd);
        }

        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            IntPtr hdcSrc = IntPtr.Zero;
            IntPtr hdcDest = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                hdcSrc = GetWindowDC(hWnd);
                if (hdcSrc == IntPtr.Zero) return null;

                // Get the size using System.Drawing
                Rectangle rect;
                if (hWnd == GetDesktopWindow())
                {
                    rect = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                }
                else
                {
                    // Try to get window rect via managed API - fall back to primary screen
                    rect = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                }

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
    }
}
