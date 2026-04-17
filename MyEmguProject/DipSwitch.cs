using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyEmguProject
{
    public class DipSwitch : Control
    {
        private bool _isOn = false;
        private int _sliderY;
        private bool _dragging;

        private const int SliderHeight = 28;
        private int SliderTop => 6;
        private int SliderBottom => Height - SliderHeight - 6;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DisableInternalHandling { get; set; } = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn != value)
                {
                    _isOn = value;
                    UpdateSliderPosition();
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? StateChanged;

        public DipSwitch()
        {
            Size = new Size(54, 90);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(55, 55, 65);
            Cursor = Cursors.Hand;
            UpdateSliderPosition();
        }

        private void UpdateSliderPosition()
        {
            _sliderY = _isOn ? SliderTop : SliderBottom;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Корпус
            using var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 50));
            e.Graphics.FillRectangle(bgBrush, 0, 0, Width, Height);

            using var borderPen = new Pen(Color.FromArgb(90, 90, 100), 2);
            e.Graphics.DrawRectangle(borderPen, 1, 1, Width - 3, Height - 3);

            // Ползунок
            var rect = new Rectangle(6, _sliderY, Width - 12, SliderHeight);

            using var sliderBrush = new LinearGradientBrush(rect,
                _isOn ? Color.LimeGreen : Color.IndianRed,
                _isOn ? Color.ForestGreen : Color.DarkRed, 90f);

            e.Graphics.FillRectangle(sliderBrush, rect);

            // Текст
            using var font = new Font("Consolas", 9, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, "ON", font, new Rectangle(0, 2, Width, 20),
                Color.WhiteSmoke, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics, "OFF", font, new Rectangle(0, Height - 22, Width, 20),
                Color.WhiteSmoke, TextFormatFlags.HorizontalCenter);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (DisableInternalHandling || e.Button != MouseButtons.Left) return;

            var sliderRect = new Rectangle(6, _sliderY, Width - 12, SliderHeight);
            if (sliderRect.Contains(e.Location))
            {
                _dragging = true;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || DisableInternalHandling) return;

            _sliderY = Math.Max(SliderTop, Math.Min(SliderBottom, e.Y - SliderHeight / 2));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging || DisableInternalHandling) return;

            _dragging = false;
            Capture = false;

            bool newState = _sliderY < (SliderTop + SliderBottom) / 2;
            if (newState != _isOn)
            {
                _isOn = newState;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            UpdateSliderPosition();
        }
    }
}