using System.Drawing;
using System.Windows.Forms;
using MyriadMelodies.Controls;

namespace MyriadMelodies
{
    internal sealed class KeyBindingDialog : OverlayDialog
    {
        private readonly OverlayButton keyField;
        private readonly Label hint;
        public Keys SelectedKey {get;private set;}
        public KeyBindingDialog(int zone,Keys current) : base(UiText.F("Клавиша зоны {0}","Zone {0} key",zone),new Size(360,280))
        {
            SelectedKey=current;
            Controls.Add(new Label {Text=UiText.T("Нажмите новую клавишу на клавиатуре."),Location=new Point(16,52),Size=new Size(328,24)});
            keyField=AddAction(LaneBindings.DisplayName(current),DialogResult.None,new Rectangle(16,84,328,52),true);
            keyField.Font=new Font("Segoe UI",16,FontStyle.Bold);
            keyField.Tag="literal";
            hint=new Label {Text=UiText.T("Если клавиша занята, назначения двух зон поменяются местами. Esc — отмена."),Location=new Point(16,152),Size=new Size(328,60),ForeColor=Color.FromArgb(184,193,201)};
            Controls.Add(hint);
            var reset=AddAction(UiText.T("Сбросить"),DialogResult.None,new Rectangle(16,232,96,32),false);
            reset.Click+=(s,e)=>{SelectedKey=LaneBindings.CreateDefaults()[zone-1];keyField.Text=LaneBindings.DisplayName(SelectedKey);keyField.Focus();};
            CancelButton=AddAction(UiText.T("Отмена"),DialogResult.Cancel,new Rectangle(120,232,96,32),false);
            AddAction(UiText.T("Сохранить"),DialogResult.OK,new Rectangle(224,232,120,32),true);
        }
        protected override void OnShown(System.EventArgs e){base.OnShown(e);keyField.Focus();}
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==(Keys.Alt|Keys.F4)) return base.ProcessCmdKey(ref msg,keyData);
            if(keyData==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}
            if(keyField.Focused)
            {
                if(!LaneBindings.IsSupported(keyData)) hint.Text=UiText.T("Нажмите одну клавишу без Ctrl, Alt, Shift и Win. Esc — отмена.");
                else {SelectedKey=keyData;keyField.Text=LaneBindings.DisplayName(keyData);hint.Text=UiText.T("Если клавиша занята, назначения двух зон поменяются местами. Esc — отмена.");}
                return true;
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }
    }
}
