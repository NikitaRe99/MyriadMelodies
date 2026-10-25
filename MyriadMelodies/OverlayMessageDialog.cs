using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyriadMelodies
{
    internal sealed class OverlayMessageDialog : OverlayDialog
    {
        public OverlayMessageDialog(string title,string message,string details=null) : base(title,new Size(380,220))
        {
            message=UiText.T(message);
            int bodyHeight=Math.Clamp(TextRenderer.MeasureText(message,Font,new Size(348,0),TextFormatFlags.WordBreak).Height+12,76,240);
            ClientSize=new Size(380,bodyHeight+116);
            Controls.Add(new Label {Name="messageLabel",Text=message,Location=new Point(16,54),Size=new Size(348,bodyHeight)});
            int y=ClientSize.Height-48;
            if(!string.IsNullOrEmpty(details))
            {
                var copy=AddAction(UiText.T("Копировать детали"),DialogResult.None,new Rectangle(16,y,172,32),false);
                copy.Click+=(s,e)=>
                {
                    try {Clipboard.SetText(details);copy.Text=UiText.T("Скопировано");}
                    catch(System.Runtime.InteropServices.ExternalException){copy.Text=UiText.T("Буфер занят");}
                };
            }
            AcceptButton=AddAction(UiText.T("ОК"),DialogResult.OK,new Rectangle(252,y,112,32),true);
            CancelButton=AcceptButton;
        }
        public static void ShowError(IWin32Window owner,string title,Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            using var dialog=new OverlayMessageDialog(title,exception.Message,exception is CaptureSourceUnavailableException ? null : exception.ToString());
            dialog.ShowDialog(owner);
        }
    }

    internal sealed class CaptureSourceUnavailableException : InvalidOperationException
    {
        public CaptureSourceUnavailableException(string message) : base(message) { }
    }
}
