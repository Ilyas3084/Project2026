using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace MyEmguProject
{
    public class DipSwitch : Control
    {
        private bool _isOn;
        private int _sliderY;
        private bool _dragging;
        private bool _stealthMode;
        private bool _hoverHighlighted;
        private bool _interactionLocked;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DisableInternalHandling { get; set; } = false;

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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;

                _isOn = value;
                UpdateSliderPosition();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? StateChanged;

        public DipSwitch()
        {
            Size = new Size(54, 90);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            UpdateSliderPosition();
        }

        private void UpdateSliderPosition()
        {
            _sliderY = _isOn ? SliderTop : SliderBottom;
            Invalidate();
        }

        private int SliderHeight => Math.Max(20, (int)(Height * 0.31f));
        private int OuterPadding => Math.Max(4, (int)(Math.Min(Width, Height) * 0.08f));
        private int SliderTop => OuterPadding;
        private int SliderBottom => Height - SliderHeight - OuterPadding;
        private int SlotPaddingX => Math.Max(7, (int)(Width * 0.16f));
        private int SlotPaddingY => Math.Max(6, (int)(Height * 0.09f));

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            var bodyRect = new Rectangle(1, 1, Width - 3, Height - 3);
            int bodyRadius = Math.Max(8, Math.Min(Width, Height) / 7);
            if (_stealthMode && !_hoverHighlighted)
                return;

            if (_stealthMode)
            {
                using var fillBrush = new SolidBrush(Color.FromArgb(28, 255, 232, 120));
                e.Graphics.FillRoundedRectangle(fillBrush, bodyRect, bodyRadius);
                using var highlightPen = new Pen(Color.FromArgb(235, 255, 232, 120), 2f);
                e.Graphics.DrawRoundedRectangle(highlightPen, new Rectangle(0, 0, Width - 1, Height - 1), Math.Max(8, bodyRadius + 1));
                return;
            }

            using (var bgBrush = new LinearGradientBrush(
                bodyRect,
                Color.FromArgb(45, 50, 62),
                Color.FromArgb(27, 31, 40),
                90f))
            {
                e.Graphics.FillRoundedRectangle(bgBrush, bodyRect, bodyRadius);
            }

            using (var borderPen = new Pen(Color.FromArgb(80, 130, 150, 170), 1.6f))
            {
                e.Graphics.DrawRoundedRectangle(borderPen, bodyRect, bodyRadius);
            }

            var slotRect = new Rectangle(
                SlotPaddingX,
                SlotPaddingY,
                Math.Max(12, Width - SlotPaddingX * 2),
                Math.Max(24, Height - SlotPaddingY * 2));
            int slotRadius = Math.Max(6, Math.Min(slotRect.Width, slotRect.Height) / 6);
            using (var slotBrush = new SolidBrush(Color.FromArgb(32, 20, 22, 28)))
            {
                e.Graphics.FillRoundedRectangle(slotBrush, slotRect, slotRadius);
            }

            var sliderRect = new Rectangle(
                Math.Max(6, SlotPaddingX - 1),
                _sliderY,
                Math.Max(12, Width - Math.Max(6, SlotPaddingX - 1) * 2),
                SliderHeight);
            int sliderRadius = Math.Max(6, Math.Min(sliderRect.Width, sliderRect.Height) / 5);
            var onTop = Color.FromArgb(32, 197, 124);
            var onBottom = Color.FromArgb(21, 124, 80);
            var offTop = Color.FromArgb(218, 64, 88);
            var offBottom = Color.FromArgb(122, 34, 49);

            using (var sliderBrush = new LinearGradientBrush(
                sliderRect,
                _isOn ? onTop : offTop,
                _isOn ? onBottom : offBottom,
                90f))
            {
                e.Graphics.FillRoundedRectangle(sliderBrush, sliderRect, sliderRadius);
            }

            using (var sliderBorder = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f))
            {
                e.Graphics.DrawRoundedRectangle(sliderBorder, sliderRect, sliderRadius);
            }

            if (_hoverHighlighted)
            {
                var highlightRect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var highlightPen = new Pen(Color.FromArgb(235, 255, 232, 120), 2f);
                e.Graphics.DrawRoundedRectangle(highlightPen, highlightRect, Math.Max(8, bodyRadius + 1));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            GraphicsExtensions.DrawPictureBoxBackground(this, pevent.Graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (DisableInternalHandling || _interactionLocked || e.Button != MouseButtons.Left) return;

            int sliderLeft = Math.Max(6, SlotPaddingX - 1);
            var sliderRect = new Rectangle(sliderLeft, _sliderY, Math.Max(12, Width - sliderLeft * 2), SliderHeight);
            if (!sliderRect.Contains(e.Location)) return;

            _dragging = true;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || DisableInternalHandling || _interactionLocked) return;

            _sliderY = Math.Max(SliderTop, Math.Min(SliderBottom, e.Y - SliderHeight / 2));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging || DisableInternalHandling || _interactionLocked) return;

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

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateSliderPosition();
        }
    }

    internal static class GraphicsExtensions
    {
        public static void DrawPictureBoxBackground(Control control, Graphics graphics)
        {
            if (control.Parent == null)
            {
                graphics.Clear(Color.Transparent);
                return;
            }

            Control backgroundHost = control.Parent;
            PictureBox? pictureBox = null;
            Point controlLocation = control.Location;

            if (control.Parent is PictureBox directPictureBox && directPictureBox.Image != null)
            {
                pictureBox = directPictureBox;
            }
            else
            {
                pictureBox = control.Parent.Controls
                    .OfType<PictureBox>()
                    .FirstOrDefault(pb => pb.Visible && pb.Image != null);
            }

            using (var backBrush = new SolidBrush(backgroundHost.BackColor))
                graphics.FillRectangle(backBrush, control.ClientRectangle);
            if (pictureBox?.Image == null)
                return;

            Rectangle imageRect = GetImageDisplayRectangle(pictureBox);
            if (!ReferenceEquals(control.Parent, pictureBox))
            {
                controlLocation = control.Parent.PointToScreen(control.Location);
                controlLocation = pictureBox.PointToClient(controlLocation);
            }

            var controlRect = new Rectangle(controlLocation, control.Size);
            Rectangle overlap = Rectangle.Intersect(controlRect, imageRect);
            if (overlap.Width <= 0 || overlap.Height <= 0)
                return;

            float scaleX = pictureBox.Image.Width / (float)imageRect.Width;
            float scaleY = pictureBox.Image.Height / (float)imageRect.Height;

            var destRect = new Rectangle(
                overlap.X - controlLocation.X,
                overlap.Y - controlLocation.Y,
                overlap.Width,
                overlap.Height);

            var srcRect = new RectangleF(
                (overlap.X - imageRect.X) * scaleX,
                (overlap.Y - imageRect.Y) * scaleY,
                overlap.Width * scaleX,
                overlap.Height * scaleY);

            graphics.DrawImage(pictureBox.Image, destRect, srcRect, GraphicsUnit.Pixel);
        }

        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle rectangle, int radius)
        {
            using var path = CreateRoundedRectPath(rectangle, radius);
            graphics.FillPath(brush, path);
        }

        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle rectangle, int radius)
        {
            using var path = CreateRoundedRectPath(rectangle, radius);
            graphics.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));
            var path = new GraphicsPath();

            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
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
    }
}
