using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MyriadMelodies
{
    public partial class MainForm
    {
        private readonly OverlaySettingsStore settingsStore;
        private bool synchronizingStorage;
        private void InitializeStorageChoice()
        {
            synchronizingStorage=true;
            try {storageSelector.SelectedIndex=(int)settingsStore.Location;}
            finally {synchronizingStorage=false;}
        }
        private void StorageSelector_Changed(object sender,EventArgs e)
        {
            if(IsInDesigner || keyInput==null || synchronizingStorage || settingsStore==null || !settingsStore.CanChangeLocation || storageSelector.SelectedIndex<0)return;
            if(screenCapture.IsRunning || screenCapture.IsRecovering || System.Threading.Volatile.Read(ref customColorSamplingInProgress)!=0)
            {InitializeStorageChoice();return;}
            try
            {
                settingsStore.ChangeLocation((SettingsLocation)storageSelector.SelectedIndex,
                    CapturePreferences(),ColorPaletteCatalog.SaveSnapshot);
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
            {
                InitializeStorageChoice();
                OverlayMessageDialog.ShowError(this,UiText.T("Не удалось перенести настройки"),
                    new IOException(UiText.T("Не удалось записать настройки. Выберите папку с правом записи или используйте LocalAppData."),exception));
            }
        }
        private OverlayPreferences CapturePreferences()
        {
            Point location=WindowState==FormWindowState.Normal?Location:RestoreBounds.Location;
            return new OverlayPreferences
            {
                MonitorDeviceName=(monitorSelector.SelectedItem as MonitorOption)?.DeviceName,
                WindowProcessName=SelectedGameWindow?.ProcessName,
                WindowTitle=SelectedGameWindow?.Title,
                ModeIndex=domainUpDownMode.SelectedIndex,
                ThemeName=(comboStyle.SelectedItem as ColorPaletteStyle)?.Name,
                Language=UiText.English?"en":"ru",
                ColorTolerance=(int)colorToleranceUpDown.Value,
                OpacityPercent=(int)opacityUpDown.Value,
                TopMost=pinCheckBox.Checked,
                SettingsExpanded=settingsExpanded,
                PreviewVisible=preview,
                ZoneKeys=Array.ConvertAll(AreaKeys,key=>(int)key),
                WindowLeft=location.X,WindowTop=location.Y
            };
        }
        private void RestoreOverlayPreferences(OverlayPreferences saved)
        {
            int[] savedKeys=LaneBindings.Normalize(saved.ZoneKeys);
            for(int i=0;i<AreaKeys.Length;i++) AreaKeys[i]=(Keys)savedKeys[i];
            for(int i=0;i<monitorSelector.Items.Count;i++)
                if(monitorSelector.Items[i] is MonitorOption monitor && monitor.DeviceName==saved.MonitorDeviceName)
                {monitorSelector.SelectedIndex=i;break;}
            if(!string.IsNullOrEmpty(saved.WindowProcessName))
            {
                int fallback=-1;
                for(int i=0;i<monitorSelector.Items.Count;i++)
                    if(monitorSelector.Items[i] is WindowCaptureTarget window && string.Equals(window.ProcessName,saved.WindowProcessName,StringComparison.OrdinalIgnoreCase))
                    {
                        fallback=i;
                        if(window.Title==saved.WindowTitle) break;
                    }
                if(fallback>=0) monitorSelector.SelectedIndex=fallback;
            }
            domainUpDownMode.SelectedIndex=saved.ModeIndex;
            for(int i=0;i<comboStyle.Items.Count;i++)
                if(comboStyle.Items[i] is ColorPaletteStyle palette && string.Equals(palette.Name,saved.ThemeName,StringComparison.Ordinal))
                {comboStyle.SelectedIndex=i;break;}
            colorToleranceUpDown.Value=saved.ColorTolerance;
            opacityUpDown.Value=saved.OpacityPercent;
            pinCheckBox.Checked=saved.TopMost;
            settingsExpanded=saved.SettingsExpanded;
            if(saved.PreviewVisible && !preview) BtnShow_Click(this,EventArgs.Empty);
            UpdateOverlayLayout();
            if(saved.WindowLeft.HasValue && saved.WindowTop.HasValue)
            {
                StartPosition=FormStartPosition.Manual;
                Rectangle work=Screen.FromRectangle(new Rectangle(saved.WindowLeft.Value,saved.WindowTop.Value,Width,Height)).WorkingArea;
                Location=new Point(Math.Clamp(saved.WindowLeft.Value,work.Left,Math.Max(work.Left,work.Right-Width)),
                    Math.Clamp(saved.WindowTop.Value,work.Top,Math.Max(work.Top,work.Bottom-Height)));
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if(e.Cancel || IsInDesigner || settingsStore==null) return;
            try
            {
                settingsStore.Save(CapturePreferences());
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
            {
                MessageBox.Show(this,UiText.F("Не удалось сохранить настройки: {0}","Could not save settings: {0}",exception.Message),UiText.T("Настройки"),
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        }
    }
}
