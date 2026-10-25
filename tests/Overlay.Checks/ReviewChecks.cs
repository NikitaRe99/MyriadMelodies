using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MyriadMelodies;
using MyriadMelodies.Controls;

partial class OverlayChecks
{
 static void RunReviewChecks()
 {
  Type catalog=typeof(MainForm).Assembly.GetType("MyriadMelodies.ColorPaletteCatalog");
  Type styleType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ColorPaletteStyle");
  Type flashType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ThemeFlashMode");
  Type editorType=typeof(MainForm).Assembly.GetType("MyriadMelodies.ThemeEditDialog");
  foreach(var test in new[]{("White",true),("Colored",false)})
  {
   var flash=(Color)catalog.GetMethod("GetFlashColor").Invoke(null,new[]{Enum.Parse(flashType,test.Item1)});
   object style=Activator.CreateInstance(styleType,new object[]{"custom behavior",Color.Red,Color.Blue,flash,test.Item2,true,null});
   using var editor=(Form)Activator.CreateInstance(editorType,new object[]{style});
   ((OverlayTextBox)editorType.GetField("nameInput",Private).GetValue(editor)).Text="renamed only";
   Require((bool)editorType.GetProperty("UsesStandardInput").GetValue(editor)==test.Item2,"Renaming changed the theme's input behavior");
   Require((Color)editorType.GetProperty("FlashColor").GetValue(editor)==flash,"Renaming changed the flash color");
  }
  Type uiText=typeof(MainForm).Assembly.GetType("MyriadMelodies.UiText");
  uiText.GetMethod("SetLanguage").Invoke(null,new object[]{"en"});
  var styles=(System.Collections.IList)catalog.GetProperty("Styles").GetValue(null);
  object builtIn=styles[0];_=builtIn.ToString();
  long beforeNames=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++)_=builtIn.ToString();
  long nameBytes=GC.GetAllocatedBytesForCurrentThread()-beforeNames;
  uiText.GetMethod("SetLanguage").Invoke(null,new object[]{"ru"});
  using(var area=new MyriadMelodies.FormElementCreator().CreatePreviewArea("A",1))
  {
   area.Update(Color.Red,false);
   long beforePreview=GC.GetAllocatedBytesForCurrentThread();
   for(int i=0;i<10000;i++)area.Update(Color.Red,false);
   long previewBytes=GC.GetAllocatedBytesForCurrentThread()-beforePreview;
   Require(nameBytes<1024 && previewBytes<1024,"Unchanged UI values still allocate for every refresh");
   Console.WriteLine($"UI allocations, 10,000 unchanged updates: theme names={nameBytes} B, preview indicators={previewBytes} B.");
  }
  using(var host=new Form())using(var combo=new OverlayComboBox())
  {
   host.Controls.Add(combo);combo.Items.AddRange(new object[]{"A","B","C"});combo.SelectedIndex=1;
   combo.Items.Insert(0,"X");
   Require(combo.SelectedIndex==2 && combo.SelectedItem.Equals("B") && combo.Text=="B","Inserting a row changed the selected item");
   combo.Items[2]="translated B";
   Require(combo.Text=="translated B","Replacing the selected row left stale text");
   int count=combo.Items.Count;bool failed=false;
   try{combo.MoveItem(0,count);}catch(ArgumentOutOfRangeException){failed=true;}
   Require(failed && combo.Items.Count==count && combo.Items[0].Equals("X"),"Invalid move corrupted the dropdown contents");
  }
  using(var form=CreateForm())
  {
   Require(!Field<Timer>(form,"captureSourceTimer").Enabled,"Idle form still polls capture sources");
   form.Close();
  }
  if(File.Exists(SettingsPath))File.Delete(SettingsPath);
  Console.WriteLine("PASS rename preserves custom input behavior, selected-item insertion/replacement and invalid-move rollback; idle source polling stopped.");
 }
}
