using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
            try {
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
            try
            {
                logger.Debug($"Starting screenshot capture for '{gameTitle}' (PID: {processId})");
                Bitmap bmp = null;

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
                    logger.Debug("Performing full screen capture");
                    bmp = ScreenCapture.CaptureScreen();
                }

                if (bmp == null)
                {
                    logger.Warn("Capture returned null bitmap");
                    return;
                }

                var outFolder = settings?.Settings?.OutputFolder;
                if (string.IsNullOrWhiteSpace(outFolder))
                {
                    outFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "Plugins", "SharpMemories", "Screenshots");
                    logger.Debug($"Using default output folder: {outFolder}");
                }

                var safeTitle = FileHelpers.MakeSafeFilename(gameTitle ?? "game");

                outFolder = Path.Combine(outFolder, safeTitle);

                try { Directory.CreateDirectory(outFolder); } catch (Exception ex) { logger.Error(ex, "Failed to create output folder"); }

                var filename = Path.Combine(outFolder, $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bmp.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
                logger.Info($"Saved screenshot: {filename}");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error taking screenshot");
            }
        }
    }
}
