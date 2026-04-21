using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;

namespace MyEmguProject
{
    public partial class BoardWindow : Form
    {
        private VideoCapture? _capture;
        private System.Windows.Forms.Timer? _frameTimer;

        private SerialPort? _serialPort;
        private Thread? _readThread;
        private volatile bool _running = true;

        private readonly Panel _videoPanel = new();
        private readonly PictureBox _pictureBox = new();
        private readonly Label _lblStatus = new();
        private Button? _btnBack;

        private readonly DipSwitch[] _switches = new DipSwitch[10];
        private RoundButton? _btnKey0;
        private RoundButton? _btnKey1;

        private int _currentCameraIndex = -1;
        private Bitmap? _lastBitmap;

        private record CameraInfo(int Index, string Name);

        // Режим редактирования оверлея
        private bool _editMode = false;
        private Control? _draggedControl;
        private bool _resizing = false;
        private Size _startSize;
        private Point _mouseDownGlobal;
        private Point _controlStartGlobal;

        private long _previousState = -1;
        private ReturnValuesWindow? _logWindow = null;

        private long currentOutputState = 0; // 0..4194303 (22 бита)

        // Флаг для отображения оверлея
        private bool _showOverlay = true;

        // Верхняя панель
        private ToolStrip? _topToolStrip;
        private ToolStripDropDownButton? _btnHideMenu;
        private ToolStripMenuItem? _menuToggleOverlay;
        private ToolStripMenuItem? _menuToggleSwitches;
        private ToolStripButton? _toolBtnEditOverlay;
        private ToolStripButton? _toolBtnHistory;
        private ComboBox? _cmbCamera;

        // Новые меню
        private ToolStripDropDownButton? _btnModeMenu;
        private ToolStripDropDownButton? _btnBoardMenu;
        private ToolStripDropDownButton? _btnCourseMenu;

        private ToolStripMenuItem? _menuModeTraining;
        private ToolStripMenuItem? _menuModeDebug;
        private ToolStripMenuItem? _menuModeTest;

        private ToolStripMenuItem? _menuBoardDE10Lite;
        private ToolStripMenuItem? _menuBoardDE0CV;
        private ToolStripMenuItem? _menuBoardDE0Nano;
        private ToolStripMenuItem? _menuBoardDE1SoC;
        private ToolStripMenuItem? _menuBoardDE10Standard;
        private ToolStripMenuItem? _menuBoardDE10Nano;

        private ToolStripMenuItem? _menuCourseDigitalLogic;
        private ToolStripMenuItem? _menuCourseArchitecture;
        private ToolStripMenuItem? _menuCourseEmbedded;
        private ToolStripMenuItem? _menuCourseFPGABasics;

        private string _selectedMode = "Учебный";
        private string _selectedBoard = "DE10-Lite";
        private string _selectedCourse = "Без курса";

        private static readonly Color BgPrimary = Color.FromArgb(19, 23, 31);
        private static readonly Color BgSurface = Color.FromArgb(30, 36, 46);
        private static readonly Color BgElevated = Color.FromArgb(38, 46, 58);
        private static readonly Color BorderMuted = Color.FromArgb(74, 93, 118);
        private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
        private static readonly Color TextPrimary = Color.FromArgb(238, 244, 255);
        private static readonly Color TextSecondary = Color.FromArgb(179, 194, 214);

        public BoardWindow()
        {
            InitializeComponent();
            SetupUI();
            InitializeSerialPort();
            StartReadingThread();
            TryOpenFirstAvailableCamera();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.Size = new Size(1180, 820);
            this.MinimumSize = this.Size;
            this.MaximumSize = this.Size;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Управление платой STM32 (22 выхода)";
            this.BackColor = BgPrimary;
            this.Font = new Font("Segoe UI Semibold", 9.75f);
            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = this.BackColor,
                Margin = new Padding(0),
                Padding = new Padding(14, 12, 14, 14)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            this.Controls.Add(rootLayout);

            // Верхняя панель
            _topToolStrip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = BgSurface,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Regular),
                Padding = new Padding(10, 7, 10, 7),
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new ModernToolStripRenderer(BgSurface, BgElevated, BorderMuted, AccentBlue)
            };

            // Скрыть
            _btnHideMenu = new ToolStripDropDownButton("Скрыть")
            {
                ForeColor = Color.White
            };

            _menuToggleOverlay = new ToolStripMenuItem("Скрыть оверлей");
            _menuToggleOverlay.Click += (s, e) => ToggleOverlayVisibility();

            _menuToggleSwitches = new ToolStripMenuItem("Скрыть SW/KEY");
            _menuToggleSwitches.Click += (s, e) => ToggleSwitchAndKeysVisibility();

            _btnHideMenu.DropDownItems.Add(_menuToggleOverlay);
            _btnHideMenu.DropDownItems.Add(_menuToggleSwitches);

            // Режим работы
            _btnModeMenu = new ToolStripDropDownButton("Режим работы")
            {
                ForeColor = Color.White
            };

            _menuModeTraining = new ToolStripMenuItem("Учебный");
            _menuModeTraining.Click += (s, e) => SelectMode("Учебный");

            _menuModeDebug = new ToolStripMenuItem("Отладка");
            _menuModeDebug.Click += (s, e) => SelectMode("Отладка");

            _menuModeTest = new ToolStripMenuItem("Тест");
            _menuModeTest.Click += (s, e) => SelectMode("Тест");

            _btnModeMenu.DropDownItems.Add(_menuModeTraining);
            _btnModeMenu.DropDownItems.Add(_menuModeDebug);
            _btnModeMenu.DropDownItems.Add(_menuModeTest);

            // Выбор платы
            _btnBoardMenu = new ToolStripDropDownButton("Выбор платы")
            {
                ForeColor = Color.White
            };

            _menuBoardDE10Lite = new ToolStripMenuItem("DE10-Lite");
            _menuBoardDE10Lite.Click += (s, e) => SelectBoard("DE10-Lite");

            _menuBoardDE0CV = new ToolStripMenuItem("DE0-CV");
            _menuBoardDE0CV.Click += (s, e) => SelectBoard("DE0-CV");

            _menuBoardDE0Nano = new ToolStripMenuItem("DE0-Nano");
            _menuBoardDE0Nano.Click += (s, e) => SelectBoard("DE0-Nano");

            _menuBoardDE1SoC = new ToolStripMenuItem("DE1-SoC");
            _menuBoardDE1SoC.Click += (s, e) => SelectBoard("DE1-SoC");

            _menuBoardDE10Standard = new ToolStripMenuItem("DE10-Standard");
            _menuBoardDE10Standard.Click += (s, e) => SelectBoard("DE10-Standard");

            _menuBoardDE10Nano = new ToolStripMenuItem("DE10-Nano");
            _menuBoardDE10Nano.Click += (s, e) => SelectBoard("DE10-Nano");

            _btnBoardMenu.DropDownItems.Add(_menuBoardDE10Lite);
            _btnBoardMenu.DropDownItems.Add(_menuBoardDE0CV);
            _btnBoardMenu.DropDownItems.Add(_menuBoardDE0Nano);
            _btnBoardMenu.DropDownItems.Add(_menuBoardDE1SoC);
            _btnBoardMenu.DropDownItems.Add(_menuBoardDE10Standard);
            _btnBoardMenu.DropDownItems.Add(_menuBoardDE10Nano);

            // Выбор курса
            _btnCourseMenu = new ToolStripDropDownButton("Выбор курса")
            {
                ForeColor = Color.White
            };

            _menuCourseDigitalLogic = new ToolStripMenuItem("Цифровая логика");
            _menuCourseDigitalLogic.Click += (s, e) => SelectCourse("Цифровая логика");

            _menuCourseArchitecture = new ToolStripMenuItem("Архитектура ЭВМ");
            _menuCourseArchitecture.Click += (s, e) => SelectCourse("Архитектура ЭВМ");

            _menuCourseEmbedded = new ToolStripMenuItem("Встраиваемые системы");
            _menuCourseEmbedded.Click += (s, e) => SelectCourse("Встраиваемые системы");

