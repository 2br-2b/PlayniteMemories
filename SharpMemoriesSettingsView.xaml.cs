using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace SharpMemories
{
    public partial class SharpMemoriesSettingsView : System.Windows.Controls.UserControl
    {
        public SharpMemoriesSettingsView()
        {
            InitializeComponent();
        }

        private void RecordHotkey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as SharpMemoriesSettingsViewModel;
                if (vm == null) return;

                // Toggle recording state
                vm.IsRecordingHotkey = !vm.IsRecordingHotkey;

                if (vm.IsRecordingHotkey)
                {
                    // Start listening for key presses
                    this.PreviewKeyDown += OnRecordingKeyDown;
                    this.Focus();
                }
                else
                {
                    // Stop listening
                    this.PreviewKeyDown -= OnRecordingKeyDown;
                }
            }
            catch (Exception)
            {
                // ignore UI errors
            }
        }

        private void OnRecordingKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                var vm = DataContext as SharpMemoriesSettingsViewModel;
                if (vm == null || !vm.IsRecordingHotkey) return;

                // Get the actual key (not modifier keys)
                var key = e.Key == Key.System ? e.SystemKey : e.Key;

                // Ignore modifier keys by themselves
                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LWin || key == Key.RWin)
                {
                    e.Handled = true;
                    return;
                }

                // Get modifier states
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

                // Update the hotkey
                vm.UpdateHotkey(key, ctrl, alt, shift);

                // Stop recording
                vm.IsRecordingHotkey = false;
                this.PreviewKeyDown -= OnRecordingKeyDown;

                e.Handled = true;
            }
            catch (Exception)
            {
                // ignore UI errors
            }
        }

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as SharpMemoriesSettingsViewModel;
                if (vm == null) return;

                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Select output folder for screenshots";
                    if (!string.IsNullOrWhiteSpace(vm.Settings.OutputFolder))
                    {
                        try { dlg.SelectedPath = vm.Settings.OutputFolder; } catch { }
                    }

                    var result = dlg.ShowDialog();
                    if (result == DialogResult.OK || result == DialogResult.Yes)
                    {
                        vm.Settings.OutputFolder = dlg.SelectedPath;
                    }
                }
            }
            catch (Exception)
            {
                // ignore UI errors
            }
        }

        private void BrowseMonitorFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as SharpMemoriesSettingsViewModel;
                if (vm == null) return;

                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Select folder to monitor for screenshots";
                    if (!string.IsNullOrWhiteSpace(vm.Settings.MonitorFolder))
                    {
                        try { dlg.SelectedPath = vm.Settings.MonitorFolder; } catch { }
                    }

                    var result = dlg.ShowDialog();
                    if (result == DialogResult.OK || result == DialogResult.Yes)
                    {
                        vm.Settings.MonitorFolder = dlg.SelectedPath;
                    }
                }
            }
            catch (Exception)
            {
                // ignore UI errors
            }
        }
    }
}