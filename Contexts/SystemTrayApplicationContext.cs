using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using TapiMonitorApp.Networking;
using TapiMonitorApp.Telephony;
using TapiMonitorApp.UI;

namespace TapiMonitorApp.Contexts
{
    public class SystemTrayApplicationContext : ApplicationContext
    {
        private NotifyIcon? _notifyIcon;
        private LogForm? _logForm;
        private LocalTcpServer? _tcpServer;
        private TapiEngine? _tapiEngine;
        private bool _showToasterNotifications = true;

        private const string RegistryKeyPath = @"SOFTWARE\TapiMonitorApp";

        public SystemTrayApplicationContext()
        {
            RefreshNotificationPreference();
            InitializeComponents();
            StartServices();
        }

        private void RefreshNotificationPreference()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                string savedSetting = key?.GetValue("ShowToasterNotifications")?.ToString() ?? "True";
                _showToasterNotifications = !savedSetting.Equals("False", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void InitializeComponents()
        {
            _logForm = new LogForm();

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem showLogsItem = new ToolStripMenuItem("View Verbose Logs", null, ShowLogs_Click);
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("Configuration Settings...", null, Settings_Click);
            ToolStripMenuItem clientCountItem = new ToolStripMenuItem("Active Clients: 0") { Enabled = false };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Quit", null, Exit_Click);

            // Assemble streamlined items into context menu layout structure
            contextMenu.Items.Add(showLogsItem);
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(clientCountItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                ContextMenuStrip = contextMenu,
                Text = "TAPI Engine Live Monitor",
                Visible = true
            };

            _notifyIcon.DoubleClick += ShowLogs_Click;

            contextMenu.Opening += (s, e) =>
            {
                if (_tcpServer != null)
                {
                    clientCountItem.Text = $"Active Clients: {_tcpServer.ActiveClientCount}";
                }
            };
        }

        private void Settings_Click(object? sender, EventArgs e)
        {
            using (SettingsForm form = new SettingsForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    // Pull down the checkbox updates locally right away
                    RefreshNotificationPreference();

                    Log("Configuration settings updated successfully.", LocalTcpServer.LogType.Success);
                    Log("Note: If you altered the Port or Max Connection values, please select Quit and restart the monitor application to apply network changes.", LocalTcpServer.LogType.Warning);
                }
            }
        }

        private void Log(string message, LocalTcpServer.LogType type)
        {
            _logForm?.AppendLog($"[Tray] {message}", type);
        }

        private void StartServices()
        {
            _tcpServer = new LocalTcpServer();
            if (_logForm != null) _tcpServer.OnLog += _logForm.AppendLog;

            _tapiEngine = new TapiEngine(_tcpServer);
            if (_logForm != null) _tapiEngine.OnLog += _logForm.AppendLog;
            _tapiEngine.OnIncomingCallAlert += DisplayToastAlert;

            _tcpServer.Start();
            _tapiEngine.Initialize();
        }

        private void DisplayToastAlert(string title, string text)
        {
            // Correctly handles the user configuration updated via the Settings dialog
            if (!_showToasterNotifications) return;

            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
            }
        }

        private void ShowLogs_Click(object? sender, EventArgs e)
        {
            if (_logForm != null && !_logForm.IsDisposed)
            {
                _logForm.Show();
                _logForm.WindowState = FormWindowState.Normal;
                _logForm.Activate();
            }
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            _tapiEngine?.Shutdown();
            _tcpServer?.Stop();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _logForm?.Dispose();

            ExitThread();
        }
    }
}