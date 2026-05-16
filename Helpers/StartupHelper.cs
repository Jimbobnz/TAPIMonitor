using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace TapiMonitorApp.Helpers
{
    public static class StartupHelper
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TapiMonitorApp"; // The name of the sub-key in the registry

        /// <summary>
        /// Toggles the Windows Startup registration for this executable.
        /// </summary>
        public static void SetRunAtStartup(bool runAtStartup)
        {
            try
            {
                // Open the Current User registry hive for the Run key path
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true)!)
                {
                    if (runAtStartup)
                    {
                        // Get the path to your compiled application .exe file
                        string exePath = Application.ExecutablePath;

                        // Set the value (e.g., "TapiMonitorApp" = "D:\TAPIMonitor\...\TapiMonitorApp.exe")
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else
                    {
                        // Remove the sub-key if it exists
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to modify startup registry key: {ex.Message}",
                                "Registry Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Checks if the application is currently configured to run at startup.
        /// </summary>
        public static bool IsConfiguredForStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false)!)
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}