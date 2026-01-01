using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SharpMemories
{
    /// <summary>
    /// Provides methods to validate the quality and uniqueness of captured screenshots.
    /// </summary>
    internal static class ScreenshotValidator
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();
        private static readonly HashSet<string> _imageHashes = new HashSet<string>();
        private static readonly object _lock = new object();
        #endregion

        #region Public Methods
        /// <summary>
        /// Validates if a bitmap is usable (not null, not entirely black/white, and unique).
        /// </summary>
        /// <param name="bitmap">The bitmap to validate.</param>
        /// <returns>True if the screenshot is valid; otherwise, false.</returns>
        public static bool IsValidScreenshot(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return false;
            }

            // check for content validity (mostly black or mostly white screens)
            if (IsImageBlank(bitmap))
            {
                return false;
            }

            // Check if the bitmap is a duplicate of a previously captured frame
            if (!IsUniqueImage(bitmap))
            {
                _logger.Debug("The captured image was rejected because it is a duplicate of a previous frame.");
                return false;
            }

            return true;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Samples pixels to check if the image is overwhelmingly black or white.
        /// </summary>
        private static bool IsImageBlank(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            // We sample ~100 pixels for performance instead of scanning the whole image
            int sampleSize = 100;
            int blackPixels = 0;
            int whitePixels = 0;
            int totalSampled = 0;

            int stepX = Math.Max(1, width / 10);
            int stepY = Math.Max(1, height / 10);

            for (int y = 0; y < height && totalSampled < sampleSize; y += stepY)
            {
                for (int x = 0; x < width && totalSampled < sampleSize; x += stepX)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    totalSampled++;

                    // Check for near-black
                    if (pixel.R < 10 && pixel.G < 10 && pixel.B < 10)
                    {
                        blackPixels++;
                    }
                    // Check for near-white
                    else if (pixel.R > 245 && pixel.G > 245 && pixel.B > 245)
                    {
                        whitePixels++;
                    }
                }
            }

            // If more than 95% of sampled pixels are black
            if (blackPixels >= (totalSampled * 0.95))
            {
                _logger.Warn("The captured image was invalid because it was mostly black.");
                return true;
            }

            // If more than 95% of sampled pixels are white
            if (whitePixels >= (totalSampled * 0.95))
            {
                _logger.Warn("The captured image was invalid because it was mostly white.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates an MD5 hash of the raw pixel data to determine uniqueness.
        /// </summary>
        private static bool IsUniqueImage(Bitmap bmp)
        {
            // Lock bits to access raw pixel data directly (significantly faster than iterating pixels)
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

            try
            {
                int byteCount = Math.Abs(bmpData.Stride) * bmp.Height;
                byte[] pixelData = new byte[byteCount];

                // Copy unmanaged memory to managed array
                Marshal.Copy(bmpData.Scan0, pixelData, 0, byteCount);

                using (MD5 md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(pixelData);
                    string hashString = BitConverter.ToString(hashBytes);

                    lock (_lock)
                    {
                        // HashSet.Add returns true if the element is new, false if it exists
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
