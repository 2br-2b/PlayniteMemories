using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace SharpMemories
{
    public partial class SharpMemoriesSettingsView : System.Windows.Controls.UserControl
    {
        public SharpMemoriesSettingsView()
        {
            InitializeComponent();
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