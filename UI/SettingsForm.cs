using System;
using System.Windows.Forms;
using Microsoft.Win32;
using TapiMonitorApp.Helpers; // Required to access your StartupHelper

namespace TapiMonitorApp.UI
{
    public class SettingsForm : Form
    {
        private NumericUpDown? _numPort;
        private NumericUpDown? _numMaxConn;
        private CheckBox? _chkStartup;
        private CheckBox? _chkNotifications;
        private Button? _btnSave;
        private Button? _btnCancel;

        private const string RegistryKeyPath = @"SOFTWARE\TapiMonitorApp";

        public SettingsForm()
        {
            InitializeComponent();
            LoadSettingsFromRegistry();
        }

        private void InitializeComponent()
        {
            this.Text = "Monitor Configuration";
            this.Size = new System.Drawing.Size(290, 250); // Height expanded to accommodate both checkboxes cleanly
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. TCP Port
            Label lblPort = new Label { Text = "TCP Port Number:", Left = 15, Top = 20, Width = 110 };
            _numPort = new NumericUpDown { Left = 130, Top = 18, Width = 120, Minimum = 1, Maximum = 65535 };

            // 2. Max Connections
            Label lblMaxConn = new Label { Text = "Max Connections:", Left = 15, Top = 55, Width = 110 };
            _numMaxConn = new NumericUpDown { Left = 130, Top = 53, Width = 120, Minimum = 1, Maximum = 100 };

            // 3. Run at Windows Startup Checkbox
            _chkStartup = new CheckBox
            {
                Text = "Run at Windows Startup",
                Left = 18,
                Top = 90,
                Width = 230,
                FlatStyle = FlatStyle.System
            };

            // 4. Notification Pop-ups Checkbox
            _chkNotifications = new CheckBox
            {
                Text = "Enable Notification Pop-ups",
                Left = 18,
                Top = 120,
                Width = 230,
                FlatStyle = FlatStyle.System
            };

            // 5. Action Buttons
            _btnSave = new Button { Text = "Save", Left = 55, Top = 165, Width = 80, DialogResult = DialogResult.OK };
            _btnCancel = new Button { Text = "Cancel", Left = 145, Top = 165, Width = 80, DialogResult = DialogResult.Cancel };

            _btnSave.Click += BtnSave_Click;

            this.Controls.Add(lblPort);
            this.Controls.Add(_numPort);
            this.Controls.Add(lblMaxConn);
            this.Controls.Add(_numMaxConn);
            this.Controls.Add(_chkStartup);
            this.Controls.Add(_chkNotifications);
            this.Controls.Add(_btnSave);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        private void LoadSettingsFromRegistry()
        {
            // Load Network & Notification settings from Registry
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                int port = (key?.GetValue("TcpServerPort") is int p) ? p : 1471;
                int maxConn = (key?.GetValue("TcpMaxConnections") is int m) ? m : 10;
                string notifySetting = key?.GetValue("ShowToasterNotifications")?.ToString() ?? "True";

                if (_numPort != null) _numPort.Value = port;
                if (_numMaxConn != null) _numMaxConn.Value = maxConn;
                if (_chkNotifications != null)
                {
                    _chkNotifications.Checked = !notifySetting.Equals("False", StringComparison.OrdinalIgnoreCase);
                }
            }

            // Load Startup checkbox using your existing Helper class logic
            if (_chkStartup != null)
            {
                _chkStartup.Checked = StartupHelper.IsConfiguredForStartup();
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                // Commit Network & Notification settings to Registry
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    if (_numPort != null) key.SetValue("TcpServerPort", (int)_numPort.Value);
                    if (_numMaxConn != null) key.SetValue("TcpMaxConnections", (int)_numMaxConn.Value);
                    if (_chkNotifications != null) key.SetValue("ShowToasterNotifications", _chkNotifications.Checked.ToString());
                }

                // Commit Startup choice via your existing Helper class logic
                if (_chkStartup != null)
                {
                    StartupHelper.SetRunAtStartup(_chkStartup.Checked);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration metrics: {ex.Message}", "Registry Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}