
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Emgu.CV;

namespace MyEmguProject
{
    public partial class BoardWindow : Form
    {
        private const int SolenoidCount = 10;
        private const int TotalChannels = 22;
        private const int DefaultPulseVisualMs = 500;

        private readonly DipSwitch[] _switches = new DipSwitch[SolenoidCount];
        private readonly Label[] _switchLabels = new Label[SolenoidCount];
        private readonly Panel _videoPanel = new();
        private readonly PictureBox _pictureBox = new();
        private readonly Label _lblStatus = new();
        private readonly System.Windows.Forms.Timer _visualResetTimer = new();
        private readonly Label _lblSwitchNumberView = new();

        private readonly DateTime[] _channelActiveUntil = new DateTime[TotalChannels];
        private readonly bool[] _keyPressedStates = new bool[2];

        private RoundButton? _btnKey0;
        private RoundButton? _btnKey1;

        private VideoCapture? _capture;
        private System.Windows.Forms.Timer? _frameTimer;
        private Bitmap? _lastBitmap;
        private int _currentCameraIndex = -1;

        private SerialPort? _serialPort;
        private Thread? _readThread;
        private volatile bool _running = true;
        private int _pulseVisualMs = DefaultPulseVisualMs;

        private bool _showOverlay = true;
        private bool _showCustomContours = true;
        private bool _rotateCamera180 = true;
        private bool _editMode = false;
        private Control? _draggedControl;
        private bool _resizing = false;
        private Size _startSize;
        private Point _mouseDownGlobal;
        private Point _controlStartGlobal;

        private ReturnValuesWindow? _logWindow;
        private SwKeyLayoutEditorWindow? _swKeyEditorWindow;

        private ToolStrip? _topToolStrip;
        private ToolStripDropDownButton? _btnHideMenu;
        private ToolStripMenuItem? _menuToggleOverlay;
        private ToolStripMenuItem? _menuToggleContours;
        private ToolStripMenuItem? _menuToggleSwitches;
        private ToolStripMenuItem? _menuToggleTheme;
        private ToolStripMenuItem? _menuRotateCamera180;
        private ToolStripMenuItem? _menuSwitchesVisible;
        private ToolStripMenuItem? _menuSwitchesHidden;
        private ToolStripMenuItem? _menuSwitchesHoverReveal;
        private ToolStripMenuItem? _menuOverlayTransparency;
        private ToolStripMenuItem? _menuEditOverlayRects;
        private ToolStripMenuItem? _menuAddOverlayRect;
        private ToolStripMenuItem? _menuRemoveOverlayRect;
        private ToolStripMenuItem? _menuNumberPresentation;
        private ToolStripDropDownButton? _btnEditOverlayMenu;
        private ToolStripMenuItem? _menuEditOverlayEnabled;
        private ToolStripButton? _toolBtnHistory;
        private ComboBox? _cmbCamera;
        private ComboBox? _cmbComPort;
        private Label? _lblModeValue;
        private Label? _lblBoardValue;
        private Label? _lblCourseValue;
        private readonly Label _lblNumbersSummary = new();
        private readonly Label _lblHoverInfo = new();

        private ToolStripDropDownButton? _btnModeMenu;
        private ToolStripDropDownButton? _btnBoardMenu;
        private ToolStripDropDownButton? _btnCourseMenu;
        private ToolStripDropDownButton? _btnToolsMenu;
        private ToolStripMenuItem? _menuPulseDuration;
        private ToolStripMenuItem? _menuNumberBinary;
        private ToolStripMenuItem? _menuNumberOctal;
        private ToolStripMenuItem? _menuNumberDecimal;
        private ToolStripMenuItem? _menuNumberHex;

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
        private TableLayoutPanel? _rootLayout;
        private ThemedSurfacePanel? _toolbarHost;
        private ThemedSurfacePanel? _selectionHost;
        private ThemedSurfacePanel? _controlHost;
        private Panel? _videoHost;
        private TableLayoutPanel? _mainLayout;
        private Label? _lblControlTitle;
        private Label? _lblControlSubtitle;
        private Label? _lblSessionPill;
        private Label? _lblToolsPill;

        private string _selectedMode = "Учебный";
        private string _selectedBoard = "DE10-Lite";
        private string _selectedCourse = "Без курса";

        private bool _isUpdatingComPortList;
        private bool _editOverlayRectsMode;
        private bool _overlayLayoutInitialized;
        private bool _overlayRectDragging;
        private bool _overlayRectResizing;
        private Rectangle _mainOverlayRect;
        private Rectangle _headerOverlayRect;
        private readonly List<CustomOverlayRect> _customOverlayRects = new();
        private Rectangle _overlayRectStartBounds;
        private Point _overlayMouseDownImage;
        private OverlayRectSelection _activeOverlayRect = OverlayRectSelection.None;
        private int _activeCustomOverlayRectIndex = -1;
        private int _previewCustomOverlayRectIndex = -1;
        private int _overlayTransparencyAlpha = 160;
        private NumberPresentation _selectedNumberPresentation = NumberPresentation.Binary;
        private SwKeyVisibilityMode _swKeyVisibilityMode = SwKeyVisibilityMode.Visible;
        private string? _hoveredSwKeyId;

        private static Color BgPrimary => AppTheme.Current.BgPrimary;
        private static Color BgCanvas => AppTheme.Current.BgCanvas;
        private static Color BgPanel => AppTheme.Current.BgPanel;
        private static Color SurfacePrimary => AppTheme.Current.SurfacePrimary;
        private static Color SurfaceSecondary => AppTheme.Current.SurfaceSecondary;
        private static Color SurfaceTertiary => AppTheme.Current.SurfaceTertiary;
        private static Color BorderSoft => AppTheme.Current.BorderSoft;
        private static Color BorderStrong => AppTheme.Current.BorderStrong;
        private static Color AccentCyan => AppTheme.Current.AccentCyan;
        private static Color AccentBlue => AppTheme.Current.AccentBlue;
        private static Color AccentMuted => AppTheme.Current.AccentMuted;
        private static Color AccentPill => AppTheme.Current.AccentPill;
        private static Color TextPrimary => AppTheme.Current.TextPrimary;
        private static Color TextSecondary => AppTheme.Current.TextSecondary;
        private static Color KeyIdleColor => AppTheme.Current.KeyIdleColor;
        private static Color KeyIdleBorderColor => AppTheme.Current.KeyIdleBorderColor;
        private static Color KeyActiveColor => AppTheme.Current.KeyActiveColor;
        private static Color KeyActiveBorderColor => AppTheme.Current.KeyActiveBorderColor;
        private static readonly string[] PreferredComPorts = { "COM9", "COM10", "COM11", "COM12", "COM8", "COM7", "COM6", "COM5" };
        private static readonly string SwKeyLayoutFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "swkey-layout.json");

        private record CameraInfo(int Index, string Name);

        private enum NumberPresentation
        {
            Binary,
            Octal,
            Decimal,
            Hexadecimal
        }

        private enum OverlayRectSelection
        {
            None,
            Main,
            Header,
            Custom
        }

        private sealed class CustomOverlayRect
        {
            public string Name { get; set; } = string.Empty;
            public Rectangle Bounds { get; set; }
            public Color Color { get; set; } = Color.DeepSkyBlue;
        }

        private enum SwKeyVisibilityMode
        {
            Visible,
            Hidden,
            HoverReveal
        }

        public BoardWindow()
        {
            InitializeComponent();
            SetupUI();
            Shown += (_, _) => BeginInvoke((Action)RefreshSwitchAnnotations);
            InitializeSerialPort();
            StartReadingThread();
            TryOpenFirstAvailableCamera();

            _visualResetTimer.Interval = 50;
            _visualResetTimer.Tick += VisualResetTimer_Tick;
            _visualResetTimer.Start();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            Size = new Size(1280, 820);
            MinimumSize = Size;
            MaximumSize = Size;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Управление платой STM32 (22 канала)";
            BackColor = BgPrimary;
            Font = new Font("Segoe UI", 9.75f);
            ResumeLayout(false);
        }

        private void SetupUI()
        {
            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = BackColor,
                Margin = new Padding(0),
                Padding = new Padding(18, 14, 18, 18)
            };
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(_rootLayout);

            _topToolStrip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.Transparent,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Padding = new Padding(12, 10, 12, 10),
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new TopMenuRenderer()
            };

            _btnHideMenu = new ToolStripDropDownButton("Вид") { ForeColor = TextPrimary };
            _menuToggleOverlay = new ToolStripMenuItem("Оверлей") { Checked = true };
            _menuToggleOverlay.Click += (s, e) => ToggleOverlayVisibility();
            _menuToggleContours = new ToolStripMenuItem("Контуры") { Checked = true };
            _menuToggleContours.Click += (s, e) => ToggleContoursVisibility();
            _menuToggleTheme = new ToolStripMenuItem("Тёмная тема") { Checked = AppTheme.IsDark };
            _menuToggleTheme.Click += (s, e) => ToggleTheme();
            _menuRotateCamera180 = new ToolStripMenuItem("Поворот камеры 180°") { Checked = _rotateCamera180 };
            _menuRotateCamera180.Click += (s, e) => ToggleCameraRotation();
            _menuToggleSwitches = new ToolStripMenuItem("SW/KEY");
            _menuSwitchesVisible = new ToolStripMenuItem("Видно");
            _menuSwitchesVisible.Click += (s, e) => SetSwKeyVisibilityMode(SwKeyVisibilityMode.Visible);
            _menuSwitchesHidden = new ToolStripMenuItem("Скрыто");
            _menuSwitchesHidden.Click += (s, e) => SetSwKeyVisibilityMode(SwKeyVisibilityMode.Hidden);
            _menuSwitchesHoverReveal = new ToolStripMenuItem("Подсветка при наведении");
            _menuSwitchesHoverReveal.Click += (s, e) => SetSwKeyVisibilityMode(SwKeyVisibilityMode.HoverReveal);
            _menuToggleSwitches.DropDownItems.AddRange(new ToolStripItem[]
            {
                _menuSwitchesVisible,
                _menuSwitchesHidden,
                _menuSwitchesHoverReveal
            });
            _menuOverlayTransparency = new ToolStripMenuItem("Прозрачность прямоугольников...");
            _menuOverlayTransparency.Click += MenuOverlayTransparency_Click;
            _menuEditOverlayRects = new ToolStripMenuItem("Редактировать прямоугольники");
            _menuEditOverlayRects.Click += MenuEditOverlayRects_Click;
            _menuAddOverlayRect = new ToolStripMenuItem("Добавить контур...");
            _menuAddOverlayRect.Click += MenuAddOverlayRect_Click;
            _menuRemoveOverlayRect = new ToolStripMenuItem("Удалить контур...");
            _menuRemoveOverlayRect.Click += MenuRemoveOverlayRect_Click;
            _menuNumberPresentation = new ToolStripMenuItem("Представление чисел");
            _menuNumberBinary = new ToolStripMenuItem("Двоичная");
            _menuNumberBinary.Click += (s, e) => SelectNumberPresentation(NumberPresentation.Binary);
            _menuNumberOctal = new ToolStripMenuItem("Восьмеричная");
            _menuNumberOctal.Click += (s, e) => SelectNumberPresentation(NumberPresentation.Octal);
            _menuNumberDecimal = new ToolStripMenuItem("Десятичная");
            _menuNumberDecimal.Click += (s, e) => SelectNumberPresentation(NumberPresentation.Decimal);
            _menuNumberHex = new ToolStripMenuItem("Шестнадцатеричная");
            _menuNumberHex.Click += (s, e) => SelectNumberPresentation(NumberPresentation.Hexadecimal);
            _btnHideMenu.DropDownItems.Add(_menuToggleOverlay);
            _btnHideMenu.DropDownItems.Add(_menuToggleContours);
            _btnHideMenu.DropDownItems.Add(_menuToggleTheme);
            _btnHideMenu.DropDownItems.Add(_menuRotateCamera180);
            _btnHideMenu.DropDownItems.Add(_menuToggleSwitches);
            _btnHideMenu.DropDownItems.Add(new ToolStripSeparator());
            _btnHideMenu.DropDownItems.Add(_menuOverlayTransparency);
            _btnHideMenu.DropDownItems.Add(_menuEditOverlayRects);
            _btnHideMenu.DropDownItems.Add(_menuAddOverlayRect);
            _btnHideMenu.DropDownItems.Add(_menuRemoveOverlayRect);
            _menuNumberPresentation.DropDownItems.AddRange(new ToolStripItem[]
            {
                _menuNumberBinary, _menuNumberOctal, _menuNumberDecimal, _menuNumberHex
            });
            _btnHideMenu.DropDownItems.Add(_menuNumberPresentation);

            _btnModeMenu = new ToolStripDropDownButton("Режим") { ForeColor = TextPrimary };
            _menuModeTraining = new ToolStripMenuItem("Учебный");
            _menuModeTraining.Click += (s, e) => SelectMode("Учебный");
            _menuModeDebug = new ToolStripMenuItem("Отладка");
            _menuModeDebug.Click += (s, e) => SelectMode("Отладка");
            _menuModeTest = new ToolStripMenuItem("Тест");
            _menuModeTest.Click += (s, e) => SelectMode("Тест");
            _btnModeMenu.DropDownItems.AddRange(new ToolStripItem[] { _menuModeTraining, _menuModeDebug, _menuModeTest });

            _btnBoardMenu = new ToolStripDropDownButton("Плата") { ForeColor = TextPrimary };
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
            _btnBoardMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _menuBoardDE10Lite, _menuBoardDE0CV, _menuBoardDE0Nano, _menuBoardDE1SoC, _menuBoardDE10Standard, _menuBoardDE10Nano
            });

            _btnCourseMenu = new ToolStripDropDownButton("Курс") { ForeColor = TextPrimary };
            _menuCourseDigitalLogic = new ToolStripMenuItem("Цифровая логика");
            _menuCourseDigitalLogic.Click += (s, e) => SelectCourse("Цифровая логика");
            _menuCourseArchitecture = new ToolStripMenuItem("Архитектура ЭВМ");
            _menuCourseArchitecture.Click += (s, e) => SelectCourse("Архитектура ЭВМ");
            _menuCourseEmbedded = new ToolStripMenuItem("Встраиваемые системы");
            _menuCourseEmbedded.Click += (s, e) => SelectCourse("Встраиваемые системы");
            _menuCourseFPGABasics = new ToolStripMenuItem("Основы FPGA");
            _menuCourseFPGABasics.Click += (s, e) => SelectCourse("Основы FPGA");
            _btnCourseMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _menuCourseDigitalLogic, _menuCourseArchitecture, _menuCourseEmbedded, _menuCourseFPGABasics
            });

            _btnToolsMenu = new ToolStripDropDownButton("Инструменты") { ForeColor = TextPrimary };
            _menuPulseDuration = new ToolStripMenuItem("Время сигнала...");
            _menuPulseDuration.Click += MenuPulseDuration_Click;
            _btnToolsMenu.DropDownItems.Add(_menuPulseDuration);

            _btnEditOverlayMenu = new ToolStripDropDownButton("Редактировать SW/KEY") { ForeColor = TextPrimary };
            _menuEditOverlayEnabled = new ToolStripMenuItem("Активно");
            _menuEditOverlayEnabled.Click += BtnEditOverlay_Click;
            _btnEditOverlayMenu.DropDownItems.Add(_menuEditOverlayEnabled);

            _toolBtnHistory = new ToolStripButton("Журнал") { DisplayStyle = ToolStripItemDisplayStyle.Text, ForeColor = TextPrimary };
            _toolBtnHistory.Click += BtnBack_Click;

            var lblCamera = new ToolStripLabel("Камера:") { ForeColor = TextPrimary };

            var lblComPort = new ToolStripLabel("COM:") { ForeColor = TextPrimary };

            _cmbCamera = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                BackColor = AppTheme.ComboBoxBackground,
                ForeColor = AppTheme.ComboBoxText,
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
                if (_cmbCamera.Tag is not List<CameraInfo> list || list.Count == 0) return;
                SwitchToCamera(list[_cmbCamera.SelectedIndex].Index);
            };

            var cameraHost = new ToolStripControlHost(_cmbCamera);

            _cmbComPort = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                BackColor = AppTheme.ComboBoxBackground,
                ForeColor = AppTheme.ComboBoxText,
                Width = 130
            };
            _cmbComPort.SelectedIndexChanged += CmbComPort_SelectedIndexChanged;

            var comPortHost = new ToolStripControlHost(_cmbComPort);

            _topToolStrip.Items.Add(_btnHideMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(_btnModeMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(_btnBoardMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(_btnCourseMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(_btnToolsMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(_btnEditOverlayMenu);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(_toolBtnHistory);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(lblCamera);
            _topToolStrip.Items.Add(cameraHost);
            _topToolStrip.Items.Add(new ToolStripSeparator());
            _topToolStrip.Items.Add(lblComPort);
            _topToolStrip.Items.Add(comPortHost);

            _toolbarHost = new ThemedSurfacePanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12, 10, 12, 10),
                SurfaceColor = SurfacePrimary,
                BorderColor = BorderSoft,
                GlowColor = AppTheme.IsDark ? Color.FromArgb(26, AccentBlue) : Color.FromArgb(14, 124, 191, 181),
                CornerRadius = 28
            };
            _toolbarHost.Controls.Add(_topToolStrip);
            _rootLayout.Controls.Add(_toolbarHost, 0, 0);

            _selectionHost = new ThemedSurfacePanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 14),
                Padding = new Padding(14, 10, 14, 14),
                SurfaceColor = SurfaceSecondary,
                BorderColor = BorderSoft,
                GlowColor = AppTheme.IsDark ? Color.FromArgb(20, AccentCyan) : Color.FromArgb(12, 126, 191, 182),
                CornerRadius = 28
            };

            _lblSessionPill = CreateSectionPill("СОСТОЯНИЕ СЕССИИ");
            _lblSessionPill.Location = new Point(18, 18);
            _selectionHost.Controls.Add(_lblSessionPill);

            _lblModeValue = CreateSelectionCard(_selectionHost, "Режим");
            _lblModeValue.Parent!.Location = new Point(200, 16);

            _lblBoardValue = CreateSelectionCard(_selectionHost, "Плата");
            _lblBoardValue.Parent!.Location = new Point(452, 16);

            _lblCourseValue = CreateSelectionCard(_selectionHost, "Курс");
            _lblCourseValue.Parent!.Location = new Point(704, 16);
            _rootLayout.Controls.Add(_selectionHost, 0, 1);

            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = BackColor,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74f));
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
            _rootLayout.Controls.Add(_mainLayout, 0, 2);

            _videoHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(0),
                BackColor = BgCanvas
            };
            _mainLayout.Controls.Add(_videoHost, 0, 0);

            _videoPanel.Dock = DockStyle.Fill;
            _videoPanel.BackColor = Color.FromArgb(226, 242, 240);
            _videoPanel.Margin = new Padding(0);
            _videoPanel.Padding = new Padding(0);
            _videoHost.Controls.Add(_videoPanel);

            _pictureBox.Dock = DockStyle.Fill;
            _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            _pictureBox.BackColor = Color.FromArgb(231, 245, 243);
            _pictureBox.Paint += PictureBox_Paint;
            _pictureBox.MouseDown += PictureBox_MouseDown;
            _pictureBox.MouseMove += PictureBox_MouseMove;
            _pictureBox.MouseUp += PictureBox_MouseUp;
            _pictureBox.MouseLeave += PictureBox_MouseLeave;
            _videoPanel.Controls.Add(_pictureBox);

            CreateOverlayControls();

            _controlHost = new ThemedSurfacePanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(16, 56, 16, 18),
                SurfaceColor = SurfacePrimary,
                BorderColor = BorderSoft,
                GlowColor = AppTheme.IsDark ? Color.FromArgb(18, AccentCyan) : Color.FromArgb(12, 126, 191, 182),
                CornerRadius = 28
            };
            _mainLayout.Controls.Add(_controlHost, 1, 0);

            _lblToolsPill = CreateSectionPill("ИНСТРУМЕНТЫ И СТАТУС");
            _lblToolsPill.Location = new Point(16, 14);
            _controlHost.Controls.Add(_lblToolsPill);
            _lblToolsPill.BringToFront();

            int y = 56;
            _lblControlTitle = new Label
            {
                Text = "Лабораторный пульт",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(16, y)
            };
            _controlHost.Controls.Add(_lblControlTitle);
            y += 38;

            _lblControlSubtitle = new Label
            {
                Text = "Управление переключателями, клавишами и визуальными контурами в едином окне.",
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TextSecondary,
                Size = new Size(360, 44),
                Location = new Point(16, y)
            };
            _controlHost.Controls.Add(_lblControlSubtitle);
            y += 64;

            CreateInfoBlock(_controlHost, _lblStatus, "КАНАЛЫ И СВЯЗЬ", ref y, 108);
            _lblStatus.Font = new Font("Consolas", 10f, FontStyle.Bold);
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Text = "Подключение...";

            CreateInfoBlock(_controlHost, _lblNumbersSummary, "ТЕКУЩЕЕ ЗНАЧЕНИЕ", ref y, 112);
            _lblNumbersSummary.Font = new Font("Consolas", 9f, FontStyle.Bold);

            CreateInfoBlock(_controlHost, _lblHoverInfo, "ПОД КУРСОРОМ", ref y, 96);
            _lblHoverInfo.Font = new Font("Consolas", 9f, FontStyle.Bold);
            _lblHoverInfo.Text = "Элемент:\n-";

            UpdateHideMenuTexts();
            UpdateSelectionMenus();
            UpdateNumberDisplays();
            ApplyTheme();
            UpdateStatusText("Готово");
        }

        private void CreateOverlayControls()
        {
            int baseY = 474;
            int spacingX = 29;
            int swWidth = 29;
            int swHeight = 60;

            _lblSwitchNumberView.AutoSize = false;
            _lblSwitchNumberView.Size = new Size(240, 28);
            _lblSwitchNumberView.BackColor = AppTheme.SwitchNumberBackground;
            _lblSwitchNumberView.ForeColor = TextPrimary;
            _lblSwitchNumberView.Font = new Font("Consolas", 10f, FontStyle.Bold);
            _lblSwitchNumberView.TextAlign = ContentAlignment.MiddleCenter;
            _pictureBox.Controls.Add(_lblSwitchNumberView);

            for (int i = 0; i < SolenoidCount; i++)
            {
                int switchIndex = i;
                _switches[i] = new DipSwitch
                {
                    Location = new Point(474 + (SolenoidCount - 1 - i) * spacingX, baseY),
                    Size = new Size(swWidth, swHeight)
                };
                _switches[i].StateChanged += (_, _) => OnSwitchStateChanged(switchIndex);
                AttachSwKeyHoverHandlers(_switches[i], $"SW{i}");
                _pictureBox.Controls.Add(_switches[i]);
                _switches[i].BringToFront();

                _switchLabels[i] = new Label
                {
                    AutoSize = false,
                    Size = new Size(swWidth, 18),
                    BackColor = AppTheme.SwitchLabelBackground,
                    ForeColor = TextPrimary,
                    Font = new Font("Consolas", 8f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = $"SW{i}"
                };
                _pictureBox.Controls.Add(_switchLabels[i]);
                _switchLabels[i].BringToFront();
            }

            PositionSwitchLabels();
            PositionSwitchNumberView();
            _lblSwitchNumberView.BringToFront();

            _btnKey0 = new RoundButton
            {
                Text = "KEY0",
                Location = new Point(696, 307),
                Size = new Size(40, 40),
                BackColor = KeyIdleColor,
                BorderColor = KeyIdleBorderColor
            };
            _btnKey0.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    BeginKeyHold(0, _btnKey0);
            };
            _btnKey0.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    EndKeyHold(0, _btnKey0);
            };
            _btnKey0.MouseCaptureChanged += (s, e) =>
            {
                if (_keyPressedStates[0])
                    EndKeyHold(0, _btnKey0);
            };
            AttachSwKeyHoverHandlers(_btnKey0, "KEY0");
            _pictureBox.Controls.Add(_btnKey0);
            _btnKey0.BringToFront();

            _btnKey1 = new RoundButton
            {
                Text = "KEY1",
                Location = new Point(696, 348),
                Size = new Size(40, 40),
                BackColor = KeyIdleColor,
                BorderColor = KeyIdleBorderColor
            };
            _btnKey1.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    BeginKeyHold(1, _btnKey1);
            };
            _btnKey1.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    EndKeyHold(1, _btnKey1);
            };
            _btnKey1.MouseCaptureChanged += (s, e) =>
            {
                if (_keyPressedStates[1])
                    EndKeyHold(1, _btnKey1);
            };
            AttachSwKeyHoverHandlers(_btnKey1, "KEY1");
            _pictureBox.Controls.Add(_btnKey1);
            _btnKey1.BringToFront();
            LoadSavedSwKeyLayout();
            ApplySwKeyVisibilityMode();
            RefreshSwitchAnnotations();
        }

        private void OnSwitchStateChanged(int index)
        {
            // ON -> ON channel, OFF -> OFF channel
            int channel = _switches[index].IsOn ? index * 2 : index * 2 + 1;
            TriggerChannel(channel);
            UpdateNumberDisplays();
        }

        private void BeginKeyHold(int keyIndex, RoundButton? button)
        {
            if (button == null || _keyPressedStates[keyIndex])
                return;

            _keyPressedStates[keyIndex] = true;
            button.Capture = true;
            button.BackColor = KeyActiveColor;
            button.BorderColor = KeyActiveBorderColor;
            button.Invalidate();

            int channel = 20 + keyIndex;
            SendKeyStateCommand(channel, true);

            string keyName = keyIndex == 0 ? "KEY0" : "KEY1";
            if (_logWindow != null && !_logWindow.IsDisposed)
                _logWindow.AddLog($"{keyName} → ON");
            else
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {keyName} → ON");

            UpdateStatusText($"{keyName}: удержание активно");
        }

        private void EndKeyHold(int keyIndex, RoundButton? button)
        {
            if (!_keyPressedStates[keyIndex])
                return;

            _keyPressedStates[keyIndex] = false;

            if (button != null && !button.IsDisposed)
            {
                button.Capture = false;
                button.BackColor = KeyIdleColor;
                button.BorderColor = KeyIdleBorderColor;
                button.Invalidate();
            }

            int channel = 20 + keyIndex;
            SendKeyStateCommand(channel, false);

            string keyName = keyIndex == 0 ? "KEY0" : "KEY1";
            if (_logWindow != null && !_logWindow.IsDisposed)
                _logWindow.AddLog($"{keyName} → OFF");
            else
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {keyName} → OFF");

            UpdateStatusText($"{keyName}: удержание снято");
        }

        private void SendKeyStateCommand(int channel, bool isPressed)
        {
            if (channel < 20 || channel > 21)
                return;

            _channelActiveUntil[channel] = isPressed ? DateTime.MaxValue : DateTime.MinValue;
            string keyName = channel == 20 ? "KEY0" : "KEY1";
            SendCommand($"{keyName}_{(isPressed ? "ON" : "OFF")}\n");
        }

        private void TriggerChannel(int channel)
        {
            if (channel < 0 || channel >= TotalChannels)
                return;

            _channelActiveUntil[channel] = DateTime.UtcNow.AddMilliseconds(_pulseVisualMs);

            string command = channel.ToString() + "\n";
            SendCommand(command);

            string message = ChannelToDescription(channel);
            if (_logWindow != null && !_logWindow.IsDisposed)
                _logWindow.AddLog($"{message} → ИМПУЛЬС {FormatPulseDuration()}");
            else
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message} → ИМПУЛЬС {FormatPulseDuration()}");

            UpdateStatusText($"Последний канал: CH{channel} / {message}");
        }

        private static string ChannelToDescription(int channel)
        {
            if (channel < 20)
                return $"Sol{channel / 2} {(channel % 2 == 0 ? "ON" : "OFF")}";
            return channel == 20 ? "KEY0" : "KEY1";
        }

        private void VisualResetTimer_Tick(object? sender, EventArgs e)
        {
            // только перерисовка для оверлея и возможных визуальных зависимостей
            if (_videoPanel.IsHandleCreated)
                _videoPanel.Invalidate();
        }

        private void SetSwKeyVisibilityMode(SwKeyVisibilityMode mode)
        {
            _swKeyVisibilityMode = mode;
            if (mode != SwKeyVisibilityMode.HoverReveal)
                _hoveredSwKeyId = null;

            ApplySwKeyVisibilityMode();
            UpdateHideMenuTexts();
        }

        private void ApplySwKeyVisibilityMode()
        {
            bool forceVisible = _editMode;
            bool hoverRevealMode = !forceVisible && _swKeyVisibilityMode == SwKeyVisibilityMode.HoverReveal;
            bool controlsVisible = forceVisible || _swKeyVisibilityMode == SwKeyVisibilityMode.Visible;
            bool interactionLocked = hoverRevealMode;

            for (int i = 0; i < _switches.Length; i++)
            {
                _switches[i].Visible = controlsVisible;
                _switches[i].StealthMode = false;
                _switches[i].HoverHighlighted = false;
                _switches[i].InteractionLocked = interactionLocked;
            }

            ApplyKeyVisibilityMode(_btnKey0, controlsVisible, interactionLocked);
            ApplyKeyVisibilityMode(_btnKey1, controlsVisible, interactionLocked);

            PositionSwitchLabels();
            UpdateHoverInfo();
            UpdateNumberDisplays();
            _pictureBox.Invalidate();
        }

        private void ApplyKeyVisibilityMode(RoundButton? button, bool controlsVisible, bool interactionLocked)
        {
            if (button == null)
                return;

            button.Visible = controlsVisible;
            button.StealthMode = false;
            button.InteractionLocked = interactionLocked;
            button.HoverHighlighted = false;
        }

        private void AttachSwKeyHoverHandlers(Control control, string id)
        {
            control.Tag = id;
            control.MouseEnter += SwKey_MouseEnter;
            control.MouseLeave += SwKey_MouseLeave;
        }

        private void SwKey_MouseEnter(object? sender, EventArgs e)
        {
            if (IsHoverRevealModeActive())
                return;

            if (sender is not Control control || control.Tag is not string id)
                return;

            _hoveredSwKeyId = id;
            UpdateHoverInfo();

            if (_swKeyVisibilityMode == SwKeyVisibilityMode.HoverReveal)
                ApplySwKeyVisibilityMode();
        }

        private void SwKey_MouseLeave(object? sender, EventArgs e)
        {
            if (IsHoverRevealModeActive())
                return;

            if (sender is not Control control || control.Tag is not string id)
                return;

            if (!string.Equals(_hoveredSwKeyId, id, StringComparison.OrdinalIgnoreCase))
                return;

            _hoveredSwKeyId = null;
            UpdateHoverInfo();

            if (_swKeyVisibilityMode == SwKeyVisibilityMode.HoverReveal)
                ApplySwKeyVisibilityMode();
        }

        private void UpdateHoverInfo()
        {
            if (_lblHoverInfo == null)
                return;

            _lblHoverInfo.Text = string.IsNullOrWhiteSpace(_hoveredSwKeyId)
                ? "Элемент:\n-"
                : $"Элемент:\n{_hoveredSwKeyId}";
        }

        private bool IsHoverRevealModeActive()
        {
            return !_editMode && _swKeyVisibilityMode == SwKeyVisibilityMode.HoverReveal;
        }

        private void UpdateHoverRevealFromPoint(Point location)
        {
            string? hoveredId = FindHoveredSwKeyId(location);
            if (string.Equals(_hoveredSwKeyId, hoveredId, StringComparison.OrdinalIgnoreCase))
                return;

            _hoveredSwKeyId = hoveredId;
            UpdateHoverInfo();
            _pictureBox.Invalidate();
        }

        private string? FindHoveredSwKeyId(Point location)
        {
            if (_btnKey0 != null && _btnKey0.Bounds.Contains(location))
                return "KEY0";
            if (_btnKey1 != null && _btnKey1.Bounds.Contains(location))
                return "KEY1";

            for (int i = 0; i < _switches.Length; i++)
            {
                if (_switches[i].Bounds.Contains(location))
                    return $"SW{i}";
            }

            return null;
        }

        private void PictureBox_MouseLeave(object? sender, EventArgs e)
        {
            if (!IsHoverRevealModeActive())
                return;

            if (_hoveredSwKeyId == null)
                return;

            _hoveredSwKeyId = null;
            UpdateHoverInfo();
            _pictureBox.Invalidate();
        }

        private void PictureBox_Paint(object? sender, PaintEventArgs e)
        {
            if (!IsHoverRevealModeActive() || string.IsNullOrWhiteSpace(_hoveredSwKeyId))
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            if (_hoveredSwKeyId.StartsWith("SW", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(_hoveredSwKeyId.Substring(2), out int switchIndex) &&
                switchIndex >= 0 && switchIndex < _switches.Length)
            {
                DrawSwitchHoverPreview(e.Graphics, _switches[switchIndex].Bounds);
                return;
            }

            if (string.Equals(_hoveredSwKeyId, "KEY0", StringComparison.OrdinalIgnoreCase) && _btnKey0 != null)
            {
                DrawKeyHoverPreview(e.Graphics, _btnKey0.Bounds);
                return;
            }

            if (string.Equals(_hoveredSwKeyId, "KEY1", StringComparison.OrdinalIgnoreCase) && _btnKey1 != null)
                DrawKeyHoverPreview(e.Graphics, _btnKey1.Bounds);
        }

        private static void DrawSwitchHoverPreview(Graphics graphics, Rectangle bounds)
        {
            var bodyRect = new Rectangle(bounds.X + 1, bounds.Y + 1, Math.Max(1, bounds.Width - 3), Math.Max(1, bounds.Height - 3));
            int bodyRadius = Math.Max(8, Math.Min(bounds.Width, bounds.Height) / 7);
            using var fillBrush = new SolidBrush(Color.FromArgb(28, 255, 232, 120));
            graphics.FillRoundedRectangle(fillBrush, bodyRect, bodyRadius);
            using var highlightPen = new Pen(Color.FromArgb(235, 255, 232, 120), 2f);
            graphics.DrawRoundedRectangle(highlightPen, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), Math.Max(8, bodyRadius + 1));
        }

        private static void DrawKeyHoverPreview(Graphics graphics, Rectangle bounds)
        {
            using var fillBrush = new SolidBrush(Color.FromArgb(28, 255, 232, 120));
            graphics.FillEllipse(fillBrush, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using var highlightPen = new Pen(Color.FromArgb(235, 255, 232, 120), 3f);
            graphics.DrawEllipse(highlightPen, bounds.X + 1, bounds.Y + 1, Math.Max(1, bounds.Width - 3), Math.Max(1, bounds.Height - 3));
        }

        private void ToggleOverlayVisibility()
        {
            _showOverlay = !_showOverlay;
            UpdateHideMenuTexts();
        }

        private void ToggleContoursVisibility()
        {
            _showCustomContours = !_showCustomContours;
            UpdateHideMenuTexts();
            _pictureBox.Invalidate();
        }

        private void MenuOverlayTransparency_Click(object? sender, EventArgs e)
        {
            using var dialog = new OverlayTransparencyDialog(_overlayTransparencyAlpha);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            _overlayTransparencyAlpha = dialog.OverlayAlpha;
            _videoPanel.Invalidate();
            UpdateStatusText($"Прозрачность оверлея: {_overlayTransparencyAlpha}");
        }

        private void MenuEditOverlayRects_Click(object? sender, EventArgs e)
        {
            _editOverlayRectsMode = !_editOverlayRectsMode;
            UpdateNumberDisplays();
            UpdateStatusText(_editOverlayRectsMode
                ? "Редактирование прямоугольников включено"
                : "Редактирование прямоугольников выключено");
        }

        private void MenuAddOverlayRect_Click(object? sender, EventArgs e)
        {
            using var colorDialog = new ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
                Color = Color.DeepSkyBlue
            };

            if (colorDialog.ShowDialog(this) != DialogResult.OK)
                return;

            Size frameSize = _pictureBox.Image?.Size ?? new Size(640, 480);
            InitializeOverlayLayoutIfNeeded(frameSize);

            var rectSize = new Size(
                Math.Max(20, _mainOverlayRect.Width / 5),
                Math.Max(14, _mainOverlayRect.Height / 4));

            int x = Math.Max(0, Math.Min(frameSize.Width - rectSize.Width, _mainOverlayRect.X + 20));
            int y = Math.Max(0, Math.Min(frameSize.Height - rectSize.Height, _mainOverlayRect.Bottom + 20));

            _customOverlayRects.Add(new CustomOverlayRect
            {
                Name = $"Контур {_customOverlayRects.Count + 1}",
                Bounds = new Rectangle(x, y, rectSize.Width, rectSize.Height),
                Color = colorDialog.Color
            });

            _pictureBox.Invalidate();
            UpdateHideMenuTexts();
            UpdateStatusText("Добавлен новый полупрозрачный контур");
        }

        private void MenuRemoveOverlayRect_Click(object? sender, EventArgs e)
        {
            if (_customOverlayRects.Count == 0)
            {
                MessageBox.Show(
                    "Сейчас нет пользовательских контуров для удаления.",
                    "Удаление контура",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dialog = new OverlayRectDeleteDialog(
                _customOverlayRects.Select(rect => rect.Name).ToList(),
                SetPreviewCustomOverlayRectIndex);

            try
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                int index = dialog.SelectedIndex;
                if (index < 0 || index >= _customOverlayRects.Count)
                    return;

                string removedName = _customOverlayRects[index].Name;
                _customOverlayRects.RemoveAt(index);
                if (_activeCustomOverlayRectIndex == index)
                    _activeCustomOverlayRectIndex = -1;
                else if (_activeCustomOverlayRectIndex > index)
                    _activeCustomOverlayRectIndex--;

                _pictureBox.Invalidate();
                UpdateHideMenuTexts();
                UpdateStatusText($"Удалён контур: {removedName}");
            }
            finally
            {
                SetPreviewCustomOverlayRectIndex(-1);
            }
        }

        private void SetPreviewCustomOverlayRectIndex(int index)
        {
            _previewCustomOverlayRectIndex = index >= 0 && index < _customOverlayRects.Count ? index : -1;
            _pictureBox.Invalidate();
        }

        private void InitializeOverlayLayoutIfNeeded(Size frameSize)
        {
            if (_overlayLayoutInitialized)
                return;

            int rectWidth = 405;
            int rectHeight = 90;
            int x = (frameSize.Width - rectWidth) / 2 + 13;
            int y = (frameSize.Height - rectHeight) / 2 + 40;

            _mainOverlayRect = new Rectangle(x, y, rectWidth, rectHeight);
            _headerOverlayRect = new Rectangle(x, y - 70, rectWidth, 60);
            _overlayLayoutInitialized = true;
        }

        private Rectangle GetOverlayRect(OverlayRectSelection selection)
        {
            return selection switch
            {
                OverlayRectSelection.Main => _mainOverlayRect,
                OverlayRectSelection.Header => _headerOverlayRect,
                OverlayRectSelection.Custom when _activeCustomOverlayRectIndex >= 0 && _activeCustomOverlayRectIndex < _customOverlayRects.Count
                    => _customOverlayRects[_activeCustomOverlayRectIndex].Bounds,
                _ => Rectangle.Empty
            };
        }

        private void SetOverlayRect(OverlayRectSelection selection, Rectangle rect)
        {
            if (selection == OverlayRectSelection.Main)
                _mainOverlayRect = rect;
            else if (selection == OverlayRectSelection.Header)
                _headerOverlayRect = rect;
            else if (selection == OverlayRectSelection.Custom &&
                     _activeCustomOverlayRectIndex >= 0 &&
                     _activeCustomOverlayRectIndex < _customOverlayRects.Count)
                _customOverlayRects[_activeCustomOverlayRectIndex].Bounds = rect;
        }

        private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_editOverlayRectsMode || e.Button != MouseButtons.Left || _pictureBox.Image == null)
                return;

            if (!TryMapPicturePointToImage(e.Location, out var imagePoint))
                return;

            const int gripSize = 12;
            _activeCustomOverlayRectIndex = -1;

            if (_showCustomContours)
            {
                for (int i = _customOverlayRects.Count - 1; i >= 0; i--)
                {
                    if (!_customOverlayRects[i].Bounds.Contains(imagePoint))
                        continue;

                    _activeOverlayRect = OverlayRectSelection.Custom;
                    _activeCustomOverlayRectIndex = i;
                    break;
                }
            }

            if (_showOverlay && _activeOverlayRect == OverlayRectSelection.None && _mainOverlayRect.Contains(imagePoint))
            {
                _activeOverlayRect = OverlayRectSelection.Main;
            }
            else if (_showOverlay && _activeOverlayRect == OverlayRectSelection.None && _headerOverlayRect.Contains(imagePoint))
            {
                _activeOverlayRect = OverlayRectSelection.Header;
            }
            else if (_activeOverlayRect == OverlayRectSelection.None)
            {
                _activeOverlayRect = OverlayRectSelection.None;
                return;
            }

            var activeRect = GetOverlayRect(_activeOverlayRect);
            _overlayRectResizing = imagePoint.X >= activeRect.Right - gripSize && imagePoint.Y >= activeRect.Bottom - gripSize;
            _overlayRectDragging = !_overlayRectResizing;
            _overlayRectStartBounds = activeRect;
            _overlayMouseDownImage = imagePoint;
            _pictureBox.Capture = true;
        }

        private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (IsHoverRevealModeActive() && !_editOverlayRectsMode)
                UpdateHoverRevealFromPoint(e.Location);

            if (!_editOverlayRectsMode || _activeOverlayRect == OverlayRectSelection.None || _pictureBox.Image == null)
                return;

            if (!TryMapPicturePointToImage(e.Location, out var imagePoint))
                return;

            var imageSize = _pictureBox.Image.Size;
            int deltaX = imagePoint.X - _overlayMouseDownImage.X;
            int deltaY = imagePoint.Y - _overlayMouseDownImage.Y;
            Rectangle newRect = _overlayRectStartBounds;

            if (_overlayRectResizing)
            {
                int minWidth = _activeOverlayRect == OverlayRectSelection.Custom ? 8 : 120;
                int minHeight = _activeOverlayRect == OverlayRectSelection.Custom ? 8 : 40;
                newRect.Width = Math.Max(minWidth, _overlayRectStartBounds.Width + deltaX);
                newRect.Height = Math.Max(minHeight, _overlayRectStartBounds.Height + deltaY);
            }
            else if (_overlayRectDragging)
            {
                newRect.X = _overlayRectStartBounds.X + deltaX;
                newRect.Y = _overlayRectStartBounds.Y + deltaY;
            }

            newRect.X = Math.Max(0, Math.Min(imageSize.Width - newRect.Width, newRect.X));
            newRect.Y = Math.Max(0, Math.Min(imageSize.Height - newRect.Height, newRect.Y));
            newRect.Width = Math.Min(newRect.Width, imageSize.Width - newRect.X);
            newRect.Height = Math.Min(newRect.Height, imageSize.Height - newRect.Y);

            SetOverlayRect(_activeOverlayRect, newRect);
            _videoPanel.Invalidate();
        }

        private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_activeOverlayRect == OverlayRectSelection.None)
                return;

            _pictureBox.Capture = false;
            _activeOverlayRect = OverlayRectSelection.None;
            _activeCustomOverlayRectIndex = -1;
            _overlayRectDragging = false;
            _overlayRectResizing = false;
        }

        private bool TryMapPicturePointToImage(Point point, out Point imagePoint)
        {
            imagePoint = Point.Empty;
            if (_pictureBox.Image == null)
                return false;

            Rectangle imageRect = GetImageDisplayRectangle(_pictureBox);
            if (!imageRect.Contains(point))
                return false;

            float scaleX = _pictureBox.Image.Width / (float)imageRect.Width;
            float scaleY = _pictureBox.Image.Height / (float)imageRect.Height;

            imagePoint = new Point(
                (int)((point.X - imageRect.X) * scaleX),
                (int)((point.Y - imageRect.Y) * scaleY));
            return true;
        }

        private static Rectangle GetImageDisplayRectangle(PictureBox pictureBox)
        {
            if (pictureBox.Image == null)
                return Rectangle.Empty;

            Size imageSize = pictureBox.Image.Size;
            Size clientSize = pictureBox.ClientSize;
            float ratio = Math.Min(clientSize.Width / (float)imageSize.Width, clientSize.Height / (float)imageSize.Height);

            int width = (int)(imageSize.Width * ratio);
            int height = (int)(imageSize.Height * ratio);
            int x = (clientSize.Width - width) / 2;
            int y = (clientSize.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }

        private static void DrawOverlayEditHandle(Graphics graphics, Rectangle rect)
        {
            var handleRect = new Rectangle(rect.Right - 10, rect.Bottom - 10, 8, 8);
            using var fillBrush = new SolidBrush(Color.FromArgb(210, 255, 255, 255));
            using var borderPen = new Pen(Color.Black, 1);
            graphics.FillRectangle(fillBrush, handleRect);
            graphics.DrawRectangle(borderPen, handleRect);
        }

        private Label CreateSelectionCard(Control parent, string title)
        {
            var card = new ThemedSurfacePanel
            {
                Size = new Size(236, 48),
                SurfaceColor = Color.White,
                BorderColor = Color.FromArgb(194, 221, 216),
                GlowColor = Color.FromArgb(8, 121, 189, 180),
                CornerRadius = 22,
                Margin = new Padding(0, 4, 12, 4),
                Padding = new Padding(14, 7, 14, 7)
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Left,
                Width = 78,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var valueLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            parent.Controls.Add(card);
            return valueLabel;
        }

        private Label CreateSectionPill(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                ForeColor = TextSecondary,
                BackColor = AccentPill,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Padding = new Padding(12, 7, 12, 7),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private void CreateInfoBlock(Control parent, Label label, string title, ref int y, int height)
        {
            var block = new ThemedSurfacePanel
            {
                Location = new Point(16, y),
                Size = new Size(Math.Max(220, parent.ClientSize.Width - 32), height),
                SurfaceColor = Color.White,
                BorderColor = Color.FromArgb(194, 221, 216),
                GlowColor = Color.FromArgb(8, 121, 189, 180),
                CornerRadius = 24,
                Padding = new Padding(16, 15, 16, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            label.Dock = DockStyle.Fill;
            label.BackColor = Color.Transparent;
            label.ForeColor = TextPrimary;
            label.Padding = new Padding(0, 6, 0, 0);

            block.Controls.Add(label);
            block.Controls.Add(titleLabel);
            parent.Controls.Add(block);
            y += height + 14;
        }

        private void UpdateHideMenuTexts()
        {
            if (_menuToggleOverlay != null)
                _menuToggleOverlay.Checked = _showOverlay;
            if (_menuToggleContours != null)
                _menuToggleContours.Checked = _showCustomContours;
            if (_menuToggleTheme != null)
                _menuToggleTheme.Checked = AppTheme.IsDark;
            if (_menuRotateCamera180 != null)
                _menuRotateCamera180.Checked = _rotateCamera180;

            if (_menuSwitchesVisible != null)
                _menuSwitchesVisible.Checked = _swKeyVisibilityMode == SwKeyVisibilityMode.Visible;
            if (_menuSwitchesHidden != null)
                _menuSwitchesHidden.Checked = _swKeyVisibilityMode == SwKeyVisibilityMode.Hidden;
            if (_menuSwitchesHoverReveal != null)
                _menuSwitchesHoverReveal.Checked = _swKeyVisibilityMode == SwKeyVisibilityMode.HoverReveal;
            if (_menuRemoveOverlayRect != null)
                _menuRemoveOverlayRect.Enabled = _customOverlayRects.Count > 0;

            UpdateNumberDisplays();
        }

        private void ToggleTheme()
        {
            AppTheme.SetMode(AppTheme.IsDark ? AppThemeMode.Light : AppThemeMode.Dark);
            ApplyTheme();
            if (_logWindow != null && !_logWindow.IsDisposed)
                _logWindow.ApplyTheme();
            UpdateStatusText(AppTheme.IsDark ? "Темная тема активна" : "Светлая тема активна");
        }

        private void ToggleCameraRotation()
        {
            _rotateCamera180 = !_rotateCamera180;
            UpdateHideMenuTexts();
            UpdateStatusText(_rotateCamera180
                ? "Поворот камеры 180° включен"
                : "Поворот камеры 180° выключен");
        }

        private void ApplyTheme()
        {
            BackColor = BgPrimary;
            if (_rootLayout != null)
                _rootLayout.BackColor = BgPrimary;
            if (_mainLayout != null)
                _mainLayout.BackColor = BgPrimary;

            if (_topToolStrip != null)
            {
                _topToolStrip.ForeColor = TextPrimary;
                _topToolStrip.Invalidate();
            }

            if (_menuToggleTheme != null)
                _menuToggleTheme.Checked = AppTheme.IsDark;

            foreach (var item in GetToolStripItems())
                item.ForeColor = TextPrimary;

            if (_cmbCamera != null)
            {
                _cmbCamera.BackColor = AppTheme.ComboBoxBackground;
                _cmbCamera.ForeColor = AppTheme.ComboBoxText;
            }

            if (_cmbComPort != null)
            {
                _cmbComPort.BackColor = AppTheme.ComboBoxBackground;
                _cmbComPort.ForeColor = AppTheme.ComboBoxText;
            }

            ApplyHostTheme(_toolbarHost, SurfacePrimary, AppTheme.IsDark ? Color.FromArgb(26, AccentBlue) : Color.FromArgb(14, 124, 191, 181));
            ApplyHostTheme(_selectionHost, SurfaceSecondary, AppTheme.IsDark ? Color.FromArgb(20, AccentCyan) : Color.FromArgb(12, 126, 191, 182));
            ApplyHostTheme(_controlHost, SurfacePrimary, AppTheme.IsDark ? Color.FromArgb(18, AccentCyan) : Color.FromArgb(12, 126, 191, 182));

            if (_videoHost != null)
                _videoHost.BackColor = BgCanvas;

            _videoPanel.BackColor = AppTheme.VideoPanelBackground;
            _pictureBox.BackColor = AppTheme.PictureBoxBackground;
            _lblSwitchNumberView.BackColor = AppTheme.SwitchNumberBackground;
            _lblSwitchNumberView.ForeColor = TextPrimary;

            foreach (var label in _switchLabels)
            {
                label.BackColor = AppTheme.SwitchLabelBackground;
                label.ForeColor = TextPrimary;
            }

            if (_lblSessionPill != null)
            {
                _lblSessionPill.BackColor = AccentPill;
                _lblSessionPill.ForeColor = TextSecondary;
            }

            if (_lblToolsPill != null)
            {
                _lblToolsPill.BackColor = AccentPill;
                _lblToolsPill.ForeColor = TextSecondary;
            }

            if (_lblControlTitle != null)
                _lblControlTitle.ForeColor = TextPrimary;
            if (_lblControlSubtitle != null)
                _lblControlSubtitle.ForeColor = TextSecondary;

            ApplySelectionCardTheme(_lblModeValue?.Parent as ThemedSurfacePanel);
            ApplySelectionCardTheme(_lblBoardValue?.Parent as ThemedSurfacePanel);
            ApplySelectionCardTheme(_lblCourseValue?.Parent as ThemedSurfacePanel);
            ApplyInfoBlockTheme(_lblStatus.Parent as ThemedSurfacePanel, _lblStatus);
            ApplyInfoBlockTheme(_lblNumbersSummary.Parent as ThemedSurfacePanel, _lblNumbersSummary);
            ApplyInfoBlockTheme(_lblHoverInfo.Parent as ThemedSurfacePanel, _lblHoverInfo);

            ApplyKeyTheme(_btnKey0, 20);
            ApplyKeyTheme(_btnKey1, 21);

            Invalidate(true);
        }

        private void ApplyHostTheme(ThemedSurfacePanel? panel, Color surfaceColor, Color glowColor)
        {
            if (panel == null) return;
            panel.SurfaceColor = surfaceColor;
            panel.BorderColor = BorderSoft;
            panel.GlowColor = glowColor;
            panel.Invalidate();
        }

        private void ApplySelectionCardTheme(ThemedSurfacePanel? card)
        {
            if (card == null) return;
            card.SurfaceColor = AppTheme.IsDark ? Color.FromArgb(31, 63, 77) : Color.White;
            card.BorderColor = AppTheme.IsDark ? BorderSoft : Color.FromArgb(194, 221, 216);
            card.GlowColor = AppTheme.IsDark ? Color.FromArgb(12, AccentCyan) : Color.FromArgb(8, 121, 189, 180);
            foreach (var control in card.Controls.OfType<Label>())
                control.ForeColor = control.Dock == DockStyle.Left ? TextSecondary : TextPrimary;
            card.Invalidate();
        }

        private void ApplyInfoBlockTheme(ThemedSurfacePanel? block, Label contentLabel)
        {
            if (block == null) return;
            block.SurfaceColor = AppTheme.IsDark ? Color.FromArgb(25, 53, 67) : Color.White;
            block.BorderColor = AppTheme.IsDark ? BorderSoft : Color.FromArgb(194, 221, 216);
            block.GlowColor = AppTheme.IsDark ? Color.FromArgb(10, AccentBlue) : Color.FromArgb(8, 121, 189, 180);
            contentLabel.ForeColor = TextPrimary;
            foreach (var control in block.Controls.OfType<Label>())
            {
                if (!ReferenceEquals(control, contentLabel))
                    control.ForeColor = TextSecondary;
            }
            block.Invalidate();
        }

        private void ApplyKeyTheme(RoundButton? button, int channel)
        {
            if (button == null) return;
            bool active = channel is 20 or 21
                ? _keyPressedStates[channel - 20]
                : DateTime.UtcNow < _channelActiveUntil[channel];
            button.BackColor = active ? KeyActiveColor : KeyIdleColor;
            button.BorderColor = active ? KeyActiveBorderColor : KeyIdleBorderColor;
            button.ForeColor = AppTheme.IsDark ? Color.White : TextPrimary;
            button.Invalidate();
        }

        private IEnumerable<ToolStripItem> GetToolStripItems()
        {
            if (_topToolStrip == null)
                yield break;

            foreach (ToolStripItem item in _topToolStrip.Items)
                yield return item;
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

        private void SelectNumberPresentation(NumberPresentation presentation)
        {
            _selectedNumberPresentation = presentation;
            UpdateNumberDisplays();
        }

        private void MenuPulseDuration_Click(object? sender, EventArgs e)
        {
            using var dialog = new PulseDurationDialog(_pulseVisualMs);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            _pulseVisualMs = dialog.PulseDurationMs;
            UpdateStatusText($"Время сигнала: {FormatPulseDuration()}");
        }

        private string FormatPulseDuration()
        {
            return $"{_pulseVisualMs / 1000d:0.###}с";
        }

        private void UpdateNumberDisplays()
        {
            int switchValue = GetSwitchValue();

            if (_menuNumberBinary != null) _menuNumberBinary.Checked = _selectedNumberPresentation == NumberPresentation.Binary;
            if (_menuNumberOctal != null) _menuNumberOctal.Checked = _selectedNumberPresentation == NumberPresentation.Octal;
            if (_menuNumberDecimal != null) _menuNumberDecimal.Checked = _selectedNumberPresentation == NumberPresentation.Decimal;
            if (_menuNumberHex != null) _menuNumberHex.Checked = _selectedNumberPresentation == NumberPresentation.Hexadecimal;
            if (_menuEditOverlayRects != null) _menuEditOverlayRects.Checked = _editOverlayRectsMode;

            _lblSwitchNumberView.Text = $"{GetPresentationLabel(_selectedNumberPresentation)}: {FormatValue(switchValue, _selectedNumberPresentation)}";
            PositionSwitchNumberView();
            bool showSwitchNumberView = _switches.Length > 0 &&
                _switches[0].Visible &&
                (_editMode || _swKeyVisibilityMode == SwKeyVisibilityMode.Visible);
            _lblSwitchNumberView.Visible = showSwitchNumberView;

            if (_lblNumbersSummary != null)
            {
                _lblNumbersSummary.Text =
                    "SW сумма" + Environment.NewLine +
                    $"BIN {FormatValue(switchValue, NumberPresentation.Binary)}" + Environment.NewLine +
                    $"OCT {FormatValue(switchValue, NumberPresentation.Octal)}" + Environment.NewLine +
                    $"DEC {FormatValue(switchValue, NumberPresentation.Decimal)}" + Environment.NewLine +
                    $"HEX {FormatValue(switchValue, NumberPresentation.Hexadecimal)}";
            }
        }

        private int GetSwitchValue()
        {
            int value = 0;

            for (int i = 0; i < _switches.Length; i++)
            {
                if (_switches[i] != null && _switches[i].IsOn)
                    value |= 1 << i;
            }

            return value;
        }

        private static string GetPresentationLabel(NumberPresentation presentation)
        {
            return presentation switch
            {
                NumberPresentation.Binary => "BIN",
                NumberPresentation.Octal => "OCT",
                NumberPresentation.Decimal => "DEC",
                NumberPresentation.Hexadecimal => "HEX",
                _ => "BIN"
            };
        }

        private string FormatValue(int value, NumberPresentation presentation)
        {
            return presentation switch
            {
                NumberPresentation.Binary => Convert.ToString(value, 2).PadLeft(SolenoidCount, '0'),
                NumberPresentation.Octal => Convert.ToString(value, 8),
                NumberPresentation.Decimal => value.ToString(),
                NumberPresentation.Hexadecimal => value.ToString("X"),
                _ => value.ToString()
            };
        }

        private void PositionSwitchNumberView()
        {
            if (_switches.Length == 0 || _switches[0] == null)
                return;

            int left = _switches.Min(sw => sw.Left);
            int right = _switches.Max(sw => sw.Right);
            int labelsBottom = _switchLabels
                .Where(label => label != null)
                .Select(label => label.Bottom)
                .DefaultIfEmpty(_switches.Max(sw => sw.Bottom))
                .Max();

            int width = Math.Max(240, right - left + 20);
            _lblSwitchNumberView.Size = new Size(width, 28);
            _lblSwitchNumberView.Location = new Point(left - 10, labelsBottom + 6);
        }

        private void PositionSwitchLabels()
        {
            for (int i = 0; i < _switches.Length; i++)
            {
                if (_switchLabels[i] == null)
                    continue;

                _switchLabels[i].Size = new Size(Math.Max(10, _switches[i].Width), _switchLabels[i].Height);
                _switchLabels[i].Location = new Point(_switches[i].Left, _switches[i].Bottom + 8);
                bool hovered = string.Equals(_hoveredSwKeyId, $"SW{i}", StringComparison.OrdinalIgnoreCase);
                _switchLabels[i].Visible = _switches[i].Visible &&
                    (_editMode || _swKeyVisibilityMode == SwKeyVisibilityMode.Visible || hovered);
            }
        }

        private void RefreshSwitchAnnotations()
        {
            PositionSwitchLabels();
            PositionSwitchNumberView();
            UpdateNumberDisplays();

            _lblSwitchNumberView.BringToFront();
            foreach (var label in _switchLabels)
                label?.BringToFront();
        }

        private void SetSwKeyEditMode(bool enabled)
        {
            _editMode = enabled;
            if (_menuEditOverlayEnabled != null)
                _menuEditOverlayEnabled.Checked = _editMode;

            SetEditMode(_switches, _editMode);
            if (_btnKey0 != null && _btnKey1 != null)
                SetEditMode(new Control[] { _btnKey0, _btnKey1 }, _editMode);

            ApplySwKeyVisibilityMode();

            if (_editMode)
                OpenSwKeyEditorWindow();
            else
                CloseSwKeyEditorWindow();
        }

        private void OpenSwKeyEditorWindow()
        {
            if (_swKeyEditorWindow != null && !_swKeyEditorWindow.IsDisposed)
            {
                _swKeyEditorWindow.RefreshTargets();
                _swKeyEditorWindow.Show();
                _swKeyEditorWindow.BringToFront();
                return;
            }

            _swKeyEditorWindow = new SwKeyLayoutEditorWindow(this);
            _swKeyEditorWindow.FormClosed += (_, _) =>
            {
                _swKeyEditorWindow = null;
                if (_editMode)
                    SetSwKeyEditMode(false);
            };
            _swKeyEditorWindow.Show(this);
        }

        private void CloseSwKeyEditorWindow()
        {
            if (_swKeyEditorWindow == null || _swKeyEditorWindow.IsDisposed)
                return;

            _swKeyEditorWindow.FormClosed -= (_, _) => { };
            _swKeyEditorWindow.Close();
            _swKeyEditorWindow = null;
        }

        internal List<SwKeyLayoutTarget> GetSwKeyLayoutTargets()
        {
            var targets = new List<SwKeyLayoutTarget>();

            for (int i = 0; i < _switches.Length; i++)
            {
                var sw = _switches[i];
                targets.Add(new SwKeyLayoutTarget($"SW{i}", sw.Left, sw.Top, sw.Width, sw.Height, true));
            }

            if (_btnKey0 != null)
                targets.Add(new SwKeyLayoutTarget("KEY0", _btnKey0.Left, _btnKey0.Top, _btnKey0.Width, _btnKey0.Height, false));

            if (_btnKey1 != null)
                targets.Add(new SwKeyLayoutTarget("KEY1", _btnKey1.Left, _btnKey1.Top, _btnKey1.Width, _btnKey1.Height, false));

            return targets;
        }

        internal List<SwKeyLayoutTarget> GetDefaultSwKeyLayoutTargets()
        {
            return BuildInitialSwKeyLayoutTargets();
        }

        private List<SwKeyLayoutTarget> BuildInitialSwKeyLayoutTargets()
        {
            const int baseY = 474;
            const int startX = 474;
            const int spacingX = 29;
            var targets = new List<SwKeyLayoutTarget>();

            for (int i = 0; i < SolenoidCount; i++)
            {
                targets.Add(new SwKeyLayoutTarget(
                    $"SW{i}",
                    startX + (SolenoidCount - 1 - i) * spacingX,
                    baseY,
                    29,
                    60,
                    true));
            }

            targets.Add(new SwKeyLayoutTarget("KEY0", 696, 307, 40, 40, false));
            targets.Add(new SwKeyLayoutTarget("KEY1", 696, 348, 40, 40, false));
            return targets;
        }

        internal void ResetSwKeyLayoutToDefaults()
        {
            ApplySwKeyLayoutTargets(GetDefaultSwKeyLayoutTargets());
        }

        internal void SaveSwKeyLayout()
        {
            try
            {
                File.WriteAllText(SwKeyLayoutFilePath, SerializeSwKeyLayout(GetSwKeyLayoutTargets()));
            }
            catch
            {
            }
        }

        internal void ExportSwKeyLayoutToFile(IWin32Window? dialogOwner = null)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Сохранить раскладку SW/KEY",
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                DefaultExt = "json",
                AddExtension = true,
                FileName = "swkey-layout.json"
            };

            if (dialog.ShowDialog(dialogOwner ?? this) != DialogResult.OK)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, SerializeSwKeyLayout(GetSwKeyLayoutTargets()));
                UpdateStatusText($"Раскладка SW/KEY сохранена: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Не удалось сохранить файл раскладки.\n{ex.Message}",
                    "Ошибка сохранения",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        internal void ImportSwKeyLayoutFromFile(IWin32Window? dialogOwner = null)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Загрузить раскладку SW/KEY",
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(dialogOwner ?? this) != DialogResult.OK)
                return;

            try
            {
                string json = File.ReadAllText(dialog.FileName);
                var targets = DeserializeSwKeyLayout(json);
                if (targets == null || targets.Count == 0)
                    throw new InvalidDataException("Файл не содержит раскладку SW/KEY.");

                ApplySwKeyLayoutTargets(NormalizeSavedSwitchOrder(targets));
                SaveSwKeyLayout();
                UpdateStatusText($"Раскладка SW/KEY загружена: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Не удалось загрузить файл раскладки.\n{ex.Message}",
                    "Ошибка загрузки",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LoadSavedSwKeyLayout()
        {
            try
            {
                if (!File.Exists(SwKeyLayoutFilePath))
                    return;

                string json = File.ReadAllText(SwKeyLayoutFilePath);
                var targets = DeserializeSwKeyLayout(json);
                if (targets == null || targets.Count == 0)
                    return;

                ApplySwKeyLayoutTargets(NormalizeSavedSwitchOrder(targets));
            }
            catch
            {
            }
        }

        private List<SwKeyLayoutTarget> NormalizeSavedSwitchOrder(List<SwKeyLayoutTarget> targets)
        {
            var orderedSwitchTargets = targets
                .Where(target => target.IsSwitch && target.Id.StartsWith("SW", StringComparison.OrdinalIgnoreCase))
                .Select(target => new
                {
                    Target = target,
                    Index = int.TryParse(target.Id.Substring(2), out int index) ? index : -1
                })
                .Where(item => item.Index >= 0)
                .OrderBy(item => item.Target.X)
                .ToList();

            if (orderedSwitchTargets.Count != SolenoidCount)
                return targets;

            bool oldLeftToRightOrder = orderedSwitchTargets
                .Select(item => item.Index)
                .SequenceEqual(Enumerable.Range(0, SolenoidCount));

            if (!oldLeftToRightOrder)
                return targets;

            var remappedSwitchTargets = new Dictionary<string, SwKeyLayoutTarget>(StringComparer.OrdinalIgnoreCase);
            for (int slotIndex = 0; slotIndex < orderedSwitchTargets.Count; slotIndex++)
            {
                var slot = orderedSwitchTargets[slotIndex].Target;
                string desiredId = $"SW{SolenoidCount - 1 - slotIndex}";
                remappedSwitchTargets[desiredId] = slot with { Id = desiredId };
            }

            var normalizedTargets = new List<SwKeyLayoutTarget>(targets.Count);
            foreach (var target in targets)
            {
                if (target.IsSwitch && remappedSwitchTargets.TryGetValue(target.Id, out var remappedTarget))
                    normalizedTargets.Add(remappedTarget);
                else
                    normalizedTargets.Add(target);
            }

            return normalizedTargets;
        }

        private static string SerializeSwKeyLayout(IEnumerable<SwKeyLayoutTarget> targets)
        {
            return JsonSerializer.Serialize(targets, new JsonSerializerOptions { WriteIndented = true });
        }

        private static List<SwKeyLayoutTarget>? DeserializeSwKeyLayout(string json)
        {
            return JsonSerializer.Deserialize<List<SwKeyLayoutTarget>>(json);
        }

        private void ApplySwKeyLayoutTargets(IEnumerable<SwKeyLayoutTarget> targets)
        {
            foreach (var target in targets)
            {
                Control? control = FindSwKeyControl(target.Id);
                if (control == null)
                    continue;

                int clampedWidth = Math.Max(10, target.Width);
                int clampedHeight = Math.Max(10, target.Height);

                if (control is RoundButton)
                {
                    int side = Math.Max(clampedWidth, clampedHeight);
                    clampedWidth = side;
                    clampedHeight = side;
                }

                control.Location = new Point(
                    Math.Max(0, Math.Min(_videoPanel.ClientSize.Width - clampedWidth, target.X)),
                    Math.Max(0, Math.Min(_videoPanel.ClientSize.Height - clampedHeight, target.Y)));
                control.Size = new Size(clampedWidth, clampedHeight);
            }

            PositionSwitchLabels();
            PositionSwitchNumberView();
            _swKeyEditorWindow?.RefreshTargets();
        }

        internal void ApplySwKeyLayout(IReadOnlyList<string> targetIds, int x, int y, int width, int height, int radius, bool keyMode)
        {
            foreach (string targetId in targetIds)
            {
                Control? control = FindSwKeyControl(targetId);
                if (control == null)
                    continue;

                int clampedWidth;
                int clampedHeight;

                if (keyMode && control is RoundButton)
                {
                    int clampedRadius = Math.Max(10, radius);
                    int diameter = clampedRadius * 2;
                    clampedWidth = diameter;
                    clampedHeight = diameter;
                }
                else
                {
                    clampedWidth = Math.Max(10, width);
                    clampedHeight = Math.Max(10, height);

                    if (control is RoundButton)
                    {
                        int side = Math.Max(clampedWidth, clampedHeight);
                        clampedWidth = side;
                        clampedHeight = side;
                    }
                }

                control.Location = new Point(
                    Math.Max(0, Math.Min(_videoPanel.ClientSize.Width - clampedWidth, x)),
                    Math.Max(0, Math.Min(_videoPanel.ClientSize.Height - clampedHeight, y)));

                control.Size = new Size(clampedWidth, clampedHeight);
            }

            PositionSwitchLabels();
            PositionSwitchNumberView();
            _swKeyEditorWindow?.RefreshTargets();
            SaveSwKeyLayout();
        }

        private Control? FindSwKeyControl(string targetId)
        {
            if (targetId.StartsWith("SW", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(targetId.Substring(2), out int switchIndex) &&
                switchIndex >= 0 && switchIndex < _switches.Length)
            {
                return _switches[switchIndex];
            }

            if (string.Equals(targetId, "KEY0", StringComparison.OrdinalIgnoreCase))
                return _btnKey0;

            if (string.Equals(targetId, "KEY1", StringComparison.OrdinalIgnoreCase))
                return _btnKey1;

            return null;
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

            if (_btnModeMenu != null) _btnModeMenu.Text = "Режим";
            if (_btnBoardMenu != null) _btnBoardMenu.Text = "Плата";
            if (_btnCourseMenu != null) _btnCourseMenu.Text = "Курс";

            if (_lblModeValue != null) _lblModeValue.Text = _selectedMode;
            if (_lblBoardValue != null) _lblBoardValue.Text = _selectedBoard;
            if (_lblCourseValue != null) _lblCourseValue.Text = _selectedCourse;
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
            SetSwKeyEditMode(!_editMode);
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
            if (sender is not Control ctrl) return;

            const int gripSize = 12;
            _mouseDownGlobal = ctrl.PointToScreen(e.Location);
            _controlStartGlobal = ctrl.PointToScreen(Point.Empty);

            if (e.X >= ctrl.Width - gripSize && e.Y >= ctrl.Height - gripSize)
            {
                _resizing = true;
                _startSize = ctrl.Size;
            }
            else
            {
                _resizing = false;
            }

            _draggedControl = ctrl;
            ctrl.Capture = true;
        }

        private void Control_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_draggedControl == null || !_editMode) return;
            Point currentMouseGlobal = _draggedControl.PointToScreen(e.Location);

            if (_resizing)
            {
                int deltaX = currentMouseGlobal.X - _mouseDownGlobal.X;
                int deltaY = currentMouseGlobal.Y - _mouseDownGlobal.Y;
                int newWidth = Math.Max(10, _startSize.Width + deltaX);
                int newHeight = Math.Max(10, _startSize.Height + deltaY);

                if (_draggedControl is RoundButton)
                {
                    int side = Math.Max(newWidth, newHeight);
                    newWidth = side;
                    newHeight = side;
                }

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

            PositionSwitchLabels();
            PositionSwitchNumberView();
            _swKeyEditorWindow?.RefreshTargets();
        }

        private void Control_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_draggedControl == null) return;
            _draggedControl.Capture = false;
            _draggedControl = null;
            _resizing = false;
            _swKeyEditorWindow?.RefreshTargets();
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
                Bitmap bmp;

                if (_rotateCamera180)
                {
                    using var rotatedFrame = new Mat();
                    CvInvoke.Rotate(frame, rotatedFrame, Emgu.CV.CvEnum.RotateFlags.Rotate180);
                    bmp = rotatedFrame.ToBitmap();
                }
                else
                {
                    bmp = frame.ToBitmap();
                }

                if (_showOverlay || _showCustomContours)
                {
                    using var g = Graphics.FromImage(bmp);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    InitializeOverlayLayoutIfNeeded(bmp.Size);
                    int x = _mainOverlayRect.X;
                    int y = _mainOverlayRect.Y;

                    if (_showOverlay)
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(_overlayTransparencyAlpha, 0, 0, 0)))
                            g.FillRectangle(brush, _mainOverlayRect);

                        using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 3))
                            g.DrawRectangle(pen, _mainOverlayRect);

                        int headerAlpha = Math.Max(40, _overlayTransparencyAlpha - 40);
                        using (var brush = new SolidBrush(Color.FromArgb(headerAlpha, 0, 0, 0)))
                            g.FillRectangle(brush, _headerOverlayRect);

                        using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 2))
                            g.DrawRectangle(pen, _headerOverlayRect);

                        using (var titleBrush = new SolidBrush(Color.FromArgb(132, 233, 255)))
                        using (var titleFont = new Font("Segoe UI", 11, FontStyle.Bold | FontStyle.Italic))
                        {
                            string line1 = $"{_selectedBoard} | {_selectedMode}";
                            string line2 = $"{_selectedCourse}";
                            g.DrawString(line1, titleFont, titleBrush, _headerOverlayRect.X + 12, _headerOverlayRect.Y + 10);
                            using var subBrush = new SolidBrush(Color.FromArgb(255, 214, 120));
                            using var subFont = new Font("Segoe UI", 9, FontStyle.Italic);
                            g.DrawString(line2, subFont, subBrush, _headerOverlayRect.X + 12, _headerOverlayRect.Y + 33);
                        }

                        float ledPaddingX = Math.Max(8f, _mainOverlayRect.Width * 0.037f);
                        float ledPaddingTop = Math.Max(5f, _mainOverlayRect.Height * 0.06f);
                        float ledBandWidth = Math.Max(60f, _mainOverlayRect.Width - ledPaddingX * 2f);
                        float ledSlotWidth = ledBandWidth / SolenoidCount;
                        float ledSizeF = Math.Max(8f, Math.Min(ledSlotWidth * 0.34f, _mainOverlayRect.Height * 0.22f));
                        float ledTop = _mainOverlayRect.Y + ledPaddingTop;
                        float labelTop = _mainOverlayRect.Y + Math.Max(18f, _mainOverlayRect.Height * 0.46f);
                        float ledTextWidth = Math.Max(20f, ledSlotWidth);
                        float ledFontSize = Math.Max(6.5f, Math.Min(10f, _mainOverlayRect.Height * 0.09f));
                        using var ledFont = new Font("Consolas", ledFontSize, FontStyle.Bold);

                        for (int i = 0; i < 10; i++)
                        {
                            float slotLeft = _mainOverlayRect.X + ledPaddingX + i * ledSlotWidth;
                            int ledSize = (int)Math.Round(ledSizeF);
                            int ledX = (int)Math.Round(slotLeft + (ledSlotWidth - ledSizeF) / 2f);
                            int ledY = (int)Math.Round(ledTop);

                            bool dir1Active = DateTime.UtcNow < _channelActiveUntil[i * 2];
                            bool dir2Active = DateTime.UtcNow < _channelActiveUntil[i * 2 + 1];
                            bool ledIsActive = dir1Active || dir2Active;
                            Color ledColor = dir1Active ? Color.LimeGreen : dir2Active ? Color.OrangeRed : Color.Gray;
                            var ledRect = new Rectangle(ledX, ledY, ledSize, ledSize);

                            using (var ledBrush = new SolidBrush(Color.FromArgb(ledIsActive ? 185 : 50, ledColor)))
                                g.FillRectangle(ledBrush, ledRect);

                            using (var ledPen = new Pen(Color.FromArgb(ledIsActive ? 235 : 110, ledColor), 1.2f))
                                g.DrawRectangle(ledPen, ledRect);

                            if (ledIsActive)
                            {
                                using var glowBrush = new SolidBrush(Color.FromArgb(70, Color.White));
                                int glowSize = Math.Max(2, ledRect.Width / 3);
                                g.FillRectangle(glowBrush, ledRect.X + 2, ledRect.Y + 2, glowSize, glowSize);
                            }

                            using (var textBrush = new SolidBrush(Color.White))
                                g.DrawString($"LED{i}", ledFont, textBrush,
                                    new RectangleF(slotLeft, labelTop, ledTextWidth, _mainOverlayRect.Height - (labelTop - _mainOverlayRect.Y)),
                                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near });
                        }
                    }

                    if (_showCustomContours)
                    {
                        for (int i = 0; i < _customOverlayRects.Count; i++)
                        {
                            var customRect = _customOverlayRects[i];
                            using var fillBrush = new SolidBrush(Color.FromArgb(_overlayTransparencyAlpha, customRect.Color));
                            g.FillRectangle(fillBrush, customRect.Bounds);

                            float borderWidth = i == _previewCustomOverlayRectIndex ? 4f : 2f;
                            Color borderColor = i == _previewCustomOverlayRectIndex
                                ? Color.FromArgb(255, 255, 240, 120)
                                : Color.FromArgb(230, customRect.Color);
                            using var borderPen = new Pen(borderColor, borderWidth);
                            g.DrawRectangle(borderPen, customRect.Bounds);
                        }
                    }

                    if (_editOverlayRectsMode)
                    {
                        if (_showOverlay)
                        {
                            DrawOverlayEditHandle(g, _mainOverlayRect);
                            DrawOverlayEditHandle(g, _headerOverlayRect);
                        }

                        if (_showCustomContours)
                        {
                            foreach (var customRect in _customOverlayRects)
                                DrawOverlayEditHandle(g, customRect.Bounds);
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
            var portsToCheck = GetAvailableComPorts();
            RefreshComPortList();

            foreach (string portName in portsToCheck)
            {
                try
                {
                    var testPort = new SerialPort(portName, 115200)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        NewLine = "\n"
                    };

                    testPort.Open();
                    _serialPort = testPort;
                    RefreshComPortList(portName);
                    UpdateStatusText($"Подключено к {portName}");
                    return;
                }
                catch
                {
                }
            }

            UpdateStatusText("НЕТ ПОДКЛЮЧЕНИЯ!");
            MessageBox.Show(
                "Не удалось подключиться к STM32.\nУправление будет работать без связи с платой.",
                "COM-порт не найден",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private List<string> GetAvailableComPorts()
        {
            return SerialPort.GetPortNames()
                .OrderBy(port =>
                {
                    int preferredIndex = Array.IndexOf(PreferredComPorts, port);
                    return preferredIndex >= 0 ? preferredIndex : int.MaxValue;
                })
                .ThenBy(port => port, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void RefreshComPortList(string? selectedPort = null)
        {
            if (_cmbComPort == null)
                return;

            _isUpdatingComPortList = true;

            try
            {
                var ports = GetAvailableComPorts();
                _cmbComPort.Items.Clear();

                if (ports.Count == 0)
                {
                    _cmbComPort.Items.Add("Нет доступных COM");
                    _cmbComPort.Enabled = false;
                    _cmbComPort.SelectedIndex = 0;
                    return;
                }

                foreach (var port in ports)
                    _cmbComPort.Items.Add(port);

                _cmbComPort.Enabled = true;

                string? portToSelect = selectedPort;
                if (string.IsNullOrWhiteSpace(portToSelect) && _serialPort?.IsOpen == true)
                    portToSelect = _serialPort.PortName;

                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(portToSelect))
                {
                    int foundIndex = ports.FindIndex(port => string.Equals(port, portToSelect, StringComparison.OrdinalIgnoreCase));
                    if (foundIndex >= 0)
                        selectedIndex = foundIndex;
                }

                _cmbComPort.SelectedIndex = selectedIndex;
            }
            finally
            {
                _isUpdatingComPortList = false;
            }
        }

        private void CmbComPort_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingComPortList || _cmbComPort == null || !_cmbComPort.Enabled)
                return;

            if (_cmbComPort.SelectedItem is not string portName || string.IsNullOrWhiteSpace(portName))
                return;

            if (_serialPort?.IsOpen == true &&
                string.Equals(_serialPort.PortName, portName, StringComparison.OrdinalIgnoreCase))
                return;

            if (!TryConnectToSerialPort(portName))
                RefreshComPortList(_serialPort?.PortName);
        }

        private bool TryConnectToSerialPort(string portName)
        {
            SerialPort? nextPort = null;

            try
            {
                nextPort = new SerialPort(portName, 115200)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    NewLine = "\n"
                };

                nextPort.Open();

                var previousPort = _serialPort;
                _serialPort = nextPort;
                nextPort = null;

                try
                {
                    if (previousPort?.IsOpen == true)
                        previousPort.Close();
                }
                catch
                {
                }
                finally
                {
                    previousPort?.Dispose();
                }

                RefreshComPortList(portName);
                UpdateStatusText($"Подключено к {portName}");
                return true;
            }
            catch
            {
                nextPort?.Dispose();

                MessageBox.Show(
                    $"Не удалось подключиться к {portName}. Возможно, порт занят или недоступен.",
                    "Ошибка подключения",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }
        }

        private void SendCommand(string command)
        {
            if (_serialPort?.IsOpen != true) return;

            try
            {
                _serialPort.Write(command);
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
                                if (_logWindow != null && !_logWindow.IsDisposed)
                                    _logWindow.AddLog(line);

                                if (!line.StartsWith("STATE", StringComparison.OrdinalIgnoreCase))
                                    _lblStatus.Text = line;
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

        private void UpdateStatusText(string text)
        {
            this.InvokeIfRequired(() => _lblStatus.Text = text);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _running = false;
            _readThread?.Join(1200);

            _frameTimer?.Stop();
            _visualResetTimer.Stop();
            _capture?.Dispose();
            CloseSwKeyEditorWindow();

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

    internal sealed record SwKeyLayoutTarget(string Id, int X, int Y, int Width, int Height, bool IsSwitch);

    internal sealed class SwKeyLayoutEditorWindow : Form
    {
        private readonly BoardWindow _owner;
        private readonly ListBox _lstTargets;
        private readonly NumericUpDown _numX;
        private readonly NumericUpDown _numY;
        private readonly NumericUpDown _numWidth;
        private readonly NumericUpDown _numHeight;
        private readonly NumericUpDown _numRadius;
        private readonly Label _lblWidthCaption;
        private readonly Label _lblHeightCaption;
        private readonly Label _lblRadiusCaption;
        private readonly Label _lblHint;

        public SwKeyLayoutEditorWindow(BoardWindow owner)
        {
            _owner = owner;

            Text = "Редактирование SW/KEY";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 408);
            BackColor = AppTheme.DialogBackground;
            Font = new Font("Segoe UI", 9.5f);

            var lblTargets = new Label
            {
                Text = "Элементы:",
                AutoSize = true,
                ForeColor = Color.FromArgb(32, 54, 74),
                Location = new Point(18, 18)
            };
            Controls.Add(lblTargets);

            _lstTargets = new ListBox
            {
                SelectionMode = SelectionMode.MultiExtended,
                Location = new Point(18, 44),
                Size = new Size(150, 230),
                BackColor = AppTheme.DialogSurface,
                ForeColor = AppTheme.DialogText,
                BorderStyle = BorderStyle.FixedSingle
            };
            _lstTargets.SelectedIndexChanged += (_, _) => LoadValuesFromSelection();
            Controls.Add(_lstTargets);

            int editorLeft = 190;
            Controls.Add(CreateFieldLabel("X:", editorLeft, 44));
            _numX = CreateNumeric(editorLeft + 72, 40);
            Controls.Add(_numX);

            Controls.Add(CreateFieldLabel("Y:", editorLeft, 86));
            _numY = CreateNumeric(editorLeft + 72, 82);
            Controls.Add(_numY);

            _lblWidthCaption = CreateFieldLabel("Ширина:", editorLeft, 128);
            Controls.Add(_lblWidthCaption);
            _numWidth = CreateNumeric(editorLeft + 72, 124);
            _numWidth.Minimum = 10;
            Controls.Add(_numWidth);

            _lblHeightCaption = CreateFieldLabel("Высота:", editorLeft, 170);
            Controls.Add(_lblHeightCaption);
            _numHeight = CreateNumeric(editorLeft + 72, 166);
            _numHeight.Minimum = 10;
            Controls.Add(_numHeight);

            _lblRadiusCaption = CreateFieldLabel("Радиус:", editorLeft, 128);
            Controls.Add(_lblRadiusCaption);
            _numRadius = CreateNumeric(editorLeft + 72, 124);
            _numRadius.Minimum = 10;
            Controls.Add(_numRadius);

            _lblHint = new Label
            {
                Text = "Можно выбрать один или несколько элементов.\nПараметры применяются ко всем выбранным.",
                Size = new Size(205, 54),
                ForeColor = AppTheme.DialogHint,
                Location = new Point(editorLeft, 214)
            };
            Controls.Add(_lblHint);

            var btnApply = CreateActionButton("Применить", 190, 286);
            btnApply.Click += (_, _) => ApplyValues();
            Controls.Add(btnApply);

            var btnDefault = CreateActionButton("Default", 302, 286);
            btnDefault.Click += (_, _) =>
            {
                _owner.ResetSwKeyLayoutToDefaults();
                RefreshTargets();
            };
            Controls.Add(btnDefault);

            var btnSave = CreateActionButton("Сохранить...", 190, 330);
            btnSave.Click += (_, _) => _owner.ExportSwKeyLayoutToFile(this);
            Controls.Add(btnSave);

            var btnLoad = CreateActionButton("Загрузить...", 302, 330);
            btnLoad.Click += (_, _) =>
            {
                _owner.ImportSwKeyLayoutFromFile(this);
                RefreshTargets();
            };
            Controls.Add(btnLoad);

            RefreshTargets();
        }

        public void RefreshTargets()
        {
            var selectedIds = _lstTargets.SelectedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var targets = _owner.GetSwKeyLayoutTargets();

            _lstTargets.BeginUpdate();
            _lstTargets.Items.Clear();
            foreach (var target in targets)
                _lstTargets.Items.Add(target.Id);

            for (int i = 0; i < _lstTargets.Items.Count; i++)
            {
                if (selectedIds.Contains(_lstTargets.Items[i]!.ToString()!))
                    _lstTargets.SetSelected(i, true);
            }
            _lstTargets.EndUpdate();

            if (_lstTargets.SelectedItems.Count == 0 && _lstTargets.Items.Count > 0)
                _lstTargets.SelectedIndex = 0;

            LoadValuesFromSelection();
        }

        private void LoadValuesFromSelection()
        {
            if (_lstTargets.SelectedItems.Count == 0)
                return;

            string firstId = _lstTargets.SelectedItems[0]!.ToString()!;
            var target = _owner.GetSwKeyLayoutTargets().FirstOrDefault(item => item.Id == firstId);
            if (target == null)
                return;

            _numX.Value = ClampToNumericRange(_numX, target.X);
            _numY.Value = ClampToNumericRange(_numY, target.Y);
            bool selectedOnlyKeys = _lstTargets.SelectedItems.Cast<string>()
                .All(id => id.StartsWith("KEY", StringComparison.OrdinalIgnoreCase));

            _lblWidthCaption.Visible = !selectedOnlyKeys;
            _numWidth.Visible = !selectedOnlyKeys;
            _lblHeightCaption.Visible = !selectedOnlyKeys;
            _numHeight.Visible = !selectedOnlyKeys;
            _lblRadiusCaption.Visible = selectedOnlyKeys;
            _numRadius.Visible = selectedOnlyKeys;
            _lblHint.Text = selectedOnlyKeys
                ? "Можно выбрать один или несколько KEY.\nРадиус применяется ко всем выбранным."
                : "Можно выбрать один или несколько SW.\nШирина и высота применяются ко всем выбранным.";

            if (selectedOnlyKeys)
            {
                _numRadius.Value = ClampToNumericRange(_numRadius, target.Width / 2);
            }
            else
            {
                _numWidth.Value = ClampToNumericRange(_numWidth, target.Width);
                _numHeight.Value = ClampToNumericRange(_numHeight, target.Height);
            }
        }

        private void ApplyValues()
        {
            var targetIds = _lstTargets.SelectedItems.Cast<string>().ToList();
            if (targetIds.Count == 0)
                return;

            bool selectedOnlyKeys = targetIds.All(id => id.StartsWith("KEY", StringComparison.OrdinalIgnoreCase));
            _owner.ApplySwKeyLayout(
                targetIds,
                (int)_numX.Value,
                (int)_numY.Value,
                (int)_numWidth.Value,
                (int)_numHeight.Value,
                (int)_numRadius.Value,
                selectedOnlyKeys);
        }

        private static Label CreateFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = AppTheme.DialogText,
                Location = new Point(x, y + 6)
            };
        }

        private static NumericUpDown CreateNumeric(int x, int y)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(120, 28),
                Minimum = 0,
                Maximum = 5000,
                BackColor = AppTheme.DialogSurface,
                ForeColor = AppTheme.DialogText,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static Button CreateActionButton(string text, int x, int y)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(104, 34),
                Location = new Point(x, y),
                BackColor = AppTheme.DialogPrimaryButtonBackground,
                ForeColor = AppTheme.DialogPrimaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static decimal ClampToNumericRange(NumericUpDown numeric, int value)
        {
            return Math.Max(numeric.Minimum, Math.Min(numeric.Maximum, value));
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

    internal class TransparentPanel : Panel
    {
        public TransparentPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            TransparentBackgroundRenderer.PaintTransparentHostBackground(this, e.Graphics);
        }
    }

    internal sealed class TransparentFlowLayoutPanel : FlowLayoutPanel
    {
        public TransparentFlowLayoutPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            TransparentBackgroundRenderer.PaintTransparentHostBackground(this, e.Graphics);
        }
    }

    internal static class TransparentBackgroundRenderer
    {
        public static void PaintTransparentHostBackground(Control control, Graphics graphics)
        {
            if (control.Parent is ThemedSurfacePanel themedParent)
            {
                using var brush = new SolidBrush(themedParent.SurfaceColor);
                graphics.FillRectangle(brush, control.ClientRectangle);
                return;
            }

            if (control.Parent != null)
            {
                using var brush = new SolidBrush(control.Parent.BackColor);
                graphics.FillRectangle(brush, control.ClientRectangle);
                return;
            }

            graphics.Clear(SystemColors.Control);
        }
    }

    internal sealed class ThemedSurfacePanel : Panel
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color SurfaceColor { get; set; } = Color.FromArgb(248, 252, 255);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.FromArgb(194, 215, 229);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color GlowColor { get; set; } = Color.FromArgb(12, 96, 203, 241);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 22;

        public ThemedSurfacePanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            TransparentBackgroundRenderer.PaintTransparentHostBackground(this, e.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var shadowRect = new Rectangle(3, 5, Math.Max(1, Width - 9), Math.Max(1, Height - 10));
            var bounds = new Rectangle(1, 1, Math.Max(1, Width - 6), Math.Max(1, Height - 7));

            if (GlowColor.A > 0)
            {
                using var shadowBrush = new SolidBrush(GlowColor);
                e.Graphics.FillRoundedRectangle(shadowBrush, shadowRect, Math.Max(8, CornerRadius));
            }

            using var fillBrush = new SolidBrush(SurfaceColor);
            e.Graphics.FillRoundedRectangle(fillBrush, bounds, CornerRadius);

            using (var borderPen = new Pen(BorderColor, 1.1f))
            {
                e.Graphics.DrawRoundedRectangle(borderPen, bounds, CornerRadius);
            }
        }
    }

    internal sealed class TopMenuRenderer : ToolStripProfessionalRenderer
    {
        private static Color HoverBackColor => AppTheme.MenuHover;
        private static Color PressedBackColor => AppTheme.MenuPressed;
        private static Color MenuBackColor => AppTheme.MenuBackground;

        public TopMenuRenderer() : base(new TopMenuColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is not ToolStripDropDown && e.ToolStrip.BackColor == Color.Transparent)
                return;

            Color backgroundColor = e.ToolStrip is ToolStripDropDown ? MenuBackColor : e.ToolStrip.BackColor;
            using var brush = new SolidBrush(backgroundColor);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            FillMenuItemBackground(e);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            FillMenuItemBackground(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            FillMenuItemBackground(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = AppTheme.Current.TextPrimary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        private static void FillMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            Color fillColor = Color.Transparent;

            if (e.Item.Pressed)
                fillColor = PressedBackColor;
            else if (e.Item.Selected)
                fillColor = HoverBackColor;

            if (fillColor == Color.Transparent)
                return;

            using var brush = new SolidBrush(fillColor);
            e.Graphics.FillRectangle(brush, bounds);
        }
    }

    internal sealed class TopMenuColorTable : ProfessionalColorTable
    {
        private static Color MenuBackColor => AppTheme.MenuBackground;

        public override Color ToolStripDropDownBackground => MenuBackColor;
        public override Color ImageMarginGradientBegin => MenuBackColor;
        public override Color ImageMarginGradientMiddle => MenuBackColor;
        public override Color ImageMarginGradientEnd => MenuBackColor;
        public override Color MenuBorder => AppTheme.MenuBorder;
        public override Color MenuItemBorder => AppTheme.MenuBorder;
        public override Color MenuItemSelected => AppTheme.MenuHover;
        public override Color MenuItemSelectedGradientBegin => AppTheme.MenuHover;
        public override Color MenuItemSelectedGradientEnd => AppTheme.MenuHover;
        public override Color MenuItemPressedGradientBegin => AppTheme.MenuPressed;
        public override Color MenuItemPressedGradientMiddle => AppTheme.MenuPressed;
        public override Color MenuItemPressedGradientEnd => AppTheme.MenuPressed;
        public override Color ButtonSelectedHighlight => AppTheme.MenuHover;
        public override Color ButtonSelectedHighlightBorder => AppTheme.MenuBorder;
        public override Color ButtonPressedHighlight => AppTheme.MenuPressed;
        public override Color ButtonPressedHighlightBorder => AppTheme.MenuBorder;
        public override Color SeparatorDark => AppTheme.MenuSeparator;
        public override Color SeparatorLight => AppTheme.MenuSeparator;
    }

    internal sealed class OverlayTransparencyDialog : Form
    {
        private readonly NumericUpDown _numAlpha;

        public int OverlayAlpha => (int)_numAlpha.Value;

        public OverlayTransparencyDialog(int currentAlpha)
        {
            Text = "Прозрачность прямоугольников";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(340, 145);
            BackColor = AppTheme.DialogBackground;
            Font = new Font("Segoe UI", 9.5f);

            var lblPrompt = new Label
            {
                Text = "Прозрачность (0-255):",
                AutoSize = true,
                ForeColor = Color.FromArgb(32, 54, 74),
                Location = new Point(18, 18)
            };

            _numAlpha = new NumericUpDown
            {
                Minimum = 20,
                Maximum = 255,
                Increment = 5,
                Value = Math.Max(20, Math.Min(255, currentAlpha)),
                Size = new Size(120, 28),
                Location = new Point(18, 48),
                BackColor = AppTheme.DialogSurface,
                ForeColor = AppTheme.DialogText,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblHint = new Label
            {
                Text = "Меньше значение = больше прозрачности.",
                AutoSize = true,
                ForeColor = AppTheme.DialogHint,
                Location = new Point(18, 82)
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(86, 32),
                Location = new Point(144, 104),
                BackColor = AppTheme.DialogPrimaryButtonBackground,
                ForeColor = AppTheme.DialogPrimaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Size = new Size(86, 32),
                Location = new Point(236, 104),
                BackColor = AppTheme.DialogSecondaryButtonBackground,
                ForeColor = AppTheme.DialogSecondaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            Controls.Add(lblPrompt);
            Controls.Add(_numAlpha);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }

    internal sealed class OverlayRectDeleteDialog : Form
    {
        private readonly ListBox _listBox;
        private readonly Action<int>? _selectionChanged;

        public int SelectedIndex => _listBox.SelectedIndex;

        public OverlayRectDeleteDialog(IReadOnlyList<string> rectNames, Action<int>? selectionChanged = null)
        {
            _selectionChanged = selectionChanged;
            Text = "Удалить контур";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(320, 235);
            BackColor = AppTheme.DialogBackground;
            Font = new Font("Segoe UI", 9.5f);

            var lblPrompt = new Label
            {
                Text = "Выберите контур:",
                AutoSize = true,
                ForeColor = Color.FromArgb(32, 54, 74),
                Location = new Point(18, 16)
            };

            _listBox = new ListBox
            {
                Location = new Point(18, 42),
                Size = new Size(284, 130),
                BackColor = AppTheme.DialogSurface,
                ForeColor = AppTheme.DialogText,
                BorderStyle = BorderStyle.FixedSingle
            };
            _listBox.SelectedIndexChanged += (_, _) => _selectionChanged?.Invoke(_listBox.SelectedIndex);

            foreach (var rectName in rectNames)
                _listBox.Items.Add(rectName);

            if (_listBox.Items.Count > 0)
                _listBox.SelectedIndex = 0;

            var btnOk = new Button
            {
                Text = "Удалить",
                DialogResult = DialogResult.OK,
                Size = new Size(86, 32),
                Location = new Point(124, 188),
                BackColor = AppTheme.DialogPrimaryButtonBackground,
                ForeColor = AppTheme.DialogPrimaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Size = new Size(86, 32),
                Location = new Point(216, 188),
                BackColor = AppTheme.DialogSecondaryButtonBackground,
                ForeColor = AppTheme.DialogSecondaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            Controls.Add(lblPrompt);
            Controls.Add(_listBox);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            FormClosed += (_, _) => _selectionChanged?.Invoke(-1);
        }
    }

    internal sealed class PulseDurationDialog : Form
    {
        private readonly NumericUpDown _numDuration;

        public int PulseDurationMs => (int)_numDuration.Value;

        public PulseDurationDialog(int currentDurationMs)
        {
            Text = "Время сигнала";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(320, 145);
            BackColor = AppTheme.DialogBackground;
            Font = new Font("Segoe UI", 9.5f);

            var lblPrompt = new Label
            {
                Text = "Длительность сигнала (мс):",
                AutoSize = true,
                ForeColor = Color.FromArgb(32, 54, 74),
                Location = new Point(18, 18)
            };

            _numDuration = new NumericUpDown
            {
                Minimum = 50,
                Maximum = 10000,
                Increment = 50,
                Value = Math.Max(50, Math.Min(10000, currentDurationMs)),
                Size = new Size(120, 28),
                Location = new Point(18, 48),
                BackColor = AppTheme.DialogSurface,
                ForeColor = AppTheme.DialogText,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblHint = new Label
            {
                Text = "Можно задать от 50 до 10000 мс.",
                AutoSize = true,
                ForeColor = AppTheme.DialogHint,
                Location = new Point(18, 82)
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(86, 32),
                Location = new Point(124, 104),
                BackColor = AppTheme.DialogPrimaryButtonBackground,
                ForeColor = AppTheme.DialogPrimaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Size = new Size(86, 32),
                Location = new Point(218, 104),
                BackColor = AppTheme.DialogSecondaryButtonBackground,
                ForeColor = AppTheme.DialogSecondaryButtonText,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            Controls.Add(lblPrompt);
            Controls.Add(_numDuration);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}

