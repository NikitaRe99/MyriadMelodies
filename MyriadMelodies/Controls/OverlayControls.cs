using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace MyriadMelodies.Controls
{
    internal static class OverlayDrawing
    {
        public static readonly Color FocusBorder = Color.FromArgb(133, 189, 174);
        public static readonly Color Border = Color.FromArgb(65, 72, 74);

        public static Color Blend(Color color, Color target, double amount) => Color.FromArgb(color.A,
            (int)Math.Round(color.R+(target.R-color.R)*amount),
            (int)Math.Round(color.G+(target.G-color.G)*amount),
            (int)Math.Round(color.B+(target.B-color.B)*amount));

        public static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter < 1) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Shape(Control control, int radius)
        {
            if (control.Width < 1 || control.Height < 1) return;
            using (var path = Rounded(control.ClientRectangle, radius * control.DeviceDpi / 96f))
            {
                Region old = control.Region;
                control.Region = new Region(path);
                old?.Dispose();
            }
        }

        public static void Chevron(Graphics graphics, Rectangle area, Color color, bool up)
        {
            float x = area.Left + area.Width / 2f, y = area.Top + area.Height / 2f;
            float half = Math.Max(2, area.Width / 7f), direction = up ? -1 : 1;
            using (var pen = new Pen(color, Math.Max(1.2f, area.Width / 18f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                graphics.DrawLines(pen, new[] { new PointF(x-half,y-direction*1.5f),
                    new PointF(x,y+direction*1.5f), new PointF(x+half,y-direction*1.5f) });
            }
        }
    }

    public enum WindowButtonGlyph { None, Minimize, Close, Plus }

    public class OverlayButton : Button
    {
        [Category("Appearance"), DefaultValue(WindowButtonGlyph.None)]
        public WindowButtonGlyph WindowGlyph {get;set;}
        private bool hovered;
        private bool pressed;
        private int cornerRadius = 12;
        [Category("Appearance"), DefaultValue(12)]
        public int CornerRadius { get => cornerRadius; set { cornerRadius = Math.Clamp(value, 0, 24); Invalidate(); } }

        public OverlayButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
            Color fill = BackColor;
            if (!Enabled) fill = Color.FromArgb(70,79,92);
            else if (pressed) fill = OverlayDrawing.Blend(fill,Color.Black,.05);
            else if (hovered) fill = OverlayDrawing.Blend(fill,Color.White,.06);
            using (var path = OverlayDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1), CornerRadius * DeviceDpi / 96f))
            using (var brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
                if (Focused && ShowFocusCues)
                    using (var pen = new Pen(OverlayDrawing.FocusBorder)) e.Graphics.DrawPath(pen,path);
            }
            if(WindowGlyph!=WindowButtonGlyph.None)
            {
                float cx=Width/2f,cy=Height/2f,half=4*DeviceDpi/96f;
                using(var pen=new Pen(Enabled?ForeColor:Color.Gray,1.4f*DeviceDpi/96f))
                {
                    pen.StartCap=pen.EndCap=LineCap.Round;
                    if(WindowGlyph==WindowButtonGlyph.Minimize) e.Graphics.DrawLine(pen,cx-half,cy,cx+half,cy);
                    else if(WindowGlyph==WindowButtonGlyph.Plus) {e.Graphics.DrawLine(pen,cx-half,cy,cx+half,cy);e.Graphics.DrawLine(pen,cx,cy-half,cx,cy+half);}
                    else {e.Graphics.DrawLine(pen,cx-half,cy-half,cx+half,cy+half);e.Graphics.DrawLine(pen,cx-half,cy+half,cx+half,cy-half);}
                }
            }
            else TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(117,125,125),
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered=true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hovered=false; pressed=false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed=e.Button==MouseButtons.Left; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed=false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space) pressed=true; base.OnKeyDown(e); Invalidate(); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed=false; base.OnKeyUp(e); Invalidate(); }
    }

    [DefaultEvent("ValueChanged"), DefaultProperty("Value"), DefaultBindingProperty("Value")]
    public class OverlayNumericUpDown : UserControl, ISupportInitialize
    {
        private readonly NumericUpDown editor;
        private readonly TextBox input;
        private bool synchronizing;
        private readonly Timer repeatTimer;
        private int direction;
        private int cornerRadius=15;
        [Category("Appearance"), DefaultValue(15)]
        public int CornerRadius { get => cornerRadius; set { cornerRadius=Math.Clamp(value,0,24); OverlayDrawing.Shape(this,cornerRadius); Invalidate(); } }
        [Category("Data"), DefaultValue(typeof(decimal),"0")]
        public decimal Value { get => editor.Value; set {editor.Value=value;UpdateText();} }
        [Category("Data"), DefaultValue(typeof(decimal),"0")]
        public decimal Minimum { get => editor.Minimum; set => editor.Minimum=value; }
        [Category("Data"), DefaultValue(typeof(decimal),"100")]
        public decimal Maximum { get => editor.Maximum; set => editor.Maximum=value; }
        [Category("Data"), DefaultValue(typeof(decimal),"1")]
        public decimal Increment { get => editor.Increment; set => editor.Increment=value; }
        [Category("Data"), DefaultValue(0)]
        public int DecimalPlaces { get => editor.DecimalPlaces; set {editor.DecimalPlaces=value;UpdateText();} }
        public event EventHandler ValueChanged;
        private int SpinWidth => (int)Math.Round(22 * DeviceDpi/96.0);
        public OverlayNumericUpDown()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true);
            BackColor=Color.FromArgb(237,232,218);
            ForeColor=Color.FromArgb(61,69,84);
            Size=new Size(60,30);
            editor=new NumericUpDown();
            input=new TextBox {BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Left,TabStop=false};
            editor.ValueChanged+=(s,e)=> {UpdateText();ValueChanged?.Invoke(this,e);};
            input.TextChanged+=(s,e)=>
            {
                if(!synchronizing && decimal.TryParse(input.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out decimal number) &&
                    number>=Minimum && number<=Maximum) editor.Value=number;
            };
            input.Leave+=(s,e)=>CommitText();
            input.KeyDown+=(s,e)=>
            {
                if(e.KeyCode==Keys.Up) {UpButton();e.SuppressKeyPress=true;}
                if(e.KeyCode==Keys.Down) {DownButton();e.SuppressKeyPress=true;}
                if(e.KeyCode==Keys.Enter) {CommitText();e.SuppressKeyPress=true;}
                if(e.KeyCode==Keys.Escape) {UpdateText();e.SuppressKeyPress=true;}
            };
            input.MouseWheel+=(s,e)=> {if(e.Delta>0) UpButton(); else if(e.Delta<0) DownButton();};
            Controls.Add(input);
            UpdateText();
            repeatTimer=new Timer();
            repeatTimer.Tick+=(s,e)=> {repeatTimer.Interval=70;Step();};
        }
        public void BeginInit() => ((ISupportInitialize)editor).BeginInit();
        public void EndInit() {((ISupportInitialize)editor).EndInit();UpdateText();}
        public void UpButton() {CommitText();editor.UpButton();}
        public void DownButton() {CommitText();editor.DownButton();}
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if(input==null) return;
            input.BackColor=BackColor; input.ForeColor=ForeColor;
            input.AccessibleName=AccessibleName ?? UiText.T("Значение");
            int left=(int)Math.Round(9*DeviceDpi/96.0);
            int inset=(int)Math.Round(14*DeviceDpi/96.0);
            input.SetBounds(left,Math.Max(0,(Height-input.PreferredHeight)/2),
                Math.Max(1,Width-SpinWidth-inset),input.PreferredHeight);
        }
        protected override void OnBackColorChanged(EventArgs e) {base.OnBackColorChanged(e);PerformLayout();Invalidate();}
        protected override void OnForeColorChanged(EventArgs e) {base.OnForeColorChanged(e);PerformLayout();Invalidate();}
        protected override void OnFontChanged(EventArgs e) {base.OnFontChanged(e);PerformLayout();Invalidate();}
        protected override void OnSizeChanged(EventArgs e) {base.OnSizeChanged(e);OverlayDrawing.Shape(this,CornerRadius);}
        protected override void OnEnter(EventArgs e) {base.OnEnter(e);input?.Focus();Invalidate();}
        protected override void OnLeave(EventArgs e) {StopRepeat();base.OnLeave(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            using(var shape=OverlayDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),CornerRadius*DeviceDpi/96f))
            using(var fill=new SolidBrush(BackColor)) e.Graphics.FillPath(fill,shape);
            Color ink=Enabled ? ForeColor : Color.Gray;
            var top=new Rectangle(Width-SpinWidth,0,SpinWidth,Height/2);
            var bottom=new Rectangle(Width-SpinWidth,Height/2,SpinWidth,Height-Height/2);
            OverlayDrawing.Chevron(e.Graphics,top,ink,true);
            OverlayDrawing.Chevron(e.Graphics,bottom,ink,false);
            using(var path=OverlayDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),CornerRadius*DeviceDpi/96f))
            using(var pen=new Pen(ContainsFocus ? Color.FromArgb(173,184,175) : BackColor)) e.Graphics.DrawPath(pen,path);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            input.Focus();
            if(e.Button!=MouseButtons.Left || e.X<Width-SpinWidth) return;
            Capture=true; direction=e.Y<Height/2 ? 1 : -1;
            Step();repeatTimer.Interval=400;repeatTimer.Start();
        }
        protected override void OnMouseUp(MouseEventArgs e) {StopRepeat();Capture=false;base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e) {if(!Capture) StopRepeat();base.OnMouseCaptureChanged(e);}
        private void Step() {if(direction>0) UpButton();else if(direction<0) DownButton();}
        private void StopRepeat() {direction=0;repeatTimer?.Stop();}
        protected override void Dispose(bool disposing) {if(disposing) {repeatTimer?.Dispose();editor?.Dispose();}base.Dispose(disposing);}

        private void UpdateText()
        {
            if(input==null) return;
            synchronizing=true;
            try {input.Text=editor.Value.ToString("F"+DecimalPlaces,CultureInfo.CurrentCulture);}
            finally {synchronizing=false;}
        }
        private void CommitText()
        {
            if(decimal.TryParse(input.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out decimal number))
                editor.Value=Math.Clamp(decimal.Round(number,DecimalPlaces),Minimum,Maximum);
            UpdateText();
        }
    }

    public class OverlayPanel : Panel
    {
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); OverlayDrawing.Shape(this,6); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); OverlayDrawing.Shape(this,6); }
    }
}
