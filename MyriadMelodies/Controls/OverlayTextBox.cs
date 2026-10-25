using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MyriadMelodies.Controls
{
    public class OverlayTextBox : UserControl
    {
        private readonly TextBox input;
        [DefaultValue(32767)] public int MaxLength {get=>input.MaxLength;set=>input.MaxLength=value;}
        [DefaultValue("")]
        public override string Text {get=>input?.Text??base.Text;set{if(input==null) base.Text=value;else input.Text=value;}}
        public OverlayTextBox()
        {
            DoubleBuffered=true;BackColor=OverlayComboBox.Cream;ForeColor=Color.FromArgb(61,69,84);Size=new Size(240,32);
            input=new TextBox {BorderStyle=BorderStyle.None,TabStop=false};
            input.TextChanged+=(s,e)=>OnTextChanged(e);Controls.Add(input);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(input==null) return;
            input.BackColor=BackColor;input.ForeColor=ForeColor;
            input.SetBounds(12,(Height-input.PreferredHeight)/2,Math.Max(1,Width-24),input.PreferredHeight);
        }
        protected override void OnBackColorChanged(EventArgs e){base.OnBackColorChanged(e);PerformLayout();Invalidate();}
        protected override void OnForeColorChanged(EventArgs e){base.OnForeColorChanged(e);PerformLayout();Invalidate();}
        protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);PerformLayout();}
        protected override void OnEnter(EventArgs e){base.OnEnter(e);input?.Focus();}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor??BackColor);
            using(var path=OverlayDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),12))
            using(var fill=new SolidBrush(BackColor)) e.Graphics.FillPath(fill,path);
        }
    }
}
