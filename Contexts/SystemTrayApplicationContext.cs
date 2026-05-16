using System;
using System.Drawing;
using System.Windows.Forms;
using TapiMonitorApp.Helpers;
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

        public SystemTrayApplicationContext()
        {
            InitializeComponents();
            StartServices();
        }

        private void InitializeComponents()
        {
            _logForm = new LogForm();

            // 1. Create the menu strip container
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // 2. Define the core menu items
            ToolStripMenuItem showLogsItem = new ToolStripMenuItem("View Verbose Logs", null, ShowLogs_Click);
            ToolStripMenuItem clientCountItem = new ToolStripMenuItem("Active Clients: 0") { Enabled = false };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit Application", null, Exit_Click);

            // 3. Define the startup toggle item (Moved up for clean logical grouping)
            var startupMenuItem = new ToolStripMenuItem("Run at Windows Startup")
            {
                CheckOnClick = true,
                Checked = StartupHelper.IsConfiguredForStartup()
            };

            // Wire up the startup registry click handler
            startupMenuItem.Click += (sender, e) =>
            {
                StartupHelper.SetRunAtStartup(startupMenuItem.Checked);
            };

            // 4. Assemble the items onto the context menu in a clean visual hierarchy
            contextMenu.Items.Add(showLogsItem);
            contextMenu.Items.Add(startupMenuItem); // <─── FIXED: Added to contextMenu 
            contextMenu.Items.Add(clientCountItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            // 5. Link the finalized context menu directly to the tray notify icon
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                ContextMenuStrip = contextMenu, // <─── This binds the menu to the icon
                Text = "TAPI Engine Live Monitor",
                Visible = true
            };

            _notifyIcon.DoubleClick += ShowLogs_Click;

            // Dynamic client count update wrapper when user right-clicks the tray icon
            contextMenu.Opening += (s, e) =>
            {
                if (_tcpServer != null)
                {
                    clientCountItem.Text = $"Active Clients: {_tcpServer.ActiveClientCount}";
                }
            };
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
