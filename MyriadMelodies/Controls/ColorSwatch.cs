using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyriadMelodies.Controls
{
    public class ColorSwatch : Control
    {
        private Color swatchColor=Color.White;
        [DefaultValue(typeof(Color),"White")]
        public Color SwatchColor {get=>swatchColor;set{if(swatchColor.ToArgb()==value.ToArgb())return;swatchColor=value;Invalidate();}}
        public ColorSwatch(){Size=new Size(16,16);TabStop=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent?.BackColor??BackColor);
            using(var brush=new SolidBrush(SwatchColor)) e.Graphics.FillEllipse(brush,1,1,Width-2,Height-2);
            using(var pen=new Pen(Color.FromArgb(180,237,232,218))) e.Graphics.DrawEllipse(pen,1,1,Width-2,Height-2);
        }
    }
}
