using System;
using System.Collections;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using MyriadMelodies;
using MyriadMelodies.Controls;

partial class OverlayChecks
{
 static void RunNewFeatures()
 {
  var assembly=typeof(MainForm).Assembly;
  Type preferences=assembly.GetType("MyriadMelodies.OverlayPreferences");
  Type storeType=assembly.GetType("MyriadMelodies.OverlaySettingsStore");
  var previous=CultureInfo.CurrentUICulture;
  foreach(var test in new[]{("ru-RU","ru"),("en-US","en"),("de-DE","en"),("ru-BY","ru")})
  {
   CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(test.Item1);
   string path=SettingsPath+"-language";
   if(File.Exists(path))File.Delete(path);
   object store=Activator.CreateInstance(storeType,new object[]{path});
   object loaded=storeType.GetMethod("Load").Invoke(store,null);
   Require((string)preferences.GetProperty("Language").GetValue(loaded)==test.Item2,"Wrong first-run language: "+test.Item1);
   File.WriteAllText(path,"{\"ColorTolerance\":27}");
   loaded=storeType.GetMethod("Load").Invoke(store,null);
   Require((string)preferences.GetProperty("Language").GetValue(loaded)==test.Item2,"Old preferences ignored system language");
   File.WriteAllText(path,"{\"Language\":\"ru\"}");
   loaded=storeType.GetMethod("Load").Invoke(store,null);
   Require((string)preferences.GetProperty("Language").GetValue(loaded)=="ru","Saved language was overridden by system");
   File.Delete(path);
  }
  CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("ru-RU");
  using(var form=CreateForm())
  {
   form.TopMost=false;form.Show();Application.DoEvents();
   var language=Field<OverlayComboBox>(form,"languageSelector");
   language.SelectedIndex=1;
   Require(Field<Label>(form,"themeLabel").Text=="Theme","English labels not applied");
   Require(Field<Button>(form,"btnStartStop").Text.Contains("Start"),"English capture state not applied");
   var mode=Field<OverlayComboBox>(form,"domainUpDownMode");
   Require((string)mode.Items[0]=="Vertical mode","English mode names not applied");
   Require(mode.Text=="Vertical mode","Current mode text used by theme preparation kept the old language");
   var palette=Field<OverlayComboBox>(form,"comboStyle");
   Require(palette.Items[0].ToString()=="Undulating Ripples","Official theme name not applied");
   Click(form,"btnSettings");Click(form,"btnShow");Save(form,"english");
   Type editorType=assembly.GetType("MyriadMelodies.ThemeEditDialog");
   using(var editor=(Form)Activator.CreateInstance(editorType,new object[]{palette.Items[0]}))
   {
    editor.TopMost=false;editor.Show(form);Application.DoEvents();Save(editor,"english-theme-editor");
    Require(((OverlayTextBox)editorType.GetField("nameInput",Private).GetValue(editor)).Text=="Undulating Ripples","Editor did not show translated builtin name");
    Require((string)editorType.GetProperty("ThemeName").GetValue(editor)==(string)palette.Items[0].GetType().GetProperty("Name").GetValue(palette.Items[0]),"Unedited English name would overwrite stored identity");
    editor.Close();
   }
   Type preparationType=assembly.GetType("MyriadMelodies.ThemeCapturePreparationDialog");
   using(var dialog=(Form)Activator.CreateInstance(preparationType,new object[]{"Vertical mode"}))
   {dialog.TopMost=false;dialog.Show(form);Application.DoEvents();Save(dialog,"english-preparation");dialog.Close();}
   foreach(Control card in Field<FlowLayoutPanel>(form,"demonstratPanel").Controls)
    Require(card.Controls["colorPanel"] is ColorSwatch && card.Controls["previwAction"] is ColorSwatch,"Zone indicators are not round swatches");
   foreach(string caption in new[]{"captureStatus","themeLabel","settingsPanel"})
   {
    var control=Field<Control>(form,caption);
    typeof(Control).GetMethod("OnMouseDown",Private).Invoke(control,new object[]{new MouseEventArgs(MouseButtons.Left,1,2,2,0)});
    Require(Field<bool>(form,"dragging"),"Cannot drag from "+caption);
    typeof(Control).GetMethod("OnMouseUp",Private).Invoke(control,new object[]{new MouseEventArgs(MouseButtons.Left,1,2,2,0)});
    Require(!Field<bool>(form,"dragging"),"Drag did not release");
   }
   language.SelectedIndex=0;Require(Field<Label>(form,"themeLabel").Text=="Тема","Russian switch-back failed");
   Require(mode.Text=="Вертикальный режим","Current mode text was not restored to Russian");
   language.SelectedIndex=1;form.Close();
  }
  using(var restored=CreateForm())
  {Require(Field<OverlayComboBox>(restored,"languageSelector").SelectedIndex==1,"Chosen language not restored");restored.Close();}
  File.Delete(SettingsPath);
  CultureInfo.CurrentUICulture=previous;

  Type catalog=assembly.GetType("MyriadMelodies.ColorPaletteCatalog");
  Type style=assembly.GetType("MyriadMelodies.ColorPaletteStyle");
  var styles=(IList)catalog.GetField("MutableStyles",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  Require(styles.Count==25,"Debug themes were not promoted to 25 defaults");
  foreach(object item in styles)Require(!(bool)style.GetProperty("IsCustom").GetValue(item),"Promoted theme is still custom");
  var original=(IList)Activator.CreateInstance(styles.GetType());foreach(object item in styles)original.Add(item);
  string orderPath=SettingsPath+"-order";
  catalog.GetMethod("MoveStyle").Invoke(null,new object[]{24,0,orderPath});
  var loadedOrder=(IList)Activator.CreateInstance(styles.GetType());foreach(object item in original)loadedOrder.Add(item);
  catalog.GetMethod("LoadCustomStyles",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{loadedOrder,orderPath});
  Require(Equals(style.GetProperty("BuiltInKey").GetValue(loadedOrder[0]),style.GetProperty("BuiltInKey").GetValue(original[24])) && loadedOrder.Count==25,"Theme order was not restored or defaults duplicated");
  Require((int)catalog.GetMethod("GetDefaultStyleIndex").Invoke(null,null)==5,"Default selection changed after reorder");
  string blocked=orderPath+"-directory";Directory.CreateDirectory(blocked);
  bool failed=false;
  try{catalog.GetMethod("MoveStyle").Invoke(null,new object[]{0,1,blocked});}
  catch(TargetInvocationException e)when(e.InnerException is IOException || e.InnerException is UnauthorizedAccessException){failed=true;}
  Require(failed && ReferenceEquals(styles[0],original[24]),"Reorder rollback failed");
  catalog.GetMethod("MoveStyle").Invoke(null,new object[]{0,24,orderPath});
  // Legacy custom records now supplied as built-ins must merge without duplicates.
  string promotedPath=SettingsPath+"-promoted.json";
  File.WriteAllText(promotedPath,System.Text.Json.JsonSerializer.Serialize(new[]{new{Name="Оракул разума (лучше не использовать)",ShortPressArgb=Color.Cyan.ToArgb(),LongPressArgb=Color.Blue.ToArgb(),LongPressFlashArgb=Color.White.ToArgb()}}));
  catalog.GetMethod("LoadCustomStyles",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{loadedOrder,promotedPath});
  Require(loadedOrder.Count==25,"Legacy debug profile duplicated new defaults");
  foreach(var sample in new[]{(17,Color.FromArgb(254,252,211)),(24,Color.FromArgb(220,222,252))})
   Require(((Color)style.GetProperty("LongPressFlashColor").GetValue(original[sample.Item1])).ToArgb()==sample.Item2.ToArgb(),"Video flash color mismatch");
  using(var host=new Form())using(var combo=new OverlayComboBox{AllowItemReorder=true})
  {
   host.Controls.Add(combo);combo.Items.AddRange(new object[]{"A","B","C"});combo.SelectedIndex=1;
   combo.ItemMoveRequested+=(s,e)=>combo.MoveItem(e.From,e.To);
   host.Show();combo.DroppedDown=true;Application.DoEvents();
   var list=(Control)typeof(OverlayComboBox).GetField("popupList",Private).GetValue(combo);
   list.GetType().GetMethod("OnKeyDown",Private).Invoke(list,new object[]{new KeyEventArgs(Keys.Alt|Keys.Down)});
   Require(combo.SelectedItem.Equals("B") && combo.SelectedIndex==2,"Keyboard reorder changed selected theme");
   int rowHeight=(int)list.GetType().GetField("rowHeight",Private).GetValue(list);
   list.GetType().GetMethod("OnMouseDown",Private).Invoke(list,new object[]{new MouseEventArgs(MouseButtons.Left,1,50,rowHeight/2,0)});
   list.GetType().GetMethod("OnMouseMove",Private).Invoke(list,new object[]{new MouseEventArgs(MouseButtons.Left,0,50,rowHeight+rowHeight/2,0)});
   list.GetType().GetMethod("OnMouseUp",Private).Invoke(list,new object[]{new MouseEventArgs(MouseButtons.Left,1,50,rowHeight+rowHeight/2,0)});
   Require(combo.Items[1].Equals("A") && combo.SelectedItem.Equals("B"),"Drag reorder failed");
   combo.DroppedDown=false;host.Close();
  }
  Console.WriteLine("PASS system/saved language, live RU/EN translation, official theme names, circle indicators, broader drag areas, 25 built-ins, legacy promotion, persistent order, rollback and drag/keyboard reorder.");
 }
}
