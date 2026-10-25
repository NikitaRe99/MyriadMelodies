using System;
using System.Windows.Forms;

namespace MyriadMelodies
{
    public partial class MainForm
    {
        private void LanguageSelector_Changed(object sender,EventArgs e)
        {
            UiText.SetLanguage(languageSelector.SelectedIndex==1?"en":"ru");
            ApplyLanguage();
        }
        private void ApplyLanguage()
        {
            UiText.Apply(this,overlayToolTip);
            Text=UiText.English?"Repertoire of Myriad Melodies":"Мириад мелодий";
            foreach(var area in captureAreas)area.SetKeyName(LaneBindings.DisplayName(AreaKeys[area.AreaNumber-1]));
            UpdateCaptureState();RecalculateCaptureRegions();
            comboStyle.Invalidate();monitorSelector.Invalidate();domainUpDownMode.Invalidate();
        }
    }
}
