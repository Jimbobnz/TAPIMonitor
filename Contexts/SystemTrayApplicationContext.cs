using System;
using System.Drawing;
using System.Windows.Forms;
using TapiMonitorApp.Helpers;
using TapiMonitorApp.Networking;
using TapiMonitorApp.Telephony;
using TapiMonitorApp.UI;
using static TapiMonitorApp.Networking.LocalTcpServer;

namespace TapiMonitorApp.Contexts
{
    public class SystemTrayApplicationContext : ApplicationContext
    {
        private NotifyIcon? _notifyIcon;
        private LogForm? _logForm;
        private LocalTcpServer? _tcpServer;
        private TapiEngine? _tapiEngine;
        private bool _showToasterNotifications = true;

        public SystemTrayApplicationContext()
        {
            InitializeComponents();
            StartServices();
        }

        private void InitializeComponents()
        {
            _logForm = new LogForm();

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem showLogsItem = new ToolStripMenuItem("View Verbose Logs", null, ShowLogs_Click);
            ToolStripMenuItem clientCountItem = new ToolStripMenuItem("Active Clients: 0") { Enabled = false };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit Application", null, Exit_Click);

            // 1. Define the Startup Toggle Item
            var startupMenuItem = new ToolStripMenuItem("Run at Windows Startup")
            {
                CheckOnClick = true,
                Checked = StartupHelper.IsConfiguredForStartup()
            };
            startupMenuItem.Click += (sender, e) => StartupHelper.SetRunAtStartup(startupMenuItem.Checked);

            // 2. Define the Toaster Pop-ups Toggle Item
            var toasterMenuItem = new ToolStripMenuItem("Enable Notification Pop-ups")
            {
                CheckOnClick = true,
                Checked = _showToasterNotifications // Defaults to checked/true
            };
            toasterMenuItem.Click += (sender, e) =>
            {
                _showToasterNotifications = toasterMenuItem.Checked;
                // Explicitly scoped to LocalTcpServer.LogType to fix compiler error CS0103
                Log($"Notification pop-ups have been {(_showToasterNotifications ? "enabled" : "disabled")}.", LocalTcpServer.LogType.Info);
            };

            // 3. Assemble the items onto the context menu in a clean visual layout
            contextMenu.Items.Add(showLogsItem);
            contextMenu.Items.Add(startupMenuItem);
            contextMenu.Items.Add(toasterMenuItem);
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

        private void Log(string message, LocalTcpServer.LogType type)
        {
            // FIX: Removed the invalid (LogForm.LogType) cast. 
            // We pass the type straight through since AppendLog already accepts LocalTcpServer.LogType!
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
            // Intercept hook: Exit early if the user turned off pop-ups via the menu toggle
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