using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace MyEmguProject
{
    public partial class ReturnValuesWindow : Form
    {
        private SerialPort? _serialPort;
        private TextBox txtLog = null!;
        private Button btnClear = null!;
        private Button btnSaveAs = null!;     // Единственная кнопка сохранения

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
            this.Text = "История состояний соленоидов и ключей";
            this.Size = new Size(620, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = true;
            this.MaximizeBox = true;
            this.BackColor = Color.FromArgb(30, 30, 40);
            this.Font = new Font("Segoe UI", 10);
        }

        private void SetupUI()
        {
            // Основное поле лога
            txtLog = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                Padding = new Padding(10)
            };
            this.Controls.Add(txtLog);

            // Панель кнопок внизу
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(35, 35, 45)
            };
            this.Controls.Add(buttonPanel);

            // Кнопка Очистить
            btnClear = new Button
            {
                Text = "Очистить",
                Size = new Size(110, 38),
                BackColor = Color.FromArgb(80, 80, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnClear.Click += BtnClear_Click;
            buttonPanel.Controls.Add(btnClear);

            // Кнопка Сохранить как...
            btnSaveAs = new Button
            {
                Text = "Сохранить как...",
                Size = new Size(180, 38),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnSaveAs.Click += BtnSaveAs_Click;
            buttonPanel.Controls.Add(btnSaveAs);

            // Отступ для текстового поля
            txtLog.Margin = new Padding(0, 0, 0, 60);
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
                    this.Invoke((MethodInvoker)delegate
                    {
                        AppendLog(data);
                    });
                }
            }
            catch { }
        }

        public void AddLog(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            this.Invoke((MethodInvoker)delegate
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
            if (MessageBox.Show("Очистить всю историю?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
                    MessageBox.Show("История пуста. Нечего сохранять.",
                        "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("=================================================================");
                sb.AppendLine("          ИСТОРИЯ ВКЛЮЧЕНИЙ И ВЫКЛЮЧЕНИЙ");
                sb.AppendLine($"          STM32 Соленоиды + Ключи");
                sb.AppendLine($"          Сохранено: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=================================================================");
                sb.AppendLine();
                sb.Append(txtLog.Text.TrimEnd());

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"История успешно сохранена!\n\n{filePath}",
                    "Сохранение завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}",
                    "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                catch { }
            }
            base.OnFormClosing(e);
        }
    }
}