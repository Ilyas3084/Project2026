using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyEmguProject
{
    public class RoundButton : Control
    {
        private bool _stealthMode;
        private bool _hoverHighlighted;
        private bool _interactionLocked;

        public RoundButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            Size = new Size(100, 100);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 12, FontStyle.Bold);
            Cursor = Cursors.Hand;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.LimeGreen;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool StealthMode
        {
            get => _stealthMode;
            set
            {
                if (_stealthMode == value) return;
                _stealthMode = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HoverHighlighted
        {
            get => _hoverHighlighted;
            set
            {
                if (_hoverHighlighted == value) return;
                _hoverHighlighted = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool InteractionLocked
        {
            get => _interactionLocked;
            set => _interactionLocked = value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, Width - 1, Height - 1);
            Region = new Region(path);

            if (_stealthMode && !_hoverHighlighted)
                return;

            if (_stealthMode)
            {
                using var fillBrush = new SolidBrush(Color.FromArgb(28, 255, 232, 120));
                e.Graphics.FillEllipse(fillBrush, 0, 0, Width - 1, Height - 1);
                using var highlightPen = new Pen(Color.FromArgb(235, 255, 232, 120), 3f);
                e.Graphics.DrawEllipse(highlightPen, 1, 1, Width - 3, Height - 3);
                return;
            }

            var fillRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var brush = new LinearGradientBrush(
                fillRect,
                ControlPaint.Light(BackColor, 0.22f),
                ControlPaint.Dark(BackColor, 0.18f),
                90f))
            {
                e.Graphics.FillEllipse(brush, fillRect);
            }

            using (var highlightBrush = new SolidBrush(Color.FromArgb(34, Color.White)))
            {
                e.Graphics.FillEllipse(highlightBrush, 8, 6, Math.Max(12, Width / 3), Math.Max(10, Height / 4));
            }

            using (var pen = new Pen(Color.FromArgb(145, BorderColor), 3))
            {
                e.Graphics.DrawEllipse(pen, 2, 2, Width - 5, Height - 5);
            }

            using (var glowPen = new Pen(Color.FromArgb(44, BorderColor), 8))
            {
                e.Graphics.DrawEllipse(glowPen, 4, 4, Width - 9, Height - 9);
            }

            if (_hoverHighlighted)
            {
                using var highlightPen = new Pen(Color.FromArgb(235, 255, 232, 120), 3f);
                e.Graphics.DrawEllipse(highlightPen, 1, 1, Width - 3, Height - 3);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            GraphicsExtensions.DrawPictureBoxBackground(this, pevent.Graphics);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width != Height) Height = Width; // сохраняем квадрат
        }
        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (_interactionLocked)
                return;

            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            if (_interactionLocked)
                return;

            base.OnMouseUp(mevent);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_interactionLocked)
                return;

            base.OnClick(e);
        }
    }
}
