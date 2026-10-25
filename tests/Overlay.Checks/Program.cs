using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyriadMelodies;
using MyriadMelodies.Controls;
partial class OverlayChecks
{
 const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
 static readonly string SettingsPath=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","tests","overlay-"+Guid.NewGuid().ToString("N")+".json");
 static MainForm CreateForm()
 {
  Type storeType=typeof(MainForm).Assembly.GetType("MyriadMelodies.OverlaySettingsStore");
  object store=Activator.CreateInstance(storeType,new object[]{SettingsPath});
  return (MainForm)Activator.CreateInstance(typeof(MainForm),Private,null,new object[]{store},null);
 }
 class DesignContext : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
 static T Field<T>(MainForm form,string name) => (T)typeof(MainForm).GetField(name,Private).GetValue(form);
 static void Click(MainForm form,string name) { Field<Button>(form,name).PerformClick(); Application.DoEvents(); }
 static void Save(Form form,string name)
 {
  using var bitmap=new Bitmap(form.Width,form.Height);
  form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
  bitmap.Save("artifacts/ui/"+name+".png");
 }
 static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
 static int RunThemeFlow()
 {
  using var form=CreateForm();
  var capture=Field<GpuScreenCapture>(form,"screenCapture");
  int frames=0,phase=0;Exception failure=null;
  capture.FrameCaptured+=(s,e)=>Interlocked.Increment(ref frames);
  using var timer=new System.Windows.Forms.Timer {Interval=50};
  var deadline=DateTime.UtcNow.AddSeconds(8);
  timer.Tick+=(s,e)=>
  {
   try
   {
    if(DateTime.UtcNow>deadline) throw new Exception($"Theme flow timed out: phase={phase}, frames={frames}, running={capture.IsRunning}");
    if(phase==0){phase=1;Field<Button>(form,"btnAddCustomColor").PerformClick();return;}
    foreach(Form dialog in Application.OpenForms)
    {
     if(phase==1 && dialog.GetType().Name=="ThemeCapturePreparationDialog")
     {
      Require(!capture.IsRunning,"Capture started before confirmation");
      Save(dialog,"theme-preparation");phase=2;dialog.DialogResult=DialogResult.OK;dialog.Close();return;
     }
     if(phase==2 && dialog.GetType().Name=="ThemeNameDialog")
     {
      Save(dialog,"theme-captured");phase=3;dialog.DialogResult=DialogResult.Cancel;dialog.Close();return;
     }
    }
    if(phase==3)
    {
     Require(frames>0,"No fresh color frame");Require(!capture.IsRunning,"Temporary capture did not stop");
     timer.Stop();form.Close();
    }
   }
   catch(Exception ex){failure=ex;timer.Stop();form.Close();}
  };
  form.Shown+=(s,e)=>{form.TopMost=false;timer.Start();};
  Application.Run(form);
  if(failure!=null){Console.Error.WriteLine(failure);return 1;}
  Console.WriteLine("PASS actual application loop: preparation confirmation -> fresh six-color frame -> naming -> cancel, no input sent.");
  return 0;
 }
 static int RunEditFlow()
 {
  using var form=CreateForm();
  var capture=Field<GpuScreenCapture>(form,"screenCapture");
  int phase=0,frames=0;Exception failure=null;
  capture.FrameCaptured+=(s,e)=>Interlocked.Increment(ref frames);
  using var timer=new System.Windows.Forms.Timer {Interval=50};
  var deadline=DateTime.UtcNow.AddSeconds(8);
  timer.Tick+=(s,e)=>
  {
   try
   {
    if(DateTime.UtcNow>deadline) throw new Exception($"Edit capture timed out: phase={phase}, frames={frames}");
    if(phase==0){phase=1;typeof(MainForm).GetMethod("EditTheme",Private).Invoke(form,new object[]{4});return;}
    foreach(Form dialog in Application.OpenForms)
    {
     if(phase==1 && dialog.GetType().Name=="ThemeEditDialog")
     {
      phase=2;((Button)dialog.Controls["recaptureButton"]).PerformClick();return;
     }
     if(phase==2 && dialog.GetType().Name=="ThemeCapturePreparationDialog")
     {
      Require(!capture.IsRunning,"Edit capture started before confirmation");
      phase=3;dialog.DialogResult=DialogResult.OK;dialog.Close();return;
     }
     if(phase==3 && dialog.GetType().Name=="ThemeEditDialog" && dialog.Enabled && frames>0)
     {
      Require(!capture.IsRunning,"Edit temporary capture did not stop");Save(dialog,"theme-editor-captured");
      phase=4;dialog.DialogResult=DialogResult.Cancel;dialog.Close();return;
     }
    }
    if(phase==4){timer.Stop();form.Close();}
   }
   catch(Exception exception){failure=exception;timer.Stop();form.Close();}
  };
  form.Shown+=(s,e)=>{form.TopMost=false;timer.Start();};Application.Run(form);
  if(failure!=null){Console.Error.WriteLine(failure);return 1;}
  Console.WriteLine("PASS actual edit loop: editor -> preparation -> fresh GPU colors -> editor -> cancel; no keys sent or theme changes saved.");return 0;
 }
 [STAThread] static int Main(string[] args)
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
  Application.EnableVisualStyles();
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  Directory.CreateDirectory("artifacts/ui");
  Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
  if(Array.IndexOf(args,"--theme-flow")>=0) return RunThemeFlow();
  if(Array.IndexOf(args,"--edit-flow")>=0) return RunEditFlow();
  try
  {
   RunNewFeatures();
   RunReviewChecks();
   RunStorageChecks();
   System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
   LicenseContext previous=LicenseManager.CurrentContext;
   LicenseManager.CurrentContext=new DesignContext();
   using(var designer=new MainForm())
   {
    Require(typeof(MainForm).GetField("keyInput",Private).GetValue(designer)==null,"Designer started input dispatcher");
    designer.TopMost=false; designer.Show(); Application.DoEvents();
    Require(Field<Panel>(designer,"settingsPanel").Visible,"Designer hides settings");
    Require(designer.FormBorderStyle==FormBorderStyle.None,"Designer border differs from runtime");
    Save(designer,"designer"); designer.Close();
   }
   LicenseManager.CurrentContext=previous;
   using var form=CreateForm();
   form.TopMost=false; form.Show(); Application.DoEvents();
   float scale=form.DeviceDpi/96f;
   Require(form.ClientSize.Height<=202*scale,"Default overlay is not compact");
   Require(form.Opacity==.96,"Opacity default not applied");
   Save(form,"main"); Save(form,"compact");
   var mode=Field<OverlayComboBox>(form,"domainUpDownMode");
   mode.DroppedDown=true; Application.DoEvents();
   Require(mode.DroppedDown,"Custom popup immediately closed");
   var menu=(ToolStripDropDown)typeof(OverlayComboBox).GetField("popup",Private).GetValue(mode);
   using(var image=new Bitmap(menu.Width,menu.Height))
   { menu.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("artifacts/ui/mode-menu.png"); }
   mode.DroppedDown=false; Application.DoEvents();
   Require(!mode.DroppedDown,"Custom popup cannot close");
   Require(menu.IsDisposed,"Closed menu resources were not released");
   for(int i=0;i<3;i++)
   {
    mode.DroppedDown=true; Application.DoEvents();
    var list=(Control)typeof(OverlayComboBox).GetField("popupList",Private).GetValue(mode);
    list.GetType().GetProperty("SelectedIndex").SetValue(list,1);
    list.GetType().GetMethod("OnKeyDown",Private).Invoke(list,new object[]{new KeyEventArgs(Keys.Enter)});
    Application.DoEvents();
    Require(mode.SelectedIndex==1 && !mode.DroppedDown,"Keyboard selection did not commit/close");
    mode.SelectedIndex=0;
   }
   Click(form,"btnSettings");
   IntPtr originalHandle=form.Handle;
   var paletteField=Field<OverlayComboBox>(form,"comboStyle");
   paletteField.DroppedDown=true;Application.DoEvents();
   var paletteList=(Control)typeof(OverlayComboBox).GetField("popupList",Private).GetValue(paletteField);
   paletteList.GetType().GetMethod("OnMouseWheel",Private).Invoke(paletteList,new object[]{new MouseEventArgs(MouseButtons.None,0,30,30,-120)});
   paletteList.GetType().GetMethod("OnKeyDown",Private).Invoke(paletteList,new object[]{new KeyEventArgs(Keys.End)});
   var paletteMenu=(ToolStripDropDown)typeof(OverlayComboBox).GetField("popup",Private).GetValue(paletteField);
   using(var image=new Bitmap(paletteMenu.Width,paletteMenu.Height))
   {paletteMenu.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("artifacts/ui/theme-menu.png");}
   paletteList.GetType().GetMethod("OnKeyDown",Private).Invoke(paletteList,new object[]{new KeyEventArgs(Keys.Enter)});
   Application.DoEvents();Require(form.Handle==originalHandle,"Theme selection recreated the main window");
   paletteField.SelectedIndex=4;
   int editorCount=0;
   EventHandler cancelEditor=(sender,eventArgs)=>
   {
    foreach(Form dialog in Application.OpenForms)
     if(dialog.GetType().Name=="ThemeEditDialog") {editorCount++;dialog.DialogResult=DialogResult.Cancel;dialog.Close();break;}
   };
   paletteField.DroppedDown=true;Application.DoEvents();
   var actionList=(Control)typeof(OverlayComboBox).GetField("popupList",Private).GetValue(paletteField);
   Application.Idle+=cancelEditor;
   try
   {
    actionList.GetType().GetMethod("OnMouseDown",Private).Invoke(actionList,new object[]{new MouseEventArgs(MouseButtons.Left,1,10,15,0)});
    Application.DoEvents();
   }
   finally {Application.Idle-=cancelEditor;}
   Require(editorCount==1,"Pencil did not open theme editor");
   Require(paletteField.SelectedIndex==4,"Pencil click changed selected theme");
   var tolerance=Field<OverlayNumericUpDown>(form,"colorToleranceUpDown");
   tolerance.Value=20; tolerance.UpButton(); Require(tolerance.Value==21,"Numeric increment failed");
   tolerance.DownButton(); Require(tolerance.Value==20,"Numeric decrement failed");
   tolerance.Value=tolerance.Maximum; tolerance.UpButton(); Require(tolerance.Value==tolerance.Maximum,"Numeric exceeds maximum");
   tolerance.Value=20;
   var input=(TextBox)typeof(OverlayNumericUpDown).GetField("input",Private).GetValue(tolerance);
   Require(input.Width>=TextRenderer.MeasureText("70",input.Font,Size.Empty,TextFormatFlags.NoPadding).Width+2,"Two-digit value is clipped");
   input.Text="70"; Require(tolerance.Value==70,"Typed numeric value did not commit");
   input.Text="999";
   typeof(OverlayNumericUpDown).GetMethod("CommitText",Private).Invoke(tolerance,null);
   Require(tolerance.Value==70 && input.Text=="70","Typed value is not clamped");
   input.Text="abc";
   typeof(OverlayNumericUpDown).GetMethod("CommitText",Private).Invoke(tolerance,null);
   Require(input.Text=="70","Invalid numeric text is not restored");
   tolerance.Value=20;
   Save(form,"settings");
   Require(Field<OverlayNumericUpDown>(form,"opacityUpDown").Visible,"Cannot open settings");
   Field<OverlayNumericUpDown>(form,"opacityUpDown").Value=85;
   Require(Math.Abs(form.Opacity-.85)<.001,"Opacity control does not work");
   Field<OverlayNumericUpDown>(form,"opacityUpDown").Value=96;
   Click(form,"btnShow"); Save(form,"expanded");
   var flow=Field<FlowLayoutPanel>(form,"demonstratPanel");
   Require(flow.Controls.Count==6,"Not all six previews created");
   foreach(Control tile in flow.Controls) Require(flow.ClientRectangle.Contains(tile.Bounds),"Preview clipped outside compact panel");
   Require(flow.Controls[0].Top==flow.Controls[2].Top && flow.Controls[3].Top>flow.Controls[0].Top,"Previews are not in two rows");
   Require(flow.Controls[2].Right==flow.Width && flow.Controls[5].Right==flow.Width,"Preview rows do not fill width");
   int keyDialogs=0;
   EventHandler assignKey=(sender,eventArgs)=>
   {
    foreach(Form dialog in Application.OpenForms)
     if(dialog.GetType().Name=="KeyBindingDialog")
     {
      var args=new object[]{Message.Create(IntPtr.Zero,0x100,IntPtr.Zero,IntPtr.Zero),Keys.F6};
      dialog.GetType().GetMethod("ProcessCmdKey",Private).Invoke(dialog,args);
      Require((Keys)dialog.GetType().GetProperty("SelectedKey").GetValue(dialog)==Keys.F6,"Key dialog did not capture function key");
      Save(dialog,"key-binding");keyDialogs++;dialog.DialogResult=DialogResult.OK;dialog.Close();break;
     }
   };
   Application.Idle+=assignKey;
   try
   {
    var firstArea=(MyriadMelodies.PreviewAreaView)Field<IList>(form,"captureAreas")[0];
    typeof(Control).GetMethod("OnClick",Private).Invoke(firstArea.GpuPreview,new object[]{EventArgs.Empty});
   }
   finally {Application.Idle-=assignKey;}
   var assigned=Field<Keys[]>(form,"AreaKeys");
   Require(keyDialogs==1 && assigned[0]==Keys.F6,"Clicking preview did not apply key");
   Require(flow.Controls[0].Controls["keyLabel"].Text=="F6","Card title did not update");
   typeof(MainForm).GetMethod("ApplyAreaKey",Private).Invoke(form,new object[]{0,Keys.S});
   Require(assigned[0]==Keys.S && assigned[1]==Keys.F6,"Occupied keys were not swapped");
   Click(form,"btnResetKeys");
   Require(assigned[0]==Keys.A && assigned[1]==Keys.S && assigned[5]==Keys.L,"Global key reset failed");
   var keyDialogType=typeof(MainForm).Assembly.GetType("MyriadMelodies.KeyBindingDialog");
   using(var keyDialog=(Form)Activator.CreateInstance(keyDialogType,new object[]{1,Keys.F6}))
   {
    keyDialog.TopMost=false;keyDialog.Show();Application.DoEvents();
    foreach(Control control in keyDialog.Controls) if(control is Button button && button.Text=="Сбросить") button.PerformClick();
    Require((Keys)keyDialogType.GetProperty("SelectedKey").GetValue(keyDialog)==Keys.A,"Single-zone reset failed");
    keyDialog.Close();
   }
   typeof(MainForm).GetMethod("ApplyAreaKey",Private).Invoke(form,new object[]{0,Keys.NumPad7});
   Save(form,"key-bindings");
   Click(form,"btnSettings"); Save(form,"preview");
   for(int i=0;i<3;i++) { Click(form,"btnShow"); Click(form,"btnShow"); }
   Require(flow.Controls.Count==6,"Preview toggle duplicates or loses regions");
   int preparationCount=0;
   EventHandler cancelPreparation=(sender,eventArgs)=>
   {
    foreach(Form dialog in Application.OpenForms)
     if(dialog.GetType().Name=="ThemeCapturePreparationDialog")
     {
      Require(!Field<GpuScreenCapture>(form,"screenCapture").IsRunning,"Capture started before preparation was confirmed");
      preparationCount++;dialog.DialogResult=DialogResult.Cancel;dialog.Close();break;
     }
   };
   Application.Idle+=cancelPreparation;
   try {Click(form,"btnAddCustomColor");} finally {Application.Idle-=cancelPreparation;}
   Require(preparationCount==1,"Preparation dialog was skipped");
   Require(!Field<GpuScreenCapture>(form,"screenCapture").IsRunning,"Cancel started a capture");
   if(Array.IndexOf(args,"--capture")>=0)
   {
    typeof(MainForm).GetField("suppressKeyInput",Private).SetValue(form,1);
    var capture=Field<GpuScreenCapture>(form,"screenCapture");
    var callback=(EventHandler<CaptureFailedEventArgs>)Delegate.CreateDelegate(typeof(EventHandler<CaptureFailedEventArgs>),form,typeof(MainForm).GetMethod("ScreenCapture_CaptureFailed",Private));
    capture.CaptureFailed-=callback;
    Exception failure=null; int frames=0;
    capture.CaptureFailed+=(s,e)=>failure=e.Exception;
    capture.FrameCaptured+=(s,e)=>Interlocked.Increment(ref frames);
    typeof(MainForm).GetMethod("StartGpuCapture",Private).Invoke(form,new object[]{true});
    typeof(MainForm).GetMethod("UpdateCaptureState",Private).Invoke(form,null);
    Keys currentKey=Field<Keys[]>(form,"AreaKeys")[0];
    typeof(MainForm).GetMethod("ApplyAreaKey",Private).Invoke(form,new object[]{0,Keys.Z});
    Require(Field<Keys[]>(form,"AreaKeys")[0]==currentKey,"Running capture allowed key changes");
    Require(!((MyriadMelodies.PreviewAreaView)Field<IList>(form,"captureAreas")[0]).CanAssign && !Field<Button>(form,"btnResetKeys").Enabled,"Key controls not locked while capturing");
    var deadline=DateTime.UtcNow.AddSeconds(5);
    while(frames<5 && failure==null && DateTime.UtcNow<deadline) { Application.DoEvents(); Thread.Sleep(10); }
    capture.Stop();
    typeof(MainForm).GetMethod("UpdateCaptureState",Private).Invoke(form,null);
    Require(failure==null,"Layered overlay capture failed: "+failure);
    Require(frames>=1,"No frames in layered preview");
    Console.WriteLine("PASS GPU preview in actual borderless, translucent MainForm; no key events sent.");

   }
   Click(form,"btnShow");
   Require(form.ClientSize.Height<=202*scale,"Overlay does not return to compact size");
   var preparationType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ThemeCapturePreparationDialog");
   using(var preparation=(Form)Activator.CreateInstance(preparationType,new object[]{"Вертикальный режим"}))
   {preparation.TopMost=false;preparation.Show(form);Application.DoEvents();Save(preparation,"theme-preparation");preparation.Close();}
   var themeType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ThemeNameDialog");
   using(var theme=(Form)Activator.CreateInstance(themeType,new object[]{Color.FromArgb(212,180,120),Color.FromArgb(150,200,180)}))
   {
    theme.TopMost=false;theme.Show(form);Application.DoEvents();Save(theme,"theme");
    var choice=(OverlayComboBox)themeType.GetField("flashModeCombo",Private).GetValue(theme);
    choice.SelectedIndex=1;Application.DoEvents();Save(theme,"theme-colored-flash");
    Require(themeType.GetProperty("FlashMode").GetValue(theme).ToString()=="Colored","Colored flash selection failed");
    choice.SelectedIndex=0;
    Require(themeType.GetProperty("FlashMode").GetValue(theme).ToString()=="White","White flash selection failed");
    theme.Close();
   }
   Click(form,"btnSettings"); Click(form,"btnShow");
   form.Scale(new SizeF(1.5f,1.5f));
   typeof(MainForm).GetMethod("UpdateOverlayLayout",Private).Invoke(form,null);
   Application.DoEvents();
   foreach(Control tile in flow.Controls) Require(flow.ClientRectangle.Contains(tile.Bounds),"Preview clipped after scaling");
   Save(form,"dpi150");
   Field<OverlayComboBox>(form,"domainUpDownMode").SelectedIndex=1;
   Field<OverlayComboBox>(form,"comboStyle").SelectedIndex=2;
   tolerance.Value=37;
   Field<OverlayNumericUpDown>(form,"opacityUpDown").Value=89;
   Field<CheckBox>(form,"pinCheckBox").Checked=false;
   form.Close();
   Require(File.Exists(SettingsPath),"Settings were not saved");
   using(var restored=CreateForm())
   {
    restored.Show();Application.DoEvents();
    Require(Field<OverlayComboBox>(restored,"domainUpDownMode").SelectedIndex==1,"Mode was not restored");
    Require(Field<OverlayComboBox>(restored,"comboStyle").SelectedIndex==2,"Theme was not restored");
    Require(Field<OverlayNumericUpDown>(restored,"colorToleranceUpDown").Value==37,"Tolerance was not restored");
    Require(Field<OverlayNumericUpDown>(restored,"opacityUpDown").Value==89,"Opacity was not restored");
    Require(!restored.TopMost,"Pin setting was not restored");
    Require(Field<Keys[]>(restored,"AreaKeys")[0]==Keys.NumPad7,"Key binding was not restored");
    Require(Field<FlowLayoutPanel>(restored,"demonstratPanel").Controls[0].Controls["keyLabel"].Text=="Num 7","Saved key not shown on restored card");
    Require(Field<Panel>(restored,"settingsPanel").Visible && Field<Panel>(restored,"previewPanel").Visible,"Panels were not restored");
    restored.Close();
   }
   File.WriteAllText(SettingsPath,"{broken");
   using(var recovered=CreateForm())
   {
    recovered.Show();Application.DoEvents();
    Require(Field<OverlayNumericUpDown>(recovered,"colorToleranceUpDown").Value==20,"Corrupt settings did not fall back to defaults");
    recovered.Close();
   }
   File.WriteAllText(SettingsPath,"{\"ZoneKeys\":[65,65,68,74,75,76]}");
   using(var invalidKeys=CreateForm())
   {
    invalidKeys.Show();Application.DoEvents();
    var keys=Field<Keys[]>(invalidKeys,"AreaKeys");
    Require(keys[0]==Keys.A && keys[1]==Keys.S,"Duplicate persisted keys were not reset");
    invalidKeys.Close();
   }
   Console.WriteLine("PASS card click/key capture, key swap, single/all reset, binding save/restore and invalid settings recovery; no keyboard input sent.");
   Type catalog=typeof(MainForm).Assembly.GetType("MyriadMelodies.ColorPaletteCatalog");
   Type dto=catalog.GetNestedType("StoredPaletteStyle",BindingFlags.NonPublic);
   Type flashType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ThemeFlashMode");
   foreach(string flashName in Enum.GetNames(flashType))
   {
    Color flash=(Color)catalog.GetMethod("GetFlashColor").Invoke(null,new object[]{Enum.Parse(flashType,flashName)});
    object stored=Activator.CreateInstance(dto,true);
    dto.GetProperty("Name").SetValue(stored,"round trip");
    dto.GetProperty("LongPressFlashArgb").SetValue(stored,flash.ToArgb());
    dto.GetProperty("FlashMode").SetValue(stored,flashName);
    string json=JsonSerializer.Serialize(stored,dto);
    object read=JsonSerializer.Deserialize(json,dto);
    Require((int)dto.GetProperty("LongPressFlashArgb").GetValue(read)==flash.ToArgb(),"Flash color was not serialized");
    Require((string)dto.GetProperty("FlashMode").GetValue(read)==flashName,"Flash mode was not serialized");
   }
   // All writes for catalog CRUD and built-in overrides use a workspace file.
   string palettePath=Path.Combine(Path.GetDirectoryName(SettingsPath),"palettes-"+Guid.NewGuid().ToString("N")+".json");
   Type styleType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ColorPaletteStyle");
   var styles=(IList)catalog.GetField("MutableStyles",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
   object builtIn=styles[0];
   var baseline=(IList)Activator.CreateInstance(styles.GetType());
   foreach(object entry in styles) baseline.Add(entry);
   object colored=Enum.Parse(flashType,"Colored");
   object custom=catalog.GetMethod("AddCustomStyle").Invoke(null,new object[]{"edit-delete-check",Color.Red,Color.Green,colored,palettePath});
   custom=catalog.GetMethod("UpdateStyle").Invoke(null,new object[]{custom,"renamed-check",Color.Blue,Color.Yellow,Color.Magenta,palettePath});
   Require((string)styleType.GetProperty("Name").GetValue(custom)=="renamed-check","Rename failed");
   var editType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ThemeEditDialog");
   Func<IWin32Window,Task<(Color Short,Color Long)?>> sample=owner=>Task.FromResult<(Color,Color)?>((Color.Aqua,Color.Orange));
   using(var editor=(Form)Activator.CreateInstance(editType,new object[]{custom,sample}))
   {
    editor.TopMost=false;editor.Show();Application.DoEvents();Save(editor,"theme-editor");
    var name=(OverlayTextBox)editType.GetField("nameInput",Private).GetValue(editor);
    name.Text="";Require(!((Button)editor.AcceptButton).Enabled,"Empty name can be saved");
    name.Text="renamed in editor";Require(((Button)editor.AcceptButton).Enabled,"Valid name cannot be saved");
    ((Button)editor.Controls["recaptureButton"]).PerformClick();
    Require(((Color)editType.GetProperty("ShortColor").GetValue(editor)).ToArgb()==Color.Aqua.ToArgb(),"Recapture did not update short color");
    Require(((Color)editType.GetProperty("LongColor").GetValue(editor)).ToArgb()==Color.Orange.ToArgb(),"Recapture did not update long color");
    var flashChoice=(OverlayComboBox)editType.GetField("flashModeCombo",Private).GetValue(editor);
    flashChoice.SelectedIndex=2;
    Require(((Color)editType.GetProperty("FlashColor").GetValue(editor)).ToArgb()==Color.FromArgb(237,226,196).ToArgb(),"Edited flash mismatch");
    Save(editor,"theme-editor");
    editor.Close();
   }
   object modified=catalog.GetMethod("UpdateStyle").Invoke(null,new object[]{builtIn,"renamed builtin",Color.Cyan,Color.Blue,Color.Pink,palettePath});
   Require((string)styleType.GetProperty("Name").GetValue(modified)=="renamed builtin","Builtin rename failed");
   Require(!(bool)styleType.GetProperty("IsCustom").GetValue(modified),"Builtin became custom");
   Require((bool)styleType.GetProperty("UsesStandardInput").GetValue(modified)==(bool)styleType.GetProperty("UsesStandardInput").GetValue(builtIn),"Editing changed input behavior");
   catalog.GetMethod("LoadCustomStyles",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{baseline,palettePath});
   Require(((Color)styleType.GetProperty("ShortPressColor").GetValue(baseline[0])).ToArgb()==Color.Cyan.ToArgb(),"Builtin override not restored from file");
   bool foundRenamed=false;foreach(object entry in baseline) if((string)styleType.GetProperty("Name").GetValue(entry)=="renamed-check") foundRenamed=true;
   Require(foundRenamed,"Edited custom theme not restored from file");
   Require(!(bool)catalog.GetMethod("RemoveCustomStyle").Invoke(null,new[]{modified,palettePath}),"Builtin was deleted");
   catalog.GetMethod("ResetBuiltInStyle").Invoke(null,new[]{modified,palettePath});
   Require((Color)styleType.GetProperty("ShortPressColor").GetValue(styles[0])==(Color)styleType.GetProperty("ShortPressColor").GetValue(builtIn),"Builtin reset failed");
   string blockedPath=palettePath+"-directory";Directory.CreateDirectory(blockedPath);
   bool writeFailed=false;
   try {catalog.GetMethod("RemoveCustomStyle").Invoke(null,new[]{custom,blockedPath});}
   catch(TargetInvocationException exception) when(exception.InnerException is IOException || exception.InnerException is UnauthorizedAccessException){writeFailed=true;}
   Require(writeFailed && styles.Contains(custom),"Failed deletion did not restore in-memory theme");
   Require((bool)catalog.GetMethod("RemoveCustomStyle").Invoke(null,new[]{custom,palettePath}),"Custom delete failed");
   Require(!File.ReadAllText(palettePath).Contains("renamed-check"),"Deleted theme remains in storage");
   Require(!(bool)catalog.GetMethod("RemoveCustomStyle").Invoke(null,new[]{custom,palettePath}),"Second delete must be harmless");
   // Restore the running form's catalog selection after the temporary catalog objects changed.
   Console.WriteLine("PASS name editing, color recapture, flash choice, built-in rename/restore/reset, custom deletion and write failure rollback; user catalog untouched.");
   Console.WriteLine("PASS designer, dropdown scrolling/keyboard/close, two-row previews, numeric input, preparation cancel, settings restore and flash choice round trip.");
   return 0;
  }
  catch(Exception e) { Console.Error.WriteLine(e); return 1; }
 }
}