            _menuCourseFPGABasics = new ToolStripMenuItem("Основы FPGA");
            _menuCourseFPGABasics.Click += (s, e) => SelectCourse("Основы FPGA");

            _btnCourseMenu.DropDownItems.Add(_menuCourseDigitalLogic);
            _btnCourseMenu.DropDownItems.Add(_menuCourseArchitecture);
            _btnCourseMenu.DropDownItems.Add(_menuCourseEmbedded);
            _btnCourseMenu.DropDownItems.Add(_menuCourseFPGABasics);

            // Остальные кнопки
            _toolBtnEditOverlay = new ToolStripButton("Редактировать SW/KEY")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.White
            };
            _toolBtnEditOverlay.Click += BtnEditOverlay_Click;

            _toolBtnHistory = new ToolStripButton("История состояний")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.White
            };
            _toolBtnHistory.Click += BtnBack_Click;

            var lblCamera = new ToolStripLabel("Камера:")
            {
                ForeColor = Color.White
            };

            _cmbCamera = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = BgElevated,
                ForeColor = TextPrimary,
                Width = 220
            };

            var available = GetAvailableCameras();
            if (available.Count == 0)
            {
                _cmbCamera.Items.Add("Нет доступных камер");
                _cmbCamera.Enabled = false;
                _cmbCamera.SelectedIndex = 0;
            }
            else
            {
                foreach (var cam in available)
                    _cmbCamera.Items.Add(cam.Name);

                _cmbCamera.SelectedIndex = 0;
            }

            _cmbCamera.Tag = available;
            _cmbCamera.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbCamera.SelectedIndex < 0) return;

                var list = _cmbCamera.Tag as List<CameraInfo>;
                if (list == null || list.Count == 0) return;

                SwitchToCamera(list[_cmbCamera.SelectedIndex].Index);
            };

            var cameraHost = new ToolStripControlHost(_cmbCamera);

            _topToolStrip.Items.Add(_btnHideMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());

            _topToolStrip.Items.Add(_btnModeMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());

            _topToolStrip.Items.Add(_btnBoardMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());

            _topToolStrip.Items.Add(_btnCourseMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());

            _topToolStrip.Items.Add(_toolBtnEditOverlay);
            _topToolStrip.Items.Add(new ToolStripSeparator());

            _topToolStrip.Items.Add(_toolBtnHistory);
            _topToolStrip.Items.Add(new ToolStripSeparator());

            _topToolStrip.Items.Add(lblCamera);
            _topToolStrip.Items.Add(cameraHost);

            rootLayout.Controls.Add(_topToolStrip, 0, 0);

            // Основная область
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = this.BackColor,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(0)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            rootLayout.Controls.Add(mainLayout, 0, 1);

            // Видео панель
            var videoCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSurface,
                Padding = new Padding(2),
                Margin = new Padding(0, 0, 14, 0)
            };
            mainLayout.Controls.Add(videoCard, 0, 0);

            _videoPanel.Dock = DockStyle.Fill;
            _videoPanel.BackColor = Color.Black;
            videoCard.Controls.Add(_videoPanel);

            _pictureBox.Dock = DockStyle.Fill;
            _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            _pictureBox.BackColor = Color.Black;
            _videoPanel.Controls.Add(_pictureBox);

            CreateOverlayControls();

            // Правая панель
            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgSurface,
                Padding = new Padding(18, 24, 18, 24)
            };
            mainLayout.Controls.Add(controlPanel, 1, 0);

            int y = 0;

            controlPanel.Controls.Add(new Label
            {
                Text = "Управление соленоидами",
                Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 34;

            controlPanel.Controls.Add(new Label
            {
                Text = "SW0-SW9 + KEY0/KEY1",
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSecondary,
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 36;

            _lblStatus.Location = new Point(0, y);
            _lblStatus.Size = new Size(220, 68);
            _lblStatus.Font = new Font("Consolas", 10.5f, FontStyle.Bold);
            _lblStatus.ForeColor = TextPrimary;
            _lblStatus.BackColor = BgElevated;
            _lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            _lblStatus.Padding = new Padding(8);
            _lblStatus.Text = "Состояние: 0";
            controlPanel.Controls.Add(_lblStatus);

            _btnBack = new Button
            {
                Visible = false
            };
            controlPanel.Controls.Add(_btnBack);

            StyleToolStripItem(_btnHideMenu);
            StyleToolStripItem(_btnModeMenu);
            StyleToolStripItem(_btnBoardMenu);
            StyleToolStripItem(_btnCourseMenu);
            StyleToolStripItem(_toolBtnEditOverlay);
            StyleToolStripItem(_toolBtnHistory);

            StyleToolStripDropDown(_btnHideMenu);
            StyleToolStripDropDown(_btnModeMenu);
            StyleToolStripDropDown(_btnBoardMenu);
            StyleToolStripDropDown(_btnCourseMenu);

            UpdateHideMenuTexts();
            UpdateSelectionMenus();
        }

        private void StyleToolStripItem(ToolStripItem? item)
        {
            if (item == null) return;

            item.ForeColor = TextPrimary;
            item.BackColor = BgSurface;
            item.Margin = new Padding(2, 0, 2, 0);
        }

        private void StyleToolStripDropDown(ToolStripDropDownItem? item)
        {
            if (item == null) return;

            item.DropDown.BackColor = BgSurface;
            item.DropDown.ForeColor = TextPrimary;

            foreach (ToolStripItem dropItem in item.DropDownItems)
            {
                dropItem.BackColor = BgSurface;
                dropItem.ForeColor = TextPrimary;
            }
        }

        private void ToggleSwitchAndKeysVisibility()
        {
            bool willBeVisible = !_switches[0].Visible;

            foreach (var sw in _switches)
                sw.Visible = willBeVisible;

            if (_btnKey0 != null) _btnKey0.Visible = willBeVisible;
            if (_btnKey1 != null) _btnKey1.Visible = willBeVisible;

            UpdateHideMenuTexts();
        }

        private void ToggleOverlayVisibility()
        {
            _showOverlay = !_showOverlay;
            UpdateHideMenuTexts();
        }

        private void UpdateHideMenuTexts()
        {
            if (_menuToggleOverlay != null)
                _menuToggleOverlay.Text = _showOverlay ? "Скрыть оверлей" : "Показать оверлей";

            bool switchesVisible = _switches.Length > 0 && _switches[0] != null && _switches[0].Visible;

            if (_menuToggleSwitches != null)
                _menuToggleSwitches.Text = switchesVisible ? "Скрыть SW/KEY" : "Показать SW/KEY";
        }

        private void SelectMode(string mode)
        {
            _selectedMode = mode;
            UpdateSelectionMenus();
        }

        private void SelectBoard(string board)
        {
            _selectedBoard = board;
            UpdateSelectionMenus();
        }

        private void SelectCourse(string course)
        {
            _selectedCourse = course;
            UpdateSelectionMenus();
        }

        private void UpdateSelectionMenus()
        {
            if (_menuModeTraining != null) _menuModeTraining.Checked = _selectedMode == "Учебный";
            if (_menuModeDebug != null) _menuModeDebug.Checked = _selectedMode == "Отладка";
            if (_menuModeTest != null) _menuModeTest.Checked = _selectedMode == "Тест";

            if (_menuBoardDE10Lite != null) _menuBoardDE10Lite.Checked = _selectedBoard == "DE10-Lite";
            if (_menuBoardDE0CV != null) _menuBoardDE0CV.Checked = _selectedBoard == "DE0-CV";
            if (_menuBoardDE0Nano != null) _menuBoardDE0Nano.Checked = _selectedBoard == "DE0-Nano";
            if (_menuBoardDE1SoC != null) _menuBoardDE1SoC.Checked = _selectedBoard == "DE1-SoC";
            if (_menuBoardDE10Standard != null) _menuBoardDE10Standard.Checked = _selectedBoard == "DE10-Standard";
            if (_menuBoardDE10Nano != null) _menuBoardDE10Nano.Checked = _selectedBoard == "DE10-Nano";

            if (_menuCourseDigitalLogic != null) _menuCourseDigitalLogic.Checked = _selectedCourse == "Цифровая логика";
            if (_menuCourseArchitecture != null) _menuCourseArchitecture.Checked = _selectedCourse == "Архитектура ЭВМ";
            if (_menuCourseEmbedded != null) _menuCourseEmbedded.Checked = _selectedCourse == "Встраиваемые системы";
            if (_menuCourseFPGABasics != null) _menuCourseFPGABasics.Checked = _selectedCourse == "Основы FPGA";

            if (_btnModeMenu != null)
                _btnModeMenu.Text = $"Режим: {_selectedMode}";

            if (_btnBoardMenu != null)
                _btnBoardMenu.Text = $"Плата: {_selectedBoard}";

            if (_btnCourseMenu != null)
                _btnCourseMenu.Text = $"Курс: {_selectedCourse}";
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            if (_logWindow == null || _logWindow.IsDisposed)
            {
                _logWindow = new ReturnValuesWindow(_serialPort);
                _logWindow.FormClosed += (s, args) => _logWindow = null;
            }

            _logWindow.Show();
            _logWindow.BringToFront();
        }

        private void BtnEditOverlay_Click(object? sender, EventArgs e)
        {
            _editMode = !_editMode;

            if (_toolBtnEditOverlay != null)
                _toolBtnEditOverlay.Text = _editMode ? "Завершить редактирование" : "Редактировать SW/KEY";

            SetEditMode(_switches, _editMode);

            if (_btnKey0 != null && _btnKey1 != null)
                SetEditMode(new[] { _btnKey0, _btnKey1 }, _editMode);
        }

        private void SetEditMode(IEnumerable<Control> controls, bool enabled)
        {
            foreach (var ctrl in controls)
            {
                ctrl.Cursor = enabled ? Cursors.SizeAll : Cursors.Default;
                ctrl.MouseDown -= Control_MouseDown;
                ctrl.MouseMove -= Control_MouseMove;
                ctrl.MouseUp -= Control_MouseUp;

                if (ctrl is DipSwitch ds)
                    ds.DisableInternalHandling = enabled;

                if (enabled)
                {
                    ctrl.MouseDown += Control_MouseDown;
                    ctrl.MouseMove += Control_MouseMove;
                    ctrl.MouseUp += Control_MouseUp;
                }
            }
        }

        private void Control_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_editMode) return;

            if (sender is Control ctrl)
            {
                const int gripSize = 12;
                _mouseDownGlobal = ctrl.PointToScreen(e.Location);
                _controlStartGlobal = ctrl.PointToScreen(new Point(0, 0));

                if (e.X >= ctrl.Width - gripSize && e.Y >= ctrl.Height - gripSize)
                {
                    _resizing = true;
                    _draggedControl = ctrl;
                    _startSize = ctrl.Size;
                }
                else
                {
                    _resizing = false;
                    _draggedControl = ctrl;
                }

                ctrl.Capture = true;
            }
        }

        private void Control_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_draggedControl == null || !_editMode) return;

            Point currentMouseGlobal = _draggedControl.PointToScreen(e.Location);

            if (_resizing)
            {
                int deltaX = currentMouseGlobal.X - _mouseDownGlobal.X;
                int deltaY = currentMouseGlobal.Y - _mouseDownGlobal.Y;
                int newWidth = Math.Max(40, _startSize.Width + deltaX);
                int newHeight = Math.Max(30, _startSize.Height + deltaY);

                if (_draggedControl.Left + newWidth > _videoPanel.ClientSize.Width)
                    newWidth = _videoPanel.ClientSize.Width - _draggedControl.Left;

                if (_draggedControl.Top + newHeight > _videoPanel.ClientSize.Height)
                    newHeight = _videoPanel.ClientSize.Height - _draggedControl.Top;

                _draggedControl.Size = new Size(newWidth, newHeight);
            }
            else
            {
                int deltaX = currentMouseGlobal.X - _mouseDownGlobal.X;
                int deltaY = currentMouseGlobal.Y - _mouseDownGlobal.Y;
                Point newLocation = new Point(_controlStartGlobal.X + deltaX, _controlStartGlobal.Y + deltaY);
                newLocation = _videoPanel.PointToClient(newLocation);

                newLocation.X = Math.Max(0, Math.Min(_videoPanel.ClientSize.Width - _draggedControl.Width, newLocation.X));
                newLocation.Y = Math.Max(0, Math.Min(_videoPanel.ClientSize.Height - _draggedControl.Height, newLocation.Y));

                _draggedControl.Location = newLocation;
            }
        }

        private void Control_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_draggedControl != null)
            {
                _draggedControl.Capture = false;
                _draggedControl = null;
                _resizing = false;
            }
        }

        private void CreateOverlayControls()
        {
            int baseY = 520;
            int spacingX = 58;

            for (int i = 0; i < 10; i++)
            {
                _switches[i] = new DipSwitch
                {
                    Location = new Point(180 + i * spacingX, baseY),
                    Size = new Size(54, 90)
                };

                _switches[i].StateChanged += (_, _) => UpdateOutputState();
                _videoPanel.Controls.Add(_switches[i]);
                _switches[i].BringToFront();
            }

            _btnKey0 = new RoundButton
            {
                Text = "KEY0",
                Location = new Point(790, baseY - 150),
                BackColor = Color.FromArgb(0, 140, 0),
                BorderColor = Color.LimeGreen
            };
            _btnKey0.Click += BtnKey0_Click;
            _videoPanel.Controls.Add(_btnKey0);
            _btnKey0.BringToFront();

            _btnKey1 = new RoundButton
            {
                Text = "KEY1",
                Location = new Point(790, baseY - 30),
                BackColor = Color.FromArgb(140, 0, 0),
                BorderColor = Color.IndianRed
            };
            _btnKey1.Click += BtnKey1_Click;
            _videoPanel.Controls.Add(_btnKey1);
            _btnKey1.BringToFront();
        }

        private void BtnKey0_Click(object? sender, EventArgs e)
        {
            currentOutputState ^= (1L << 20);
            UpdateOutputState();
            UpdateKeyAppearance();
        }

        private void BtnKey1_Click(object? sender, EventArgs e)
        {
            currentOutputState ^= (1L << 21);
            UpdateOutputState();
            UpdateKeyAppearance();
        }

        private void UpdateKeyAppearance()
        {
            if (_btnKey0 != null)
                _btnKey0.BackColor = (currentOutputState & (1L << 20)) != 0
                    ? Color.LimeGreen
                    : Color.FromArgb(0, 140, 0);

            if (_btnKey1 != null)
                _btnKey1.BackColor = (currentOutputState & (1L << 21)) != 0
                    ? Color.Red
                    : Color.FromArgb(140, 0, 0);
        }

        private void UpdateOutputState()
        {
            long newState = 0;

            for (int i = 0; i < 10; i++)
            {
                if (_switches[i].IsOn)
                {
                    newState |= (1L << (i * 2));
                    newState |= (1L << (i * 2 + 1));
                }
            }

            newState |= (currentOutputState & (1L << 20));
            newState |= (currentOutputState & (1L << 21));

            string command = newState.ToString() + "\n";
            SendCommand(command);

            if (_previousState != -1)
            {
                for (int i = 0; i < 22; i++)
                {
                    long mask = 1L << i;
                    bool wasOn = (_previousState & mask) != 0;
                    bool nowOn = (newState & mask) != 0;

                    if (wasOn != nowOn)
                    {
                        string msg = i < 20
                            ? $"Sol{i / 2} Dir{(i % 2 == 0 ? "1" : "2")} → {(nowOn ? "ИМПУЛЬС 0.5с" : "0")}"
                            : $"KEY{i - 20} → {(nowOn ? "1" : "0")}";

                        if (_logWindow != null && !_logWindow.IsDisposed)
                            _logWindow.AddLog(msg);
                        else
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
                    }
                }
            }

            _previousState = newState;
            currentOutputState = newState;

            this.InvokeIfRequired(() =>
            {
                _lblStatus.Text = $"Состояние: {newState} (0x{newState:X6})";
            });
        }

        private List<CameraInfo> GetAvailableCameras(int maxIndex = 15)
        {
            var cameras = new List<CameraInfo>();

            for (int i = 0; i <= maxIndex; i++)
            {
                try
                {
                    using var testCapture = new VideoCapture(i, VideoCapture.API.DShow);
                    if (testCapture.IsOpened)
                        cameras.Add(new CameraInfo(i, $"Камера {i}"));
                }
                catch
                {
                }
            }

            return cameras;
        }

        private void TryOpenFirstAvailableCamera()
        {
            var cameras = GetAvailableCameras();
            if (cameras.Count > 0)
                SwitchToCamera(cameras[0].Index);
        }

        private void SetupCamera(int index)
        {
            try
            {
                _frameTimer?.Stop();
                _capture?.Dispose();
                _capture = null;

                _capture = new VideoCapture(index, VideoCapture.API.DShow);
                if (!_capture.IsOpened)
                {
                    MessageBox.Show($"Не удалось открыть камеру {index}", "Ошибка");
                    return;
                }

                _capture.Set(Emgu.CV.CvEnum.CapProp.FrameWidth, 640);
                _capture.Set(Emgu.CV.CvEnum.CapProp.FrameHeight, 480);

                _frameTimer = new System.Windows.Forms.Timer { Interval = 40 };
                _frameTimer.Tick += FrameTimer_Tick;
                _frameTimer.Start();

                _currentCameraIndex = index;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка камеры {index}:\n{ex.Message}", "Ошибка");
            }
        }

        private void SwitchToCamera(int index)
        {
            if (index == _currentCameraIndex) return;
            SetupCamera(index);
        }

        private void FrameTimer_Tick(object? sender, EventArgs e)
        {
            if (_capture == null || !_capture.IsOpened) return;

            try
            {
                using var frame = _capture.QueryFrame();
                if (frame is null || frame.IsEmpty) return;

                var bmp = frame.ToBitmap();

                if (_showOverlay)
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                        // === НИЖНИЙ ПРЯМОУГОЛЬНИК ===
                        int rectWidth = 405;
                        int rectHeight = 90;
                        int x = (bmp.Width - rectWidth) / 2 + 13;
                        int y = (bmp.Height - rectHeight) / 2 + 40;

                        using (var brush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                        {
                            g.FillRectangle(brush, x, y, rectWidth, rectHeight);
                        }

                        using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 3))
                        {
                            g.DrawRectangle(pen, x, y, rectWidth, rectHeight);
                        }

                        // === ВЕРХНИЙ ПРЯМОУГОЛЬНИК ===
                        int topRectWidth = 405;
                        int topRectHeight = 60;

                        int topX = x;
                        int topY = y - topRectHeight - 10; // выше основного

                        using (var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                        {
                            g.FillRectangle(brush, topX, topY, topRectWidth, topRectHeight);
                        }

                        using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 2))
                        {
                            g.DrawRectangle(pen, topX, topY, topRectWidth, topRectHeight);
                        }

                        // === СТАРАЯ ЛОГИКА LED ===
                        using (var ledFont = new Font("Consolas", 7, FontStyle.Bold))
                        {
                            int startX = x - 8;
                            int startY = y + 20;
                            int spacingX = 40;
                            int spacingY = 35;
                            int cols = 10;

                            for (int i = 0; i < 10; i++)
                            {
                                int col = i % cols;
                                int row = i / cols;
                                int ledX = startX + (col * spacingX);
                                int ledY = startY + (row * spacingY);

                                bool isOn = _switches[i].IsOn;
                                Color ledColor = isOn ? Color.LimeGreen : Color.Gray;

                                using (var ledBrush = new SolidBrush(ledColor))
                                {
                                    g.FillEllipse(ledBrush, ledX + 23, ledY - 15, 12, 12);
                                }

                                using (var textBrush = new SolidBrush(Color.White))
                                {
                                    g.DrawString($"LED{i}", ledFont, textBrush, ledX + 18, ledY - 2);
                                }
                            }
                        }
                    }
                }

                var oldImage = _pictureBox.Image;
                _pictureBox.Image = bmp;
                _lastBitmap?.Dispose();
                _lastBitmap = bmp;
                oldImage?.Dispose();
            }
            catch
            {
            }
        }

        private void InitializeSerialPort()
        {
            string[] portsToCheck = { "COM9", "COM10", "COM11", "COM12", "COM8", "COM7", "COM6", "COM5" };

            foreach (string portName in portsToCheck)
            {
                try
                {
                    var testPort = new SerialPort(portName, 115200)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500
                    };

                    testPort.Open();
                    _serialPort = testPort;

                    this.InvokeIfRequired(() =>
                    {
                        _lblStatus.Text = $"Подключено к {portName}";
                        _lblStatus.BackColor = Color.FromArgb(50, 90, 50);
                    });
                    return;
                }
                catch
                {
                }
            }

            this.InvokeIfRequired(() =>
            {
                _lblStatus.Text = "НЕТ ПОДКЛЮЧЕНИЯ!";
                _lblStatus.BackColor = Color.FromArgb(90, 40, 40);
                MessageBox.Show(
                    "Не удалось подключиться к STM32.\nУправление будет работать без связи с платой.",
                    "COM-порт не найден",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            });
        }

        private void SendCommand(string command)
        {
            if (_serialPort?.IsOpen != true) return;

            try
            {
                _serialPort.WriteLine(command);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки: {ex.Message}");
            }
        }

        private void StartReadingThread()
        {
            _readThread = new Thread(ReadSerialLoop)
            {
                IsBackground = true,
                Name = "SerialReader"
            };
            _readThread.Start();
        }

        private void ReadSerialLoop()
        {
            while (_running)
            {
                try
                {
                    if (_serialPort?.IsOpen == true && _serialPort.BytesToRead > 0)
                    {
                        string? line = _serialPort.ReadLine()?.Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            this.InvokeIfRequired(() =>
                            {
                                if (!line.StartsWith("STATE"))
                                    _lblStatus.Text = $"Ответ: {line}";
                            });
                        }
                    }
                }
                catch
                {
                }

                Thread.Sleep(60);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _running = false;
            _readThread?.Join(1200);

            _frameTimer?.Stop();
            _capture?.Dispose();

            if (_serialPort?.IsOpen == true)
            {
                try { _serialPort.Close(); } catch { }
                _serialPort.Dispose();
            }

            _pictureBox.Image?.Dispose();
            _lastBitmap?.Dispose();

            base.OnFormClosing(e);
        }
    }

    internal sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _background;
        private readonly Color _hover;
        private readonly Color _border;
        private readonly Color _accent;

        public ModernToolStripRenderer(Color background, Color hover, Color border, Color accent)
        {
            _background = background;
            _hover = hover;
            _border = border;
            _accent = accent;
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(_background);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(_background);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2);
            using var pen = new Pen(Color.FromArgb(72, _border));
            e.Graphics.DrawLine(pen, e.Item.ContentRectangle.Left + 2, y, e.Item.ContentRectangle.Right - 2, y);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(Point.Empty, e.Item.Size);

            if (e.Item.Selected)
            {
                using var brush = new SolidBrush(_hover);
                e.Graphics.FillRectangle(brush, rect);
                using var side = new SolidBrush(_accent);
                e.Graphics.FillRectangle(side, 0, 0, 3, rect.Height);
                return;
            }

            using var idle = new SolidBrush(_background);
            e.Graphics.FillRectangle(idle, rect);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(Point.Empty, e.Item.Size);
            using var brush = new SolidBrush(e.Item.Selected ? _hover : _background);
            e.Graphics.FillRectangle(brush, rect);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            OnRenderButtonBackground(e);
        }
    }

    public static class ControlExtensions
    {
        public static void InvokeIfRequired(this Control control, Action action)
        {
            if (control.InvokeRequired)
                control.Invoke(action);
            else
                action();
        }
    }
}
