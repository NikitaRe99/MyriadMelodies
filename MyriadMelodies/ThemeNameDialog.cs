using System;
using System.Drawing;
using System.Windows.Forms;
using MyriadMelodies.Controls;

namespace MyriadMelodies
{
    internal class OverlayDialog : Form
    {
        private Point dragOffset;
        private bool dragging;
        protected OverlayDialog(string title,Size size)
        {
            Text=title;ClientSize=size;Font=new Font("Segoe UI",9.5f);
            BackColor=Color.FromArgb(49,59,75);ForeColor=Color.FromArgb(237,232,218);
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterParent;
            MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;
            var caption=new Label {Text=title,Font=new Font("Segoe UI",11,FontStyle.Bold),Location=new Point(16,14),Size=new Size(size.Width-64,26)};
            caption.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){dragOffset=PointToClient(caption.PointToScreen(e.Location));dragging=true;caption.Capture=true;}};
            caption.MouseMove+=(s,e)=>{if(dragging) Location=new Point(Cursor.Position.X-dragOffset.X,Cursor.Position.Y-dragOffset.Y);};
            caption.MouseUp+=(s,e)=>{dragging=false;caption.Capture=false;};
            var close=new OverlayButton {WindowGlyph=WindowButtonGlyph.Close,AccessibleName=UiText.T("Закрыть"),Location=new Point(size.Width-40,12),Size=new Size(24,24),BackColor=BackColor,ForeColor=ForeColor,DialogResult=DialogResult.Cancel};
            Controls.Add(caption);Controls.Add(close);
        }
        protected OverlayButton AddAction(string text,DialogResult result,Rectangle bounds,bool primary)
        {
            var button=new OverlayButton {Text=text,DialogResult=result,Bounds=bounds,
                BackColor=primary?Color.FromArgb(237,232,218):Color.FromArgb(72,83,103),
                ForeColor=primary?Color.FromArgb(61,69,84):ForeColor};
            Controls.Add(button);return button;
        }
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);OverlayDrawing.Shape(this,14);}
        protected override void OnShown(EventArgs e){UiText.Apply(this);base.OnShown(e);}
    }
    internal sealed class ThemeCapturePreparationDialog : OverlayDialog
    {
        public ThemeCapturePreparationDialog(string mode):base(UiText.T("Своя тема"),new Size(360,312))
        {
            Controls.Add(new Label {Location=new Point(16,52),Size=new Size(328,168),Text=
                UiText.T("1. Перейдите в «Стили интерфейса».\n\n2. Выберите нужную вам тему.\n\n3. Убедитесь, что выбран нужный режим.\n\nПосле этого нажмите «ОК»: приложение считает цвета и предложит ввести название.")});
            Controls.Add(new Label {Location=new Point(16,230),Size=new Size(328,20),ForeColor=Color.FromArgb(184,193,201),Text=UiText.T("В приложении: ")+mode});
            CancelButton=AddAction(UiText.T("Отмена"),DialogResult.Cancel,new Rectangle(16,264,100,32),false);
            AcceptButton=AddAction(UiText.T("ОК"),DialogResult.OK,new Rectangle(232,264,112,32),true);
        }
    }
    internal sealed class ThemeNameDialog : OverlayDialog
    {
        private readonly OverlayTextBox nameTextBox;
        private readonly OverlayComboBox flashModeCombo;
        public ThemeNameDialog(Color shortPressColor,Color longPressColor):base(UiText.T("Название темы"),new Size(360,308))
        {
            Controls.Add(new Label {Location=new Point(16,52),Size=new Size(328,40),Text=
                UiText.F("Короткая нота: {0}, {1}, {2}\nДлинная нота: {3}, {4}, {5}","Short note: {0}, {1}, {2}\nLong note: {3}, {4}, {5}",shortPressColor.R,shortPressColor.G,shortPressColor.B,longPressColor.R,longPressColor.G,longPressColor.B)});
            Controls.Add(new Label {Location=new Point(16,103),Size=new Size(328,20),Text=UiText.T("Как назвать эту тему?")});
            nameTextBox=new OverlayTextBox {Location=new Point(16,129),Size=new Size(280,32),MaxLength=100,Font=Font};Controls.Add(nameTextBox);
            Controls.Add(new ColorSwatch {Location=new Point(304,137),SwatchColor=shortPressColor,AccessibleName=UiText.T("Цвет короткой ноты")});
            Controls.Add(new ColorSwatch {Location=new Point(328,137),SwatchColor=longPressColor,AccessibleName=UiText.T("Цвет длинной ноты")});
            Controls.Add(new Label {Location=new Point(16,177),Size=new Size(328,20),Text=UiText.T("Тип всполоха")});
            flashModeCombo=new OverlayComboBox {Location=new Point(16,201),Size=new Size(304,30),Font=Font};
            foreach(var mode in Enum.GetValues<ThemeFlashMode>())flashModeCombo.Items.Add(UiText.T(ColorPaletteCatalog.FlashPresetName(mode)));flashModeCombo.SelectedIndex=0;
            Controls.Add(flashModeCombo);
            var flashSwatch=new ColorSwatch {Location=new Point(328,208),AccessibleName=UiText.T("Цвет всполоха")};Controls.Add(flashSwatch);
            var flashHint=new Label {Location=new Point(16,238),Size=new Size(328,16),Font=new Font("Segoe UI",8),ForeColor=Color.FromArgb(184,193,201)};
            void UpdateFlashHint() {Color color=ColorPaletteCatalog.GetFlashColor(FlashMode);flashSwatch.SwatchColor=color;flashHint.Text=UiText.F("Цвет всполоха: #{0:X2}{1:X2}{2:X2}","Flash color: #{0:X2}{1:X2}{2:X2}",color.R,color.G,color.B);}
            flashModeCombo.SelectedIndexChanged+=(s,e)=>UpdateFlashHint();UpdateFlashHint();Controls.Add(flashHint);
            var add=AddAction(UiText.T("Добавить"),DialogResult.OK,new Rectangle(232,260,112,32),true);
            add.Enabled=false;nameTextBox.TextChanged+=(s,e)=>add.Enabled=ThemeName.Length>0;
            AcceptButton=add;CancelButton=AddAction(UiText.T("Отмена"),DialogResult.Cancel,new Rectangle(16,260,100,32),false);
        }
        public string ThemeName=>nameTextBox.Text.Trim();
        public ThemeFlashMode FlashMode=>(ThemeFlashMode)Math.Max(0,flashModeCombo.SelectedIndex);
        protected override void OnShown(EventArgs e){base.OnShown(e);nameTextBox.Focus();}
    }

    internal sealed class DeleteThemeDialog : OverlayDialog
    {
        public DeleteThemeDialog(string name) : base(UiText.T("Удалить тему?"), new Size(360,202))
        {
            Controls.Add(new Label {Location=new Point(16,56),Size=new Size(328,78),
                Text=UiText.F("Пользовательская тема «{0}» будет удалена.\n\nЧтобы вернуть её, потребуется заново считать цвета.","Custom theme “{0}” will be deleted.\n\nTo restore it, you will need to sample its colors again.",name),AutoEllipsis=true});
            CancelButton=AddAction(UiText.T("Отмена"),DialogResult.Cancel,new Rectangle(16,154,100,32),false);
            AddAction(UiText.T("Удалить"),DialogResult.OK,new Rectangle(232,154,112,32),true);
            // Enter по умолчанию отменяет действие, а удаление требует явного выбора.
            AcceptButton=CancelButton;
        }
    }
}
