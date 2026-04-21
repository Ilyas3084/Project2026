using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyEmguProject
{
    public class RoundButton : Button
    {
        public RoundButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Size = new Size(100, 100);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 12, FontStyle.Bold);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.LimeGreen;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, Width - 1, Height - 1);
            Region = new Region(path);

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

            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width != Height) Height = Width; // сохраняем квадрат
        }
    }
}
