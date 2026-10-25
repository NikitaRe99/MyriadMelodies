using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows.Forms;
using MyriadMelodies;

class RegressionChecks
{
 sealed class QueuedContext : SynchronizationContext
 {
  readonly Queue<Action> callbacks=new Queue<Action>();
  public override void Post(SendOrPostCallback callback,object state){lock(callbacks)callbacks.Enqueue(()=>callback(state));}
  public void Drain(){while(true){Action callback;lock(callbacks){if(callbacks.Count==0)return;callback=callbacks.Dequeue();}callback();}}
 }
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly Assembly App=typeof(MainForm).Assembly;
 static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
 static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Private).Invoke(obj,args);
 static void Require(bool value,string message){if(!value) throw new Exception(message);}
 static void Wait(Func<bool> condition,string message)
 {
  var end=DateTime.UtcNow.AddSeconds(8);
  while(!condition() && DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}
  Require(condition(),message);
 }
 [STAThread] static int Main(string[] args)
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  try
  {
   string folder=Path.GetFullPath("artifacts/tests/regression-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   var events=new List<(Keys Key,bool Down)>();
   object store=Activator.CreateInstance(App.GetType("MyriadMelodies.OverlaySettingsStore"),new object[]{Path.Combine(folder,"settings.json")});
   Action<Keys,bool> fake=(key,down)=>{lock(events)events.Add((key,down));};
   var inputAllowed=new[]{true};
   using var form=(MainForm)Activator.CreateInstance(typeof(MainForm),Private,null,new object[]{store,fake,new Func<IntPtr,bool>(_=>inputAllowed[0])},null);
   form.TopMost=false;form.Show();Application.DoEvents();
   var catalog=App.GetType("MyriadMelodies.ColorPaletteCatalog");
   var styleType=App.GetType("MyriadMelodies.ColorPaletteStyle");
   var flashType=App.GetType("MyriadMelodies.ThemeFlashMode");
   var styles=(IList)catalog.GetField("MutableStyles",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
   object standard=styles[0];
   Color GetColor(object style,string name)=>(Color)styleType.GetProperty(name).GetValue(style);
   string path=Path.Combine(folder,"palettes.json");
   object custom=catalog.GetMethod("AddCustomStyle").Invoke(null,new object[]{"standard-copy",GetColor(standard,"ShortPressColor"),GetColor(standard,"LongPressColor"),Enum.Parse(flashType,"Colored"),path});
   Require((bool)styleType.GetProperty("UsesStandardInput").GetValue(custom),"Colored standard copy lost hold behavior");
   var dispatcher=Field<object>(form,"keyInput");
   foreach(object style in new[]{standard,custom})
   {
    dispatcher.GetType().GetMethod("ReleaseAll").Invoke(dispatcher,null);
    Call(form,"ProcessCapturedColor",GetColor(style,"LongPressColor"),style,true,0);
    Call(form,"ProcessCapturedColor",GetColor(style,"LongPressFlashColor"),style,true,0);
    Require((bool)dispatcher.GetType().GetMethod("IsPressed").Invoke(dispatcher,new object[]{0}),"Standard hold released on pink flash");
    Call(form,"ProcessCapturedColor",(Color)catalog.GetField("NeutralColor").GetValue(null),style,true,0);
    Require(!(bool)dispatcher.GetType().GetMethod("IsPressed").Invoke(dispatcher,new object[]{0}),"Standard hold remained down on an empty zone");
   }
   dispatcher.GetType().GetMethod("ReleaseAll").Invoke(dispatcher,null);
   // Real video averages measured before the red miss phase, using the GPU's circular mask.
   foreach(var fixture in new[]{
    (17,Color.FromArgb(254,252,217)),(17,Color.FromArgb(254,252,209)),
    (24,Color.FromArgb(220,223,252)),(24,Color.FromArgb(223,225,251)),(24,Color.FromArgb(212,213,250))})
    Require(Call(form,"DetectColorSignal",fixture.Item2,styles[fixture.Item1]).ToString()=="LongPressFlash","Measured highlight frame not recognized");
   Require(Call(form,"DetectColorSignal",Color.FromArgb(241,167,151),styles[24]).ToString()=="None","Red miss was mistaken for a note or flash");
   typeof(MainForm).GetField("colorTolerance",Private).SetValue(form,35);
   foreach(object style in styles)
   {
    if(!(bool)styleType.GetProperty("IsCustom").GetValue(style))
    {
     Require(Call(form,"DetectColorSignal",GetColor(style,"LongPressColor"),style).ToString()=="LongPress","Exact long color lost to tolerance overlap");
     Require(Call(form,"DetectColorSignal",GetColor(style,"LongPressFlashColor"),style).ToString()=="LongPressFlash","Exact flash lost to tolerance overlap");
    }
   }
   object first=styles[0],second=styles[1];string secondName=(string)styleType.GetProperty("Name").GetValue(second);
   var baseline=(IList)Activator.CreateInstance(styles.GetType());foreach(object style in styles)baseline.Add(style);
   catalog.GetMethod("UpdateStyle").Invoke(null,new object[]{second,"renamed second",Color.Red,Color.Green,Color.Blue,path});
   catalog.GetMethod("UpdateStyle").Invoke(null,new object[]{first,secondName,GetColor(first,"ShortPressColor"),GetColor(first,"LongPressColor"),GetColor(first,"LongPressFlashColor"),path});
   catalog.GetMethod("LoadCustomStyles",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{baseline,path});
   Require((string)styleType.GetProperty("Name").GetValue(baseline[0])==secondName,"Chained builtin rename not restored");
   Require((string)styleType.GetProperty("Name").GetValue(baseline[1])=="renamed second","Second builtin rename not restored");
   string legacyPath=Path.Combine(folder,"legacy.json");
   File.WriteAllText(legacyPath,JsonSerializer.Serialize(new[]{
    new {Name="legacy standard",ShortPressArgb=Color.FromArgb(241,195,95).ToArgb(),LongPressArgb=Color.FromArgb(167,147,245).ToArgb(),LongPressFlashArgb=Color.FromArgb(210,173,255).ToArgb(),FlashMode="Colored",PaletteVersion=1},
    new {Name="legacy arbitrary",ShortPressArgb=Color.Red.ToArgb(),LongPressArgb=Color.Green.ToArgb(),LongPressFlashArgb=Color.Magenta.ToArgb(),FlashMode="Colored",PaletteVersion=2}
   }));
   var migrated=(IList)Activator.CreateInstance(styles.GetType());
   catalog.GetMethod("LoadCustomStyles",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{migrated,legacyPath});
   Require((bool)styleType.GetProperty("UsesStandardInput").GetValue(migrated[0]),"Legacy standard behavior not migrated");
   Require(!(bool)styleType.GetProperty("UsesStandardInput").GetValue(migrated[1]),"Arbitrary legacy color incorrectly became standard");
   var getFlags=App.GetType("MyriadMelodies.KeyboardInputSender").GetMethod("GetFlags",BindingFlags.Static|BindingFlags.NonPublic);
   Require((uint)getFlags.Invoke(null,new object[]{Keys.Left,true})==1,"Arrow missing extended flag");
   Require((uint)getFlags.Invoke(null,new object[]{Keys.Delete,false})==3,"Extended key-up flags incorrect");
   Require((uint)getFlags.Invoke(null,new object[]{Keys.NumPad7,true})==0,"Numpad incorrectly marked extended");
   Require(!App.GetType("MyriadMelodies.WindowCaptureTarget").GetMethod("IsGameWindow").Invoke(null,new object[]{form.Handle}).Equals(true),"Non-game process accepted as game");
   Console.WriteLine("PASS closest color classification, custom hold behavior, chained rename persistence, extended keyboard flags and non-game rejection.");
   if(Array.IndexOf(args,"--gpu")>=0) CheckGpu(form,dispatcher,inputAllowed);
   form.Close();return 0;
  }
  catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
 }

 static void CheckGpu(MainForm form,object dispatcher,bool[] inputAllowed)
 {
  var capture=Field<GpuScreenCapture>(form,"screenCapture");
  var callback=(EventHandler<GpuFrameCapturedEventArgs>)Delegate.CreateDelegate(typeof(EventHandler<GpuFrameCapturedEventArgs>),form,typeof(MainForm).GetMethod("ScreenCapture_FrameCaptured",Private));
  capture.FrameCaptured-=callback;
  var failureCallback=(EventHandler<CaptureFailedEventArgs>)Delegate.CreateDelegate(typeof(EventHandler<CaptureFailedEventArgs>),form,typeof(MainForm).GetMethod("ScreenCapture_CaptureFailed",Private));
  capture.CaptureFailed-=failureCallback;
  int frames=0,failures=0;var timings=new List<double>();var mapTimes=new List<double>();
  GpuFrameCapturedEventArgs firstFrame=null;Color firstColor=default;
  capture.FrameCaptured+=(s,e)=>
  {
   lock(timings)
   {
    timings.Add(capture.LastAnalysisMilliseconds);mapTimes.Add(capture.LastReadbackMilliseconds);
    if(firstFrame==null){firstFrame=e;firstColor=e.Colors[0];}
   }
   Interlocked.Increment(ref frames);
  };
  capture.CaptureFailed+=(s,e)=>Interlocked.Increment(ref failures);
  void Start()=>Call(form,"StartGpuCapture",false);
  void Release()=>dispatcher.GetType().GetMethod("ReleaseAll").Invoke(dispatcher,null);
  bool Pressed()=>(bool)dispatcher.GetType().GetMethod("IsPressed").Invoke(dispatcher,new object[]{0});
  void Press()=>dispatcher.GetType().GetMethod("Press").Invoke(dispatcher,new object[]{0,Keys.A});
  Start();Wait(()=>Volatile.Read(ref frames)>=30,"Insufficient timing frames");
  Require(firstFrame.Colors[0]==firstColor,"Published frame was mutated");
  object oldPool=Field<object>(capture,"framePool");
  object oldItem=Field<object>(capture,"item");
  int oldSession=capture.SessionId;
  var data=(GpuFrameCapturedEventArgs)Activator.CreateInstance(typeof(GpuFrameCapturedEventArgs),Private,null,new object[]{new Color[6],oldSession},null);
  int processed=0;
  typeof(MainForm).GetField("compareMethod",Private).SetValue(form,new Action<Color,int>((c,i)=>{Interlocked.Increment(ref processed);Press();}));
  object gate=Field<object>(form,"inputProcessingSync");Exception workerError=null;
  var worker=new Thread(()=>{try{Call(form,"ScreenCapture_FrameCaptured",capture,data);}catch(Exception ex){workerError=ex;}});
  lock(gate)
  {
   worker.Start();var deadline=Stopwatch.StartNew();
   while((worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin)==0 && deadline.ElapsedMilliseconds<3000) Thread.Yield();
   Require((worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0,"Frame did not block at input gate");
   capture.Stop();Release();
  }
  Require(worker.Join(3000),"Frame worker stuck");if(workerError!=null)throw workerError;
  Require(processed==0 && !Pressed(),"Late frame pressed key after Stop");
  Start();Call(form,"ScreenCapture_FrameCaptured",capture,data);
  Call(capture,"OnFrameArrived",oldPool,null);
  Call(capture,"OnCaptureItemClosed",oldItem,null);
  Require(capture.IsRunning,"Old capture-item close stopped new session");
  Require(processed==0,"Previous session frame was accepted after restart");
  var current=(GpuFrameCapturedEventArgs)Activator.CreateInstance(typeof(GpuFrameCapturedEventArgs),Private,null,new object[]{new Color[6],capture.SessionId},null);
  inputAllowed[0]=false;Press();Call(form,"ScreenCapture_FrameCaptured",capture,current);
  Require(processed==0 && !Pressed(),"Screen source sent input while game was not active");inputAllowed[0]=true;
  Press();Call(capture,"QueueRecovery",new Exception("injected failure"));
  Require(!Pressed() && !capture.IsRunning,"Recovery did not immediately pause input");
  capture.Stop();
  var until=Stopwatch.StartNew();while(until.ElapsedMilliseconds<250){Application.DoEvents();Thread.Sleep(5);}
  Require(!capture.IsRunning && !capture.IsRecovering,"Queued recovery resurrected stopped capture");
  Call(capture,"QueueRecoveryForSession",new Exception("stale failure"),oldSession);
  Require(!capture.IsRunning && !capture.IsRecovering,"Stale failure resurrected stopped capture");
  capture.AutomaticRecovery=false;Start();Press();Call(capture,"QueueRecovery",new Exception("injected terminal failure"));
  Require(!Pressed(),"Disabled recovery left key down");
  Wait(()=>Volatile.Read(ref failures)==1 && !capture.IsRecovering,"Disabled recovery did not report failure");
  capture.AutomaticRecovery=true;Start();int previousFrames=frames;Press();Call(capture,"QueueRecovery",new Exception("recover once"));
  Wait(()=>capture.IsRunning && !capture.IsRecovering && frames>previousFrames,"Capture did not recover");
  capture.Stop();Require(!Pressed(),"Stop after recovery left key down");
  lock(timings)
  {
   timings.Sort();mapTimes.Sort();
   Console.WriteLine($"GPU analysis: n={timings.Count}, median={timings[timings.Count/2]:F3} ms, p95={timings[(int)((timings.Count-1)*.95)]:F3} ms; Map median={mapTimes[mapTimes.Count/2]:F3} ms, p95={mapTimes[(int)((mapTimes.Count-1)*.95)]:F3} ms.");
  }
  Console.WriteLine("PASS Stop/input race, stale session/pool rejection, immutable frame, immediate failure release, recovery cancellation and disabled-recovery notification; fake keyboard only.");
  // A temporary color sample must never restart input through a queued recovery.
  EventHandler acceptPreparation=(s,e)=>
  {
   foreach(Form dialog in Application.OpenForms)
    if(dialog.GetType().Name=="ThemeCapturePreparationDialog")
    {dialog.DialogResult=DialogResult.OK;dialog.Close();break;}
  };
  Task BeginSample()
  {
   Application.Idle+=acceptPreparation;
   try{return (Task)Call(form,"ReadPreparedThemeColorsAsync",form);}
   finally{Application.Idle-=acceptPreparation;}
  }
  Task sample=BeginSample();
  Require(capture.IsRunning,"Temporary sample capture did not start");
  var queuedRecovery=new QueuedContext();
  typeof(GpuScreenCapture).GetField("ownerContext",Private).SetValue(capture,queuedRecovery);
  Call(capture,"QueueRecovery",new Exception("sample interrupted"));
  Wait(()=>sample.IsCompleted,"Interrupted color sample did not finish");
  Require(sample.IsFaulted,"Interrupted sample unexpectedly succeeded");
  _=sample.Exception;
  queuedRecovery.Drain();
  var settle=Stopwatch.StartNew();while(settle.ElapsedMilliseconds<250){Application.DoEvents();Thread.Sleep(5);}
  Require(!capture.IsRunning && !capture.IsRecovering,"Temporary sample was resurrected by recovery");
  Task closingSample=BeginSample();form.Close();
  Wait(()=>closingSample.IsCompleted,"Closing the form left sampling pending");
  Require(closingSample.IsCanceled,"Form close did not cancel sampling");
  Require(!capture.IsRunning && !Pressed(),"Form close left capture or input running");
  Console.WriteLine("PASS interrupted temporary sampling cancels recovery; closing the window cancels its pending sample without late UI or input.");
 }
}
