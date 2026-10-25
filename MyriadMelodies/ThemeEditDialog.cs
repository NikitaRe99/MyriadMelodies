using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyriadMelodies.Controls;

namespace MyriadMelodies
{
    internal sealed class ThemeEditDialog : OverlayDialog
    {
        private readonly OverlayTextBox nameInput;
        private readonly OverlayComboBox flashModeCombo;
        private readonly Color originalFlash;
        private readonly bool originalInput;
        private readonly string originalName;
        private readonly string displayName;
        private static readonly ThemeFlashMode[] FlashModes=Enum.GetValues<ThemeFlashMode>();
        private readonly int originalPreset;
        private bool recapturing;
        public string ThemeName => nameInput.Text.Trim()==displayName ? originalName : nameInput.Text.Trim();
        public Color ShortColor { get; private set; }
        public Color LongColor { get; private set; }
        private int CurrentColorIndex=>FlashModes.Length;
        public Color FlashColor => flashModeCombo.SelectedIndex==CurrentColorIndex ? originalFlash
            : ColorPaletteCatalog.GetFlashColor((ThemeFlashMode)Math.Max(0,flashModeCombo.SelectedIndex));
        public bool UsesStandardInput => flashModeCombo.SelectedIndex==CurrentColorIndex || flashModeCombo.SelectedIndex==originalPreset ? originalInput : flashModeCombo.SelectedIndex==1;

        public ThemeEditDialog(ColorPaletteStyle style) : this(style,null) { }
        public ThemeEditDialog(ColorPaletteStyle style, Func<IWin32Window,Task<(Color Short,Color Long)?>> sample)
            : base(UiText.T("Редактирование темы"), new Size(360,308))
        {
            ShortColor=style.ShortPressColor;LongColor=style.LongPressColor;originalFlash=style.LongPressFlashColor;
            originalInput=style.UsesStandardInput;
            originalName=style.Name;displayName=ColorPaletteCatalog.GetDisplayName(style);
            Controls.Add(new Label {Text=UiText.T("Название темы"),Location=new Point(16,52),Size=new Size(328,20)});
            nameInput=new OverlayTextBox {Text=displayName,MaxLength=100,Location=new Point(16,76),Size=new Size(280,32)};
            Controls.Add(nameInput);
            var shortSwatch=new ColorSwatch {SwatchColor=ShortColor,Location=new Point(304,84),AccessibleName=UiText.T("Цвет короткой ноты")};
            var longSwatch=new ColorSwatch {SwatchColor=LongColor,Location=new Point(328,84),AccessibleName=UiText.T("Цвет длинной ноты")};
            Controls.Add(shortSwatch);Controls.Add(longSwatch);
            var recapture=AddAction(UiText.T("Считать цвета из игры"),DialogResult.None,new Rectangle(16,122,328,32),false);
            recapture.Name="recaptureButton";
            recapture.Enabled=sample!=null;
            recapture.Click+=async(s,e)=>
            {
                recapturing=true;Enabled=false;
                try
                {
                    var colors=await sample(this);
                    if(colors.HasValue)
                    {
                        ShortColor=colors.Value.Short;LongColor=colors.Value.Long;
                        shortSwatch.SwatchColor=ShortColor;longSwatch.SwatchColor=LongColor;
                    }
                }
                finally {recapturing=false;if(!IsDisposed) Enabled=true;}
            };
            Controls.Add(new Label {Text=UiText.T("Тип всполоха"),Location=new Point(16,174),Size=new Size(328,20)});
            flashModeCombo=new OverlayComboBox {Location=new Point(16,198),Size=new Size(304,30)};
            foreach(var mode in FlashModes)flashModeCombo.Items.Add(UiText.T(ColorPaletteCatalog.FlashPresetName(mode)));
            flashModeCombo.Items.Add(UiText.T("Текущий цвет"));
            int preset=CurrentColorIndex;
            for(int i=0;i<CurrentColorIndex;i++) if(ColorPaletteCatalog.GetFlashColor((ThemeFlashMode)i).ToArgb()==originalFlash.ToArgb()) preset=i;
            originalPreset=preset;
            flashModeCombo.SelectedIndex=preset;Controls.Add(flashModeCombo);
            var flashSwatch=new ColorSwatch {SwatchColor=FlashColor,Location=new Point(328,205),AccessibleName=UiText.T("Цвет всполоха")};Controls.Add(flashSwatch);
            flashModeCombo.SelectedIndexChanged+=(s,e)=>flashSwatch.SwatchColor=FlashColor;
            AddAction(style.IsCustom?UiText.T("Удалить"):UiText.T("Сбросить"),style.IsCustom?DialogResult.Retry:DialogResult.Ignore,new Rectangle(16,260,96,32),false);
            CancelButton=AddAction(UiText.T("Отмена"),DialogResult.Cancel,new Rectangle(120,260,96,32),false);
            var save=AddAction(UiText.T("Сохранить"),DialogResult.OK,new Rectangle(224,260,120,32),true);
            AcceptButton=save;
            nameInput.TextChanged+=(s,e)=>save.Enabled=ThemeName.Length>0;
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(recapturing && e.CloseReason==CloseReason.UserClosing) e.Cancel=true;
            base.OnFormClosing(e);
        }
    }
}
