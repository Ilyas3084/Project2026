using System.ComponentModel;
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

            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, Width - 1, Height - 1);
            Region = new Region(path);

            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);

            using var pen = new Pen(BorderColor, 4);
            e.Graphics.DrawEllipse(pen, 2, 2, Width - 5, Height - 5);

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