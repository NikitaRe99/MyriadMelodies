using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MyriadMelodies;
class Smoke
{
 [STAThread] static int Main()
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
  Application.EnableVisualStyles();
  try
  {
   using var host=new Form { Text="Capture smoke check", ClientSize=new Size(600,100), ShowInTaskbar=false, StartPosition=FormStartPosition.CenterScreen };
   var handles=new IntPtr[6];
   for(int i=0;i<6;i++)
   {
    var panel=new Panel {Location=new Point(i*100,0),Size=new Size(96,96)};
    host.Controls.Add(panel); handles[i]=panel.Handle;
   }
   host.Show(); Application.DoEvents();
   using var capture=new GpuScreenCapture {AutomaticRecovery=false};
   var bounds=Screen.FromControl(host).Bounds;
   var regions=new Rectangle[6];
   for(int i=0;i<6;i++) regions[i]=new Rectangle(bounds.Left+100+i*40,bounds.Top+100,32,32);
   int frames=0, wrongColorCount=0; Exception failure=null;
   capture.FrameCaptured+=(s,e)=>{ if(e.Colors.Count!=6) Interlocked.Increment(ref wrongColorCount); Interlocked.Increment(ref frames); };
   capture.CaptureFailed+=(s,e)=>failure=e.Exception;
   foreach(string mode in new[]{"normal start", "preview start", "theme sampling"})
   {
    frames=0; failure=null;
    capture.Start(regions,mode=="preview start"?handles:null);
    var timer=Stopwatch.StartNew();
    while(Volatile.Read(ref frames)<3 && failure==null && timer.ElapsedMilliseconds<5000)
    { Application.DoEvents(); Thread.Sleep(10); }
    capture.Stop();
    if(failure!=null) throw new Exception(mode+" asynchronous failure",failure);
    if(frames==0 || wrongColorCount!=0) throw new Exception(mode+": no six-color frame received");
    Console.WriteLine($"PASS {mode}: {frames} frames, six colors per frame. Adapter: {capture.AdapterName}");
   }
   host.Close();
   return 0;
  }
  catch(Exception e) { Console.Error.WriteLine(e); return 1; }
 }
}
