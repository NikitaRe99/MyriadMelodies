using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using MyriadMelodies;
using MyriadMelodies.Controls;

partial class OverlayChecks
{
 static void RunStorageChecks()
 {
  var app=typeof(MainForm).Assembly;
  var storageType=app.GetType("MyriadMelodies.SettingsStorage");
  var locationType=app.GetType("MyriadMelodies.SettingsLocation");
  var storeType=app.GetType("MyriadMelodies.OverlaySettingsStore");
  var prefsType=app.GetType("MyriadMelodies.OverlayPreferences");
  string root=Path.GetFullPath(Path.Combine("artifacts/tests","storage-"+Guid.NewGuid().ToString("N")));
  string exe=Path.Combine(root,"app"),local=Path.Combine(root,"local"),legacy=Path.Combine(root,"legacy");
  Directory.CreateDirectory(exe);Directory.CreateDirectory(legacy);
  string prefsName="overlay-settings.json",themesName="custom-color-themes.json",marker="MyriadMelodies.portable";
  File.WriteAllText(Path.Combine(legacy,prefsName),"{\"ColorTolerance\":37,\"Language\":\"en\",\"ZoneKeys\":[90,83,68,74,75,76],\"SettingsExpanded\":true,\"PreviewVisible\":true}");
  const string themes="[{\"Name\":\"portable theme\",\"Order\":0}]";
  File.WriteAllText(Path.Combine(legacy,themesName),themes);
  object NewStorage(string appDir,string profileDir)=>Activator.CreateInstance(storageType,new object[]{appDir,profileDir,legacy});
  object NewStore(object storage)=>Activator.CreateInstance(storeType,Private,null,new[]{storage},null);
  object Read(object store)=>storeType.GetMethod("Load").Invoke(store,null);
  string Mode(object storage)=>storageType.GetProperty("Location").GetValue(storage).ToString();
  object storage=NewStorage(exe,local),store=NewStore(storage);
  object prefs=Read(store);
  Require((int)prefsType.GetProperty("ColorTolerance").GetValue(prefs)==37,"Renamed application did not read legacy preferences");
  Require(!(bool)prefsType.GetProperty("SettingsExpanded").GetValue(prefs) && !(bool)prefsType.GetProperty("PreviewVisible").GetValue(prefs),"First migrated launch was not compact");
  Require((string)storageType.GetMethod("GetReadPath").Invoke(storage,new object[]{themesName})==Path.Combine(legacy,themesName),"Legacy themes were not found");
  storeType.GetMethod("Save").Invoke(store,new[]{prefs});
  Require(File.Exists(Path.Combine(local,prefsName)),"Local settings were not saved to the new product folder");
  prefsType.GetProperty("ColorTolerance").SetValue(prefs,44);
  Action<string> saveThemes=path=>File.WriteAllText(path,themes);
  void Switch(string mode,Action<string> write)=>storageType.GetMethod("ChangeLocation").Invoke(storage,new object[]{Enum.Parse(locationType,mode),prefs,write});
  Switch("ExecutableDirectory",saveThemes);
  Require(Mode(storage)=="ExecutableDirectory" && File.Exists(Path.Combine(exe,marker)),"Portable selection was not committed");
  Require(File.ReadAllText(Path.Combine(exe,themesName))==themes,"Theme/order snapshot did not move with settings");
  object reopened=NewStorage(exe,local);
  Require(Mode(reopened)=="ExecutableDirectory" && (int)prefsType.GetProperty("ColorTolerance").GetValue(Read(NewStore(reopened)))==44,"Portable choice did not survive restart");
  Require(((int[])prefsType.GetProperty("ZoneKeys").GetValue(Read(NewStore(reopened))))[0]==90,"Portable key bindings were lost");
  string copied=Path.Combine(root,"copied-app");Directory.CreateDirectory(copied);
  foreach(string name in new[]{prefsName,themesName,marker})File.Copy(Path.Combine(exe,name),Path.Combine(copied,name));
  Require(Mode(NewStorage(copied,Path.Combine(root,"other-pc")))=="ExecutableDirectory","Portable folder did not survive moving to another path");
  using(var locked=new FileStream(Path.Combine(exe,marker),FileMode.Open,FileAccess.Read,FileShare.None))
  {
   bool failed=false;
   try{Switch("LocalAppData",saveThemes);}catch(TargetInvocationException e)when(e.InnerException is IOException || e.InnerException is UnauthorizedAccessException){failed=true;}
   Require(failed && Mode(storage)=="ExecutableDirectory","Failed location commit changed the active store");
  }
  Switch("LocalAppData",saveThemes);
  Require(Mode(NewStorage(exe,local))=="LocalAppData" && !File.Exists(Path.Combine(exe,marker)),"Return to LocalAppData was not persisted");
  Require(File.Exists(Path.Combine(exe,prefsName)) && File.Exists(Path.Combine(legacy,prefsName)),"Migration removed the original files");
  bool copyFailed=false;
  try{Switch("ExecutableDirectory",path=>throw new IOException("injected snapshot failure"));}
  catch(TargetInvocationException e)when(e.InnerException is IOException){copyFailed=true;}
  Require(copyFailed && Mode(storage)=="LocalAppData" && Mode(NewStorage(exe,local))=="LocalAppData","Failed snapshot enabled incomplete portable settings");
  // Exercise the real dropdown with fully isolated storage directories.
  using(var form=(MainForm)Activator.CreateInstance(typeof(MainForm),Private,null,new[]{store},null))
  {
   form.TopMost=false;form.Show();Application.DoEvents();
   var choice=Field<OverlayComboBox>(form,"storageSelector");
   Require(choice.Enabled && choice.SelectedIndex==0,"Storage choice was not initialized");
   choice.SelectedIndex=1;
   Require(Mode(storage)=="ExecutableDirectory" && File.ReadAllText(Path.Combine(exe,themesName)).Contains("BuiltInKey"),"UI did not move the complete palette catalog");
   Click(form,"btnSettings");Save(form,"storage-settings");
   Field<OverlayComboBox>(form,"languageSelector").SelectedIndex=0;Save(form,"storage-settings-ru");
   choice.SelectedIndex=0;form.Close();
  }
  Console.WriteLine("PASS storage dropdown, complete settings/theme/key copy, legacy fallback, restart/portable move and migration failure rollback; source data preserved.");
 }
}
