using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMemories
{
    public class ScreenshotCaptureManager
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly SharpMemoriesSettingsViewModel settings;
        private CancellationTokenSource captureCts;
        private Task captureTask;
        private int currentGameProcessId = 0;
        private string currentGameTitle = null;
        private readonly object captureLock = new object();

        public ScreenshotCaptureManager(SharpMemoriesSettingsViewModel settings)
        {
            this.settings = settings;
        }

        public void StartCaptureForProcess(int processId, string gameTitle)
        {
            lock (captureLock)
            {
                // stop any existing capture
                if (captureCts != null)
                {
                    logger.Debug("Stopping existing capture before starting new one");
                }
                StopCapture();

                logger.Debug($"Initializing capture for process {processId}, game: {gameTitle}");
                currentGameProcessId = processId;
                currentGameTitle = gameTitle;
                captureCts = new CancellationTokenSource();
                captureTask = Task.Run(() => CaptureLoop(processId, gameTitle, captureCts.Token));
            }
        }

        public void StopCapture()
        {
            lock (captureLock)
            {
                try
                {
                    if (captureCts != null)
                    {
                        logger.Info($"Stopping capture loop for game: {currentGameTitle ?? "Unknown"}");
                        captureCts.Cancel();
                        try { captureTask?.Wait(2000); } catch { }
                        captureCts.Dispose();
                        captureCts = null;
                        logger.Debug("Capture task cancelled and disposed");
                    }
                    else
                    {
                        logger.Debug("No active capture to stop");
                    }
                }
                finally
                {
                    captureTask = null;
                    currentGameProcessId = 0;
                    currentGameTitle = null;
                }
            }
        }

        public void CaptureOnDemand(int processId, string gameTitle)
        {
            logger.Info($"On-demand screenshot capture triggered for '{gameTitle}'");
            CaptureOnce(processId, gameTitle);

            // Play a sound
            try 
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch (Exception e) 
            { 
                logger.Error(e, "Error playing sound on screenshot capture");
            }
        }

        private async Task CaptureLoop(int processId, string gameTitle, CancellationToken token)
        {
            try
            {
                var intervalMinutes = settings?.Settings?.IntervalMinutes ?? 30;
                if (intervalMinutes <= 0) intervalMinutes = 30;
                var interval = TimeSpan.FromMinutes(intervalMinutes);

                logger.Info($"Capture loop started for '{gameTitle}' with interval: {intervalMinutes} minutes");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        logger.Debug($"Waiting {intervalMinutes} minutes before next capture");
                        await Task.Delay(interval, token);
                    }
                    catch (TaskCanceledException)
                    {
                        logger.Debug("Capture loop cancelled during delay");
                        break;
                    }

                    if (token.IsCancellationRequested) break;

                    await Task.Run(() => CaptureOnce(processId, gameTitle));
                }

                logger.Info($"Capture loop ended for '{gameTitle}'");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error in CaptureLoop");
            }
        }

        private void CaptureOnce(int processId, string gameTitle)
        {
            Bitmap bmp = null;
            try
            {
                logger.Debug($"Starting screenshot capture for '{gameTitle}' (PID: {processId})");

                if (processId > 0)
                {
                    try
                    {
                        var proc = Process.GetProcessById(processId);
                        if (proc != null)
                        {
                            var h = proc.MainWindowHandle;
                            if (h != IntPtr.Zero)
                            {
                                // Is the game minimized?
                                if (ScreenCapture.IsIconic(h))
                                {
                                    logger.Debug($"Game process {processId} is minimized. Skipping capture.");
                                    return;
                                }

                                logger.Debug($"Attempting to capture window for process {processId}");
                                bmp = ScreenCapture.CaptureWindow(h);
                                if (bmp != null)
                                {
                                    logger.Debug("Window capture successful");
                                }
                                else
                                {
                                    logger.Debug("Window capture returned null, will fallback to screen capture");
                                }
                            }
                            else
                            {
                                logger.Debug("Process has no main window handle, will use screen capture");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug($"Failed to capture window: {ex.Message}");
                    }
                }
                else
                {
                    logger.Debug("No process ID available, using screen capture");
                }

                if (bmp == null)
                {
                    logger.Debug("Window capture failed. Performing full screen capture of the primary screen.");
                    bmp = ScreenCapture.CapturePrimaryScreen();
                    if (bmp == null)
                    {
                        logger.Warn("Even full screen capture failed. No screenshot taken.");
                        return;
                    }
                }

                var outFolder = settings?.Settings?.OutputFolder;
                if (string.IsNullOrWhiteSpace(outFolder))
                {
                    outFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "Plugins", "SharpMemories", "Screenshots");
                    logger.Debug($"Using default output folder: {outFolder}");
                }

                var safeTitle = FileHelpers.MakeSafeFilename(gameTitle ?? "game");

                outFolder = Path.Combine(outFolder, safeTitle);

                Directory.CreateDirectory(outFolder);
                string filename = string.Empty;

                if (settings.Settings.ScreenshotFormat == ScreenshotFormat.Png)
                {
                    filename = Path.Combine(outFolder, $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    SaveUncompressed(bmp, filename);
                }
                else if(settings.Settings.ScreenshotFormat == ScreenshotFormat.Jpeg)
                {
                    filename = Path.Combine(outFolder, $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.jpeg");
                    SaveCompressed(bmp, filename, settings.Settings.JpegQuality);
                }

                logger.Info($"Saved screenshot: {filename}");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error taking screenshot");
            }
            finally
            {
                bmp?.Dispose();
            }
        }

        public static void SaveUncompressed(Bitmap bmp, string path)
        {
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        /// <summary>
        /// Saves a bitmap as a JPEG with the specified quality level.
        /// </summary>
        /// <param name="bmp">The image to save.</param>
        /// <param name="path">The full output path.</param>
        /// <param name="quality">Quality from 0 to 100 (Default 85 is a good balance).</param>
        public static void SaveCompressed(Bitmap bmp, string path, long quality = 85)
        {
            try
            {
                if (quality < 0 || quality > 100)
                    quality = 85;

                // Get the JPEG codec
                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

                // Create an Encoder object based on the GUID for the Quality parameter category
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;

                // Create an EncoderParameters object
                // An array of EncoderParameter objects
                EncoderParameters myEncoderParameters = new EncoderParameters(1);

                // Save the bitmap as a JPEG file with the specified quality level
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, quality);
                myEncoderParameters.Param[0] = myEncoderParameter;

                bmp.Save(path, jpgEncoder, myEncoderParameters);
            }
            catch (Exception ex)
            {
                // Fallback to standard save if compression fails
                bmp.Save(path, ImageFormat.Jpeg);
                throw new Exception("Error compressing image, saved as standard JPEG.", ex);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
        }
    }
}
