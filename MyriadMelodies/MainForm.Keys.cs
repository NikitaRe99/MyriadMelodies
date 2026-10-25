using System;
using System.Threading;
using System.Windows.Forms;

namespace MyriadMelodies
{
    public partial class MainForm
    {
        private void EditAreaKey(int index)
        {
            if(screenCapture.IsRunning || screenCapture.IsRecovering || Volatile.Read(ref customColorSamplingInProgress)!=0 || index<0 || index>=AreaKeys.Length) return;
            using var dialog=new KeyBindingDialog(index+1,AreaKeys[index]);
            if(dialog.ShowDialog(this)==DialogResult.OK) ApplyAreaKey(index,dialog.SelectedKey);
        }
        private void ApplyAreaKey(int index,Keys key)
        {
            if(screenCapture.IsRunning || screenCapture.IsRecovering || Volatile.Read(ref customColorSamplingInProgress)!=0 || index<0 || index>=AreaKeys.Length || !LaneBindings.IsSupported(key)) return;
            lock(inputProcessingSync)
            {
                keyInput.ReleaseAll();
                int other=Array.IndexOf(AreaKeys,key);
                if(other>=0 && other!=index) AreaKeys[other]=AreaKeys[index];
                AreaKeys[index]=key;
                ResetShortPressLatches();
            }
            RefreshAreaKeyLabels();
        }
        private void BtnResetKeys_Click(object sender,EventArgs e)
        {
            if(screenCapture.IsRunning || screenCapture.IsRecovering || Volatile.Read(ref customColorSamplingInProgress)!=0) return;
            lock(inputProcessingSync)
            {
                keyInput.ReleaseAll();LaneBindings.CreateDefaults().CopyTo(AreaKeys,0);ResetShortPressLatches();
            }
            RefreshAreaKeyLabels();
        }
        private void RefreshAreaKeyLabels()
        {
            foreach(var area in captureAreas) area.SetKeyName(LaneBindings.DisplayName(AreaKeys[area.AreaNumber-1]));
        }
    }
}
