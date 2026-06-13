using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace MyEmguProject
{
    public partial class ReturnValuesWindow : Form
    {
        private readonly SerialPort? _serialPort;
        private TextBox txtLog = null!;
        private Button btnClear = null!;
        private Button btnSaveAs = null!;
        private TableLayoutPanel? _root;
        private Panel? _headerPanel;
        private Label? _lblTitle;
        private Label? _lblSubtitle;
        private Panel? _logCard;
        private FlowLayoutPanel? _buttonPanel;

        private static Color BgPrimary => AppTheme.Current.BgPrimary;
        private static Color BgSurface => AppTheme.Current.SurfacePrimary;
        private static Color BgElevated => AppTheme.IsDark ? Color.FromArgb(38, 46, 58) : Color.FromArgb(252, 255, 254);
        private static Color BorderMuted => AppTheme.Current.BorderSoft;
        private static Color AccentBlue => AppTheme.Current.AccentBlue;
        private static Color TextPrimary => AppTheme.IsDark ? Color.FromArgb(238, 244, 255) : AppTheme.Current.TextPrimary;
        private static Color TextSecondary => AppTheme.IsDark ? Color.FromArgb(179, 194, 214) : AppTheme.Current.TextSecondary;

        public ReturnValuesWindow(SerialPort? serialPort = null)
        {
            _serialPort = serialPort;

            InitializeWindow();
            SetupUI();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                StartListening();
            }
        }

        private void InitializeWindow()
        {
            Text = "История состояний соленоидов и ключей";
            Size = new Size(700, 600);
            MinimumSize = new Size(640, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = true;
            MaximizeBox = true;
            BackColor = BgPrimary;
            Font = new Font("Segoe UI Semibold", 9.75f);
        }

        private void SetupUI()
        {
            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = BgPrimary,
                Padding = new Padding(14),
                Margin = new Padding(0)
            };
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            Controls.Add(_root);

            _headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSurface,
                Padding = new Padding(14, 10, 14, 8)
            };
            _root.Controls.Add(_headerPanel, 0, 0);

            _lblTitle = new Label
            {
                Text = "История переключений",
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 0)
            };
            _headerPanel.Controls.Add(_lblTitle);

            _lblSubtitle = new Label
            {
                Text = "Временная лента сигналов SW/KEY и ответов STM32",
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Location = new Point(1, 30)
            };
            _headerPanel.Controls.Add(_lblSubtitle);

            _logCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSurface,
                Padding = new Padding(2),
                Margin = new Padding(0, 12, 0, 12)
            };
            _root.Controls.Add(_logCard, 0, 1);

            txtLog = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = BgElevated,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10f),
                Padding = new Padding(12)
            };
            _logCard.Controls.Add(txtLog);

            _buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = BgPrimary
            };
            _root.Controls.Add(_buttonPanel, 0, 2);

            btnClear = new Button
            {
                Text = "Очистить",
                Size = new Size(118, 40)
            };
            StyleActionButton(btnClear, Color.FromArgb(125, 146, 168));
            btnClear.Click += BtnClear_Click;
            _buttonPanel.Controls.Add(btnClear);

            btnSaveAs = new Button
            {
                Text = "Сохранить как...",
                Size = new Size(170, 40),
                Margin = new Padding(12, 3, 3, 3)
            };
            StyleActionButton(btnSaveAs, AccentBlue);
            btnSaveAs.Click += BtnSaveAs_Click;
            _buttonPanel.Controls.Add(btnSaveAs);

            ApplyTheme();
        }

        private static void StyleActionButton(Button button, Color backColor)
        {
            button.BackColor = backColor;
            button.ForeColor = AppTheme.IsDark && backColor == Color.White ? Color.Black : Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
        }

        public void ApplyTheme()
        {
            BackColor = BgPrimary;
            if (_root != null) _root.BackColor = BgPrimary;
            if (_headerPanel != null) _headerPanel.BackColor = BgSurface;
            if (_logCard != null) _logCard.BackColor = BgSurface;
            if (_buttonPanel != null) _buttonPanel.BackColor = BgPrimary;
            if (_lblTitle != null) _lblTitle.ForeColor = TextPrimary;
            if (_lblSubtitle != null) _lblSubtitle.ForeColor = TextSecondary;
            if (txtLog != null)
            {
                txtLog.BackColor = BgElevated;
                txtLog.ForeColor = TextPrimary;
            }
            if (btnClear != null) StyleActionButton(btnClear, AppTheme.IsDark ? Color.White : Color.FromArgb(125, 146, 168));
            if (btnSaveAs != null) StyleActionButton(btnSaveAs, AppTheme.IsDark ? Color.White : AccentBlue);
            Invalidate(true);
        }

        private void StartListening()
        {
            _serialPort!.DataReceived += SerialPort_DataReceived;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort!.ReadExisting().Trim();
                if (!string.IsNullOrWhiteSpace(data))
                {
                    Invoke((MethodInvoker)delegate
                    {
                        AppendLog(data);
                    });
                }
            }
            catch
            {
            }
        }

        public void AddLog(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Invoke((MethodInvoker)delegate
            {
                txtLog.AppendText(line + Environment.NewLine);
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();
            });
        }

        private void AppendLog(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Очистить всю историю?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                txtLog.Clear();
            }
        }

        private void BtnSaveAs_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.log)|*.log|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"STM32_History_{DateTime.Now:yyyyMMdd_HHmmss}.log",
                Title = "Сохранить историю состояний",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                SaveLogToFile(sfd.FileName);
            }
        }

        private void SaveLogToFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtLog.Text))
                {
                    MessageBox.Show("История пуста. Нечего сохранять.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("=================================================================");
                sb.AppendLine("                 ИСТОРИЯ ВКЛЮЧЕНИЙ И ВЫКЛЮЧЕНИЙ");
                sb.AppendLine("                 STM32 Соленоиды + Ключи");
                sb.AppendLine($"                 Сохранено: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=================================================================");
                sb.AppendLine();
                sb.Append(txtLog.Text.TrimEnd());

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"История успешно сохранена!\n\n{filePath}", "Сохранение завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_serialPort != null)
            {
                try
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                }
                catch
                {
                }
            }

            base.OnFormClosing(e);
        }
    }
}
