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
    /// <summary>
    /// Manages the automated and on-demand capturing of screenshots for running games.
    /// Handles the background loop, process monitoring, and file saving orchestration.
    /// </summary>
    public class ScreenshotCaptureManager
    {
        #region Fields
        private static readonly ILogger _logger = LogManager.GetLogger();
        private readonly SharpMemoriesSettingsViewModel _settings;
        private readonly object _captureLock = new object();

        private CancellationTokenSource _captureCts;
        private Task _captureTask;
        private int _currentGameProcessId = 0;
        private string _currentGameTitle = null;
        #endregion

        #region Constructors
        public ScreenshotCaptureManager(SharpMemoriesSettingsViewModel settings)
        {
            this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts the automatic capture loop for a specific game process.
        /// </summary>
        /// <param name="processId">The ID of the game process.</param>
        /// <param name="gameTitle">The display title of the game.</param>
        public void StartCaptureForProcess(int processId, string gameTitle)
        {
            lock (_captureLock)
            {
                // Ensure any previous session is cleanly stopped before starting a new one
                if (_captureCts != null)
                {
                    _logger.Debug("An existing capture session was found. Stopping it before starting the new session.");
                    StopCapture();
                }

                _logger.Info($"Initializing automatic capture session. Process ID: {processId} | Game: {gameTitle}");

                _currentGameProcessId = processId;
                _currentGameTitle = gameTitle;

                _captureCts = new CancellationTokenSource();
                _captureTask = Task.Run(() => CaptureLoop(processId, gameTitle, _captureCts.Token));
            }
        }

        /// <summary>
        /// Stops the current capture loop and cleans up resources.
        /// </summary>
        public void StopCapture()
        {
            lock (_captureLock)
            {
                try
                {
                    if (_captureCts != null)
                    {
                        _logger.Info($"Stopping capture session for: {_currentGameTitle ?? "Unknown Game"}");

                        _captureCts.Cancel();

                        // Wait briefly for the task to acknowledge cancellation
                        try
                        {
                            _captureTask?.Wait(2000);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn(ex, "Exception occurred while waiting for capture task to stop.");
                        }

                        _captureCts.Dispose();
                        _captureCts = null;
                        _logger.Debug("Capture resources disposed successfully.");
                    }
                }
                finally
                {
                    _captureTask = null;
                    _currentGameProcessId = 0;
                    _currentGameTitle = null;
                }
            }
        }

        /// <summary>
        /// Immediately captures a screenshot for the specified game, bypassing the timer.
        /// </summary>
        public void CaptureOnDemand(int processId, string gameTitle)
        {
            _logger.Info($"Manual trigger: Capturing screenshot for '{gameTitle}'");

            PerformCapture(processId, gameTitle);
            PlayCaptureSound();
        }
        #endregion

        #region Worker Methods
        /// <summary>
        /// The main background loop that triggers screenshots at configured intervals.
        /// </summary>
        private async Task CaptureLoop(int processId, string gameTitle, CancellationToken token)
        {
            try
            {
                var intervalMinutes = _settings.Settings?.IntervalMinutes ?? 30;

                // Enforce a sensible minimum to prevent spamming
                if (intervalMinutes <= 0) intervalMinutes = 30;

                var interval = TimeSpan.FromMinutes(intervalMinutes);

                _logger.Info($"Capture loop active. Next screenshot in {intervalMinutes} minutes.");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(interval, token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Debug("Capture loop delay cancelled. Exiting loop.");
                        break;
                    }

                    if (token.IsCancellationRequested) break;

                    // Execute capture on a background thread to ensure loop timing isn't blocked
                    await Task.Run(() => PerformCapture(processId, gameTitle), token);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error occurred within the CaptureLoop.");
            }
            finally
            {
                _logger.Info($"Capture loop terminated for '{gameTitle}'.");
            }
        }

        /// <summary>
        /// Orchestrates the logic of acquiring the image and saving it to disk.
        /// </summary>
        private void PerformCapture(int processId, string gameTitle)
        {
            Bitmap bmp = null;
            try
            {
                _logger.Debug($"Initiating capture routine for Process {processId} ({gameTitle})");

                // 1. Attempt to capture the specific game window
                bmp = TryCaptureGameWindow(processId);

                // 2. Fallback to primary screen if window capture failed
                if (bmp == null)
                {
                    _logger.Debug("Window capture unavailable. Falling back to primary screen capture.");
                    bmp = ScreenCapture.CapturePrimaryScreen();
                }

                // 3. If everything failed, abort
                if (bmp == null)
                {
                    _logger.Warn("Screenshot failed. Both window and screen capture returned null.");
                    return;
                }

                // 4. Determine output path
                string outputPath = PrepareOutputPath(gameTitle);

                // 5. Save the file based on settings
                var format = _settings.Settings.ScreenshotFormat;

                if (format == ScreenshotFormat.Png)
                {
                    var file = Path.Combine(outputPath, GenerateFileName(gameTitle, "png"));
                    FileHelpers.SaveAsPng(bmp, file);
                }
                else
                {
                    var file = Path.Combine(outputPath, GenerateFileName(gameTitle, "jpeg"));
                    FileHelpers.SaveAsJpeg(bmp, file, _settings.Settings.JpegQuality);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Critical error during screenshot execution for '{gameTitle}'.");
            }
            finally
            {
                // Always dispose the bitmap to prevent GDI+ memory leaks
                bmp?.Dispose();
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Attempts to find the game window and capture it. Returns null if minimized or not found.
        /// </summary>
        private Bitmap TryCaptureGameWindow(int processId)
        {
            if (processId <= 0) return null;

            try
            {
                var proc = Process.GetProcessById(processId);
                var handle = proc.MainWindowHandle;

                if (handle == IntPtr.Zero)
                {
                    _logger.Debug($"Process {processId} exists but has no main window handle.");
                    return null;
                }

                if (NativeMethods.IsIconic(handle))
                {
                    _logger.Debug($"Game process {processId} is currently minimized. Capture skipped.");
                    return null;
                }

                return ScreenCapture.CaptureWindow(handle);
            }
            catch (ArgumentException)
            {
                _logger.Warn($"Process {processId} is no longer running.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while attempting to capture game window.");
                return null;
            }
        }

        /// <summary>
        /// Prepares the directory structure for the screenshot and returns the folder path.
        /// </summary>
        private string PrepareOutputPath(string gameTitle)
        {
            var baseFolder = _settings.Settings?.OutputFolder;

            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Playnite", "Plugins", "SharpMemories", "Screenshots");
            }

            var safeTitle = FileHelpers.MakeSafeFilename(gameTitle ?? "UnknownGame");
            var gameFolder = Path.Combine(baseFolder, safeTitle);

            if (!Directory.Exists(gameFolder))
            {
                Directory.CreateDirectory(gameFolder);
            }

            return gameFolder;
        }

        private string GenerateFileName(string gameTitle, string extension)
        {
            var safeTitle = FileHelpers.MakeSafeFilename(gameTitle ?? "UnknownGame");
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{safeTitle}_{timestamp}.{extension}";
        }

        private void PlayCaptureSound()
        {
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not play capture sound.");
            }
        }
        #endregion
    }
}
