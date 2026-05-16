using System;
using System.Drawing;
using System.Windows.Forms;
using TapiMonitorApp.Networking;

namespace TapiMonitorApp.UI
{
    public class LogForm : Form
    {
        private RichTextBox? _logBox;

        public LogForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _logBox = new RichTextBox();
            this.SuspendLayout();
            
            _logBox.BackColor = Color.FromArgb(24, 24, 24);
            _logBox.Dock = DockStyle.Fill;
            _logBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _logBox.ForeColor = Color.Gainsboro;
            _logBox.ReadOnly = true;
            _logBox.Text = "";
            
            this.ClientSize = new Size(750, 450);
            this.Controls.Add(_logBox);
            this.Name = "LogForm";
            this.Text = "TAPI Monitor Service Console Logs";
            
            this.FormClosing += (s, e) => {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
            this.ResumeLayout(false);
        }

        public void AppendLog(string message, LocalTcpServer.LogType type)
        {
            if (this.IsDisposed || _logBox == null) return;

            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action(() => AppendLog(message, type)));
                return;
            }

            Color logColor = type switch
            {
                LocalTcpServer.LogType.Success => Color.LimeGreen,
                LocalTcpServer.LogType.Error => Color.Crimson,
                LocalTcpServer.LogType.Warning => Color.Orange,
                LocalTcpServer.LogType.Event => Color.Cyan,
                LocalTcpServer.LogType.Tapi => Color.Magenta,
                _ => Color.DarkGray
            };

            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = logColor;
            _logBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type.ToString().ToUpper()}] {message}{Environment.NewLine}");
            _logBox.SelectionColor = _logBox.ForeColor;
            
            _logBox.SelectionStart = _logBox.Text.Length;
            _logBox.ScrollToCaret();
        }
    }
}
