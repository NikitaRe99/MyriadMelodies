using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MyriadMelodies;
using MyriadMelodies.Controls;

class WindowChecks
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly Assembly AppAssembly=typeof(MainForm).Assembly;
 static readonly Color[] Colors={Color.FromArgb(210,40,60),Color.FromArgb(40,190,70),Color.FromArgb(50,70,220),Color.FromArgb(220,170,40),Color.FromArgb(190,60,180),Color.FromArgb(30,180,200)};
 static Rectangle[] Regions(Size size)=>(Rectangle[])AppAssembly.GetType("MyriadMelodies.CaptureLayout").GetMethod("CreateRegions").Invoke(null,new object[]{new Rectangle(Point.Empty,size),false});
 sealed class Target : Form
 {
  public int Phase;
  public Target(){Text="Window capture test";ClientSize=new Size(960,540);StartPosition=FormStartPosition.Manual;Location=new Point(80,80);DoubleBuffered=true;}
  protected override void OnPaint(PaintEventArgs e)
  {
   e.Graphics.Clear(Color.FromArgb(25,30,35));
   var regions=Regions(ClientSize);
   for(int i=0;i<6;i++){using var brush=new SolidBrush(Colors[(i+Phase)%6]);var rect=regions[i];rect.Inflate(3,3);e.Graphics.FillRectangle(brush,rect);}
  }
 }
 static T Field<T>(MainForm form,string name)=>(T)typeof(MainForm).GetField(name,Private).GetValue(form);
 static void Require(bool condition,string message){if(!condition) throw new Exception(message);}
 static void Wait(Func<bool> ready,string message)
 {
  var end=DateTime.UtcNow.AddSeconds(6);
  while(!ready() && DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(15);}
  Require(ready(),message);
 }
 [STAThread] static int Main()
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  try
  {
   string path=Path.Combine(Directory.GetCurrentDirectory(),"artifacts/tests/window-"+Guid.NewGuid().ToString("N")+".json");
   object store=Activator.CreateInstance(AppAssembly.GetType("MyriadMelodies.OverlaySettingsStore"),new object[]{path});
   using var main=(MainForm)Activator.CreateInstance(typeof(MainForm),Private,null,new[]{store},null);
   using var target=new Target();target.Show();main.TopMost=false;main.Show();Application.DoEvents();
   object option=Activator.CreateInstance(AppAssembly.GetType("MyriadMelodies.WindowCaptureTarget"),new object[]{target.Handle,Environment.ProcessId,"GenshinImpact",target.Text});
   var selector=Field<OverlayComboBox>(main,"monitorSelector");selector.Items.Add(option);selector.SelectedItem=option;
   typeof(MainForm).GetField("suppressKeyInput",Private).SetValue(main,1);
   var capture=Field<GpuScreenCapture>(main,"screenCapture");
   var failureCallback=(EventHandler<CaptureFailedEventArgs>)Delegate.CreateDelegate(typeof(EventHandler<CaptureFailedEventArgs>),main,typeof(MainForm).GetMethod("ScreenCapture_CaptureFailed",Private));
   capture.CaptureFailed-=failureCallback;
   Color[] latest=null;Exception failure=null;
   capture.CaptureFailed+=(s,e)=>failure=e.Exception;
   capture.FrameCaptured+=(s,e)=>{var copy=new Color[6];for(int i=0;i<6;i++)copy[i]=e.Colors[i];Volatile.Write(ref latest,copy);};
   bool Matches(int phase)
   {
    if(failure!=null) throw failure;
    var values=Volatile.Read(ref latest);if(values==null)return false;
    for(int i=0;i<6;i++){var actual=values[i];var wanted=Colors[(i+phase)%6];if(Math.Abs(actual.R-wanted.R)>3||Math.Abs(actual.G-wanted.G)>3||Math.Abs(actual.B-wanted.B)>3)return false;}
    return true;
   }
   Color startColor=Field<Button>(main,"btnStartStop").BackColor;
   void Start()
   {
    typeof(MainForm).GetMethod("StartGpuCapture",Private).Invoke(main,new object[]{false});
    typeof(MainForm).GetMethod("UpdateCaptureState",Private).Invoke(main,null);
    Require(Field<Button>(main,"btnStartStop").BackColor==Color.FromArgb(218,153,141),"Running capture did not show the red Stop button");
   }
   Start();Wait(()=>Matches(0),"Window pixels/client offset mismatch");
   Require(!selector.Enabled && !Field<OverlayComboBox>(main,"domainUpDownMode").Enabled && !Field<OverlayComboBox>(main,"comboStyle").Enabled && !Field<Button>(main,"btnAddCustomColor").Enabled,"Capture controls are not consistently locked");
   using var cover=new Form{Text="Occlusion test",StartPosition=FormStartPosition.Manual,Bounds=target.Bounds,BackColor=Color.Magenta};
   cover.Show();target.Phase=1;latest=null;target.Invalidate();
   Wait(()=>Matches(1),"Covering window leaked into captured colors");cover.Hide();
   target.Location=new Point(220,130);target.Phase=2;latest=null;target.Invalidate();
   Wait(()=>Matches(2),"Moving window changed capture coordinates");
   int deviceCount=capture.DeviceCreationCount;
   target.ClientSize=new Size(1120,700);target.Phase=3;latest=null;target.Invalidate();
   Wait(()=>Matches(3),"Resize did not rebuild client zones");
   Require(capture.DeviceCreationCount==deviceCount,"Window resize recreated the D3D device");
   target.WindowState=FormWindowState.Minimized;
   Wait(()=>!capture.IsRunning,"Minimized window capture was not stopped");
   Require(Field<Button>(main,"btnStartStop").BackColor==startColor,"Stopped capture did not restore the green Start color");
   selector.SelectedIndex=0;selector.SelectedItem=option;Application.DoEvents();
   var mode=Field<OverlayComboBox>(main,"domainUpDownMode");mode.SelectedIndex=1;mode.SelectedIndex=0;
   selector.DroppedDown=true;Application.DoEvents();selector.DroppedDown=false;Application.DoEvents();
   selector.Items.Add(option);selector.SelectedItem=option;
   int messages=0;
   EventHandler closeError=(s,e)=>
   {
    foreach(Form dialog in Application.OpenForms)
     if(dialog.GetType().Name=="OverlayMessageDialog")
     {
      Require(dialog.FormBorderStyle==FormBorderStyle.None,"Error dialog uses native border");
      string text=dialog.Controls["messageLabel"].Text;
      Require((text.Contains("свёрнуто") || text.Contains("minimized",StringComparison.OrdinalIgnoreCase)) && !text.Contains("System."),"Minimized error is not user-readable");
      Directory.CreateDirectory("artifacts/ui");using var bitmap=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(bitmap,dialog.ClientRectangle);bitmap.Save("artifacts/ui/minimized-error.png");
      messages++;dialog.DialogResult=DialogResult.OK;dialog.Close();break;
     }
   };
   Application.Idle+=closeError;
   try {typeof(MainForm).GetMethod("StartStopClicked",Private).Invoke(main,new object[]{main,EventArgs.Empty});}
   finally {Application.Idle-=closeError;}
   Require(messages==1 && !capture.IsRunning,"Start on minimized window did not show a styled error");
   Require(selector.Enabled && mode.Enabled && Field<OverlayComboBox>(main,"comboStyle").Enabled && Field<Button>(main,"btnAddCustomColor").Enabled,"Controls did not unlock after failed start");
   target.WindowState=FormWindowState.Normal;Application.DoEvents();latest=null;Start();Wait(()=>Matches(3),"Capture could not restart after restore");
   target.Close();Wait(()=>!capture.IsRunning,"Closed window capture was not stopped");
   main.Close();Require(File.ReadAllText(path).Contains("GenshinImpact"),"Window source preference not saved");
   Console.WriteLine("PASS window crop/resize, consistent control lock, source/mode/dropdown changes while minimized, styled start error, restore/close and persistence; no keyboard input sent.");return 0;
  }
  catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
 }
}
