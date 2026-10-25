using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyriadMelodies.Controls
{
    public sealed class OverlayItemActionEventArgs : EventArgs
    {
        public int Index { get; }
        public OverlayItemActionEventArgs(int index) { Index = index; }
    }
    public sealed class OverlayItemMoveEventArgs : EventArgs
    {
        public int From {get;} public int To {get;}
        public OverlayItemMoveEventArgs(int from,int to){From=from;To=to;}
    }
    // Полностью рисуемое поле: без нативных ComboBox/ListBox, белой рамки
    // и системной полосы прокрутки. Один буфер на поле и один на меню.
    [DefaultEvent("SelectedIndexChanged"), DefaultProperty("Items")]
    public class OverlayComboBox : Control
    {
        internal static readonly Color MenuColor=Color.FromArgb(72,83,103);
        internal static readonly Color MenuHighlight=Color.FromArgb(96,108,128);
        internal static readonly Color Cream=Color.FromArgb(237,232,218);
        private int selectedIndex=-1;
        private int cornerRadius=15;
        private ToolStripDropDown popup;
        private PopupList popupList;
        private long suppressOpenUntil;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public ObjectCollection Items { get; }
        [DefaultValue(-1)]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if(value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value));
                if(selectedIndex==value) return;
                selectedIndex=value;
                base.Text=GetItemText(SelectedItem);
                Invalidate();
                SelectedIndexChanged?.Invoke(this,EventArgs.Empty);
                AccessibilityNotifyClients(AccessibleEvents.ValueChange,-1);
            }
        }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SelectedItem { get => selectedIndex>=0 && selectedIndex<Items.Count ? Items[selectedIndex] : null; set => SelectedIndex=Items.IndexOf(value); }
        [Category("Appearance"), DefaultValue(15)]
        public int CornerRadius { get=>cornerRadius; set {cornerRadius=Math.Clamp(value,0,24);Invalidate();} }
        [DefaultValue(22)] public int ItemHeight {get;set;}=22;
        [DefaultValue(0)] public int DropDownWidth {get;set;}
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DroppedDown {get=>popup!=null;set {if(value) ShowPopup();else popup?.Close();} }
        public event EventHandler SelectedIndexChanged;
        public event EventHandler DropDown;
        public event EventHandler DropDownClosed;
        public event DrawItemEventHandler DrawItem;
        public event EventHandler<OverlayItemActionEventArgs> ItemAction;
        public event EventHandler<OverlayItemMoveEventArgs> ItemMoveRequested;
        [DefaultValue(false)] public bool AllowItemReorder {get;set;}
        public void MoveItem(int from,int to)
        {
            object selected=SelectedItem;Items.MoveItemCore(from,to);
            selectedIndex=Items.IndexOf(selected);base.Text=GetItemText(selected);
            if(popupList!=null){popupList.SelectedIndex=to;popupList.Invalidate();}Invalidate();
        }
        private void RequestMove(int from,int to)
        {if(AllowItemReorder && from>=0 && to>=0 && from<Items.Count && to<Items.Count && from!=to)ItemMoveRequested?.Invoke(this,new OverlayItemMoveEventArgs(from,to));}
        public OverlayComboBox()
        {
            Items=new ObjectCollection(this);
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
            BackColor=Cream;ForeColor=Color.FromArgb(61,69,84);Size=new Size(240,30);
            Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.ComboBox;
        }
        public string GetItemText(object item)=>item?.ToString() ?? string.Empty;
        public void BeginUpdate()=>SuspendLayout();
        public void EndUpdate(){ResumeLayout(false);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? MenuColor);
            using(var path=OverlayDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),CornerRadius*DeviceDpi/96f))
            using(var fill=new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(fill,path);
                var saved=e.Graphics.Save();e.Graphics.SetClip(path,CombineMode.Intersect);
                int arrow=(int)Math.Round(26*DeviceDpi/96.0);
                var bounds=new Rectangle(3,0,Math.Max(1,Width-arrow-3),Height);
                PaintItem(new DrawItemEventArgs(e.Graphics,Font,bounds,SelectedIndex,DrawItemState.ComboBoxEdit));
                OverlayDrawing.Chevron(e.Graphics,new Rectangle(Width-arrow,0,arrow-4,Height),Enabled?ForeColor:Color.Gray,DroppedDown);
                e.Graphics.Restore(saved);
                if(Focused && ShowFocusCues)
                    using(var pen=new Pen(Color.FromArgb(173,184,175))) e.Graphics.DrawPath(pen,path);
            }
        }
        internal void PaintItem(DrawItemEventArgs e)
        {
            bool edit=(e.State & DrawItemState.ComboBoxEdit)!=0;
            bool selected=(e.State & DrawItemState.Selected)!=0;
            Color bg=edit?BackColor:selected?MenuHighlight:MenuColor;
            Color fg=Enabled?(edit?ForeColor:Cream):Color.Gray;
            var args=new DrawItemEventArgs(e.Graphics,Font,e.Bounds,e.Index,DrawItemState.None,fg,bg);
            if(DrawItem!=null) {DrawItem(this,args);return;}
            args.DrawBackground();
            var text=e.Bounds;text.X+=10;text.Width=Math.Max(0,text.Width-14);
            TextRenderer.DrawText(e.Graphics,e.Index>=0?GetItemText(Items[e.Index]):Text,Font,text,fg,
                TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(e.Button!=MouseButtons.Left || e.Clicks>1) return;
            Focus();
            // AutoClose сначала закрывает меню, затем доставляет щелчок полю.
            // Не открываем его повторно этим же щелчком.
            if(popup==null && Environment.TickCount64<suppressOpenUntil) {suppressOpenUntil=0;return;}
            DroppedDown=!DroppedDown;
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key=keyData&Keys.KeyCode;
            return key==Keys.Down||key==Keys.Up||key==Keys.Home||key==Keys.End||base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(AllowItemReorder && e.Alt && (e.KeyCode==Keys.Up || e.KeyCode==Keys.Down))
            {RequestMove(SelectedIndex,SelectedIndex+(e.KeyCode==Keys.Up?-1:1));e.SuppressKeyPress=true;return;}
            if(e.KeyCode==Keys.F2 && ItemAction!=null){InvokeItemAction(SelectedIndex);e.SuppressKeyPress=true;return;}
            if(e.KeyCode==Keys.F4||e.KeyCode==Keys.Space||(e.Alt&&e.KeyCode==Keys.Down))
            {DroppedDown=!DroppedDown;e.SuppressKeyPress=true;return;}
            if(Items.Count>0)
            {
                if(e.KeyCode==Keys.Down) {SelectedIndex=Math.Min(Items.Count-1,SelectedIndex+1);e.Handled=true;}
                if(e.KeyCode==Keys.Up) {SelectedIndex=Math.Max(0,SelectedIndex-1);e.Handled=true;}
                if(e.KeyCode==Keys.Home) {SelectedIndex=0;e.Handled=true;}
                if(e.KeyCode==Keys.End) {SelectedIndex=Items.Count-1;e.Handled=true;}
            }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        private void ShowPopup()
        {
            if(popup!=null||!Enabled||Items.Count==0) return;
            DropDown?.Invoke(this,EventArgs.Empty);
            if(Items.Count==0) return;
            int row=Math.Max(Font.Height+14,(int)Math.Round(30*DeviceDpi/96.0));
            Rectangle work=Screen.FromControl(this).WorkingArea;
            int width=Math.Min(Math.Max(Width,DropDownWidth),work.Width-16);
            int rows=Math.Min(8,Items.Count);
            popupList=new PopupList(this,row) {Font=Font,Size=new Size(width-16,rows*row),BackColor=MenuColor,ForeColor=Cream};
            popupList.SelectedIndex=SelectedIndex;
            var host=new ToolStripControlHost(popupList){AutoSize=false,Size=popupList.Size,Margin=Padding.Empty,Padding=Padding.Empty};
            popup=new ToolStripDropDown {AutoClose=true,AutoSize=true,BackColor=MenuColor,Padding=new Padding(8,6,8,6),
                Margin=Padding.Empty,DropShadowEnabled=false,Renderer=new PopupRenderer()};
            popup.Items.Add(host);
            var current=popup;
            popup.Opened+=(s,e)=>{OverlayDrawing.Shape(current,16);popupList.Focus();};
            popup.Closing+=(s,e)=>
            {
                if(e.CloseReason==ToolStripDropDownCloseReason.AppClicked && RectangleToScreen(ClientRectangle).Contains(Cursor.Position))
                    suppressOpenUntil=Environment.TickCount64+350;
            };
            popup.Closed+=(s,e)=>
            {
                popup=null;popupList=null;DropDownClosed?.Invoke(this,EventArgs.Empty);Invalidate();
                current.BeginInvoke((MethodInvoker)(()=>current.Dispose()));
            };
            popup.Show(this,new Point(0,Height+6),ToolStripDropDownDirection.BelowRight);
            Invalidate();
        }
        private void CommitSelection(int index)
        {
            if(index>=0&&index<Items.Count) SelectedIndex=index;
            popup?.Close(ToolStripDropDownCloseReason.ItemClicked);
        }
        private void InvokeItemAction(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            popup?.Close();
            BeginInvoke((MethodInvoker)(() => ItemAction?.Invoke(this, new OverlayItemActionEventArgs(index))));
        }
        protected override void Dispose(bool disposing){if(disposing) popup?.Close();base.Dispose(disposing);}
        protected override AccessibleObject CreateAccessibilityInstance()=>new ComboAccessible(this);
        private sealed class ComboAccessible : ControlAccessibleObject
        {
            private readonly OverlayComboBox owner;
            public ComboAccessible(OverlayComboBox owner):base(owner){this.owner=owner;}
            public override string Value {get=>owner.GetItemText(owner.SelectedItem);set{}}
            public override string DefaultAction=>owner.DroppedDown?UiText.T("Закрыть"):UiText.T("Открыть");
            public override AccessibleStates State=>base.State|AccessibleStates.Focusable|(owner.DroppedDown?AccessibleStates.Expanded:AccessibleStates.Collapsed);
            public override void DoDefaultAction()=>owner.DroppedDown=!owner.DroppedDown;
        }
        public sealed class ObjectCollection : Collection<object>
        {
            private readonly OverlayComboBox owner;
            internal ObjectCollection(OverlayComboBox owner){this.owner=owner;}
            public void AddRange(object[] items){foreach(object item in items) Add(item);}
            internal void MoveItemCore(int from,int to)
            {
                if(from<0 || from>=Count)throw new ArgumentOutOfRangeException(nameof(from));
                if(to<0 || to>=Count)throw new ArgumentOutOfRangeException(nameof(to));
                object item=this[from];base.RemoveItem(from);base.InsertItem(to,item);
            }
            protected override void SetItem(int index,object item)
            {
                base.SetItem(index,item);
                if(index==owner.SelectedIndex)owner.Text=owner.GetItemText(item);
                owner.Invalidate();
            }
            protected override void InsertItem(int index,object item)
            {
                int selected=owner.SelectedIndex;
                base.InsertItem(index,item);
                if(selected>=index)owner.selectedIndex=selected+1;
                owner.Invalidate();
            }
            protected override void ClearItems(){base.ClearItems();owner.SelectedIndex=-1;owner.Invalidate();}
            protected override void RemoveItem(int index)
            {
                int selected=owner.SelectedIndex;base.RemoveItem(index);
                if(selected>=Count) owner.SelectedIndex=Count-1;
                else if(index<=selected) owner.SelectedIndex=selected-1;
                owner.Invalidate();
            }
        }
        private sealed class PopupRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(MenuColor);}
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}
        }
        private sealed class PopupList : Control
        {
            private readonly OverlayComboBox owner;
            private readonly int rowHeight;
            private int selected=-1,first,hovered=-1;
            private bool draggingScroll;
            private int dragOffset;
            private int pressedIndex=-1;
            private Point pressedPoint;
            private bool movingItem;
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public int SelectedIndex {get=>selected;set{selected=Math.Clamp(value,-1,owner.Items.Count-1);EnsureVisible();Invalidate();}}
            private int VisibleRows=>Math.Max(1,Height/rowHeight);
            private int MaxFirst=>Math.Max(0,owner.Items.Count-VisibleRows);
            private Rectangle Track=>new Rectangle(Width-5,4,3,Math.Max(1,Height-8));
            private Rectangle Thumb
            {
                get
                {
                    var t=Track;int height=Math.Max(20,t.Height*VisibleRows/Math.Max(1,owner.Items.Count));
                    return new Rectangle(t.X,t.Y+(MaxFirst==0?0:(t.Height-height)*first/MaxFirst),t.Width,height);
                }
            }
            public PopupList(OverlayComboBox owner,int rowHeight)
            {
                this.owner=owner;this.rowHeight=rowHeight;TabStop=true;AccessibleRole=AccessibleRole.List;
                SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            }
            private void EnsureVisible(){if(selected<first) first=Math.Max(0,selected);if(selected>=first+VisibleRows) first=selected-VisibleRows+1;first=Math.Clamp(first,0,MaxFirst);}
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(MenuColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                for(int row=0;row<VisibleRows && first+row<owner.Items.Count;row++)
                {
                    int index=first+row;bool highlight=index==(hovered>=0?hovered:selected);
                    var bounds=new Rectangle(0,row*rowHeight,Width-(MaxFirst>0?10:0),rowHeight);bounds.Inflate(-1,-1);
                    using(var path=OverlayDrawing.Rounded(bounds,bounds.Height/2f))
                    using(var fill=new SolidBrush(highlight?MenuHighlight:MenuColor))
                    {
                        e.Graphics.FillPath(fill,path);var saved=e.Graphics.Save();e.Graphics.SetClip(path,CombineMode.Intersect);
                        var content=bounds;content.Width-=30;
                        if (owner.ItemAction != null)
                        {
                            int actionWidth = (int)Math.Round(28 * DeviceDpi / 96f);
                            content.X += actionWidth; content.Width -= actionWidth;
                            float cx=bounds.Left+actionWidth/2f, cy=bounds.Top+bounds.Height/2f;
                            using(var pen=new Pen(Cream,1.5f))
                            {
                                e.Graphics.DrawPolygon(pen,new[]{new PointF(cx-5,cy+2),new PointF(cx+3,cy-6),new PointF(cx+6,cy-3),new PointF(cx-2,cy+5),new PointF(cx-6,cy+6)});
                                e.Graphics.DrawLine(pen,cx+1,cy-4,cx+4,cy-1);
                            }
                        }
                        owner.PaintItem(new DrawItemEventArgs(e.Graphics,Font,content,index,highlight?DrawItemState.Selected:DrawItemState.None));
                        if(index==owner.SelectedIndex)
                        {
                            float x=bounds.Right-21,y=bounds.Top+bounds.Height/2f;
                            using(var pen=new Pen(Cream,2.4f)){pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLines(pen,new[]{new PointF(x-4,y),new PointF(x,y+4),new PointF(x+8,y-5)});}
                        }
                        e.Graphics.Restore(saved);
                    }
                }
                if(MaxFirst>0)
                {
                    using(var fill=new SolidBrush(Color.FromArgb(126,141,161)))
                    using(var path=OverlayDrawing.Rounded(Thumb,2)) e.Graphics.FillPath(fill,path);
                }
                if(movingItem && hovered>=first && hovered<first+VisibleRows)
                    using(var pen=new Pen(Cream,2))e.Graphics.DrawLine(pen,10,(hovered-first)*rowHeight+1,Width-15,(hovered-first)*rowHeight+1);
            }
            private int IndexAt(Point p){int index=first+p.Y/rowHeight;return p.Y>=0&&p.Y<Height&&index<owner.Items.Count?index:-1;}
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if(e.Button!=MouseButtons.Left) return;
                if(MaxFirst>0&&e.X>=Width-12)
                {
                    draggingScroll=true;Capture=true;dragOffset=Thumb.Contains(e.Location)?e.Y-Thumb.Y:Thumb.Height/2;ScrollTo(e.Y);return;
                }
                int index=IndexAt(e.Location);
                if(owner.ItemAction!=null && e.X<28*DeviceDpi/96f) owner.InvokeItemAction(index);
                else if(owner.AllowItemReorder){pressedIndex=index;pressedPoint=e.Location;Capture=true;}
                else owner.CommitSelection(index);
            }
            private void ScrollTo(int y){var track=Track;var thumb=Thumb;first=Math.Clamp((y-track.Y-dragOffset)*MaxFirst/Math.Max(1,track.Height-thumb.Height),0,MaxFirst);Invalidate();}
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);if(draggingScroll){ScrollTo(e.Y);return;}
                int previousFirst=first;
                if(pressedIndex>=0 && e.Button==MouseButtons.Left)
                {
                    if(Math.Abs(e.X-pressedPoint.X)>SystemInformation.DragSize.Width/2 || Math.Abs(e.Y-pressedPoint.Y)>SystemInformation.DragSize.Height/2) movingItem=true;
                    if(movingItem && e.Y<rowHeight/2) first=Math.Max(0,first-1);
                    if(movingItem && e.Y>Height-rowHeight/2) first=Math.Min(MaxFirst,first+1);
                }
                int next=e.X<Width-10?IndexAt(e.Location):-1;if(next!=hovered || previousFirst!=first || movingItem){hovered=next;Invalidate();}
            }
            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                if(!Capture){pressedIndex=-1;movingItem=false;draggingScroll=false;Invalidate();}
                base.OnMouseCaptureChanged(e);
            }
            protected override void OnMouseUp(MouseEventArgs e)
            {
                int from=pressedIndex,to=IndexAt(e.Location);bool move=movingItem;
                pressedIndex=-1;movingItem=false;draggingScroll=false;Capture=false;base.OnMouseUp(e);
                if(from>=0){if(move)owner.RequestMove(from,to);else if(from==to)owner.CommitSelection(to);}
                Invalidate();
            }
            protected override void OnMouseLeave(EventArgs e){hovered=-1;Invalidate();base.OnMouseLeave(e);}
            protected override void OnMouseWheel(MouseEventArgs e){first=Math.Clamp(first-(e.Delta/120)*3,0,MaxFirst);hovered=-1;Invalidate();base.OnMouseWheel(e);}
            protected override bool IsInputKey(Keys keyData)=>true;
            protected override void OnKeyDown(KeyEventArgs e)
            {
                hovered=-1;
                if(owner.AllowItemReorder && e.Alt && (e.KeyCode==Keys.Up || e.KeyCode==Keys.Down))
                {owner.RequestMove(selected,selected+(e.KeyCode==Keys.Up?-1:1));e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.F2 && owner.ItemAction!=null){owner.InvokeItemAction(selected);e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.Enter){owner.CommitSelection(selected);e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.Escape||e.KeyCode==Keys.F4||(e.Alt&&e.KeyCode==Keys.Up)){owner.popup?.Close();e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.Tab){owner.popup?.Close();owner.Parent?.SelectNextControl(owner,!e.Shift,true,true,true);e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.Down) SelectedIndex=Math.Min(owner.Items.Count-1,selected+1);
                if(e.KeyCode==Keys.Up) SelectedIndex=Math.Max(0,selected-1);
                if(e.KeyCode==Keys.Home) SelectedIndex=0;
                if(e.KeyCode==Keys.End) SelectedIndex=owner.Items.Count-1;
                if(e.KeyCode==Keys.PageDown) SelectedIndex=Math.Min(owner.Items.Count-1,selected+VisibleRows);
                if(e.KeyCode==Keys.PageUp) SelectedIndex=Math.Max(0,selected-VisibleRows);
                e.Handled=true;base.OnKeyDown(e);
            }
        }
    }
}
