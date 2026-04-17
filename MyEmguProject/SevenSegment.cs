using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace MyEmguProject
{
    public class SevenSegment : Control
    {
        private int _value;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value & 0xF;
                    Invalidate();
                }
            }
        }

        public SevenSegment()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            Size = new Size(78, 120);
            
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (e.Graphics == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            PointF[][] segments = new PointF[][]
            {
                new[] { new PointF(15,15), new PointF(63,15), new PointF(58,25), new PointF(20,25) },
                new[] { new PointF(63,20), new PointF(70,25), new PointF(70,58), new PointF(63,65) },
                new[] { new PointF(63,70), new PointF(70,75), new PointF(70,108), new PointF(63,115) },
                new[] { new PointF(15,115), new PointF(63,115), new PointF(58,125), new PointF(20,125) },
                new[] { new PointF(8,70), new PointF(15,75), new PointF(15,108), new PointF(8,115) },
                new[] { new PointF(8,20), new PointF(15,25), new PointF(15,58), new PointF(8,65) },
                new[] { new PointF(15,65), new PointF(63,65), new PointF(58,75), new PointF(20,75) }
            };

            string[] patterns = new string[]
            {
                "abcdef", "bc", "abged", "abgcd", "fgbc", "afgcd", "afgcde",
                "abc", "abcdefg", "abcfg", "abefg", "fgced", "afed",
                "bcged", "afged", "afge"
            };

            string pattern = (_value < patterns.Length) ? patterns[_value] : "";

            Color onColor = Color.FromArgb(255, 80, 80);
            Color offColor = Color.FromArgb(50, 20, 20);

            for (int i = 0; i < segments.Length; i++)
            {
                using var brush = new SolidBrush(pattern.Contains("abcdefg"[i]) ? onColor : offColor);
                e.Graphics.FillPolygon(brush, segments[i]);
            }
        }
    }
}