using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using MyriadMelodies;
class Checks
{
 static void Main(string[] args)
 {
  using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"observations.json")));
  var output = new System.Collections.Generic.List<object>();
  double maximum = 0;
  foreach (var row in fixture.RootElement.EnumerateArray())
  {
   int w=row.GetProperty("width").GetInt32(), h=row.GetProperty("height").GetInt32();
   bool spread=row.GetProperty("spread").GetBoolean();
   var bounds=new Rectangle(0,0,w,h);
   Rectangle[] regions=CaptureLayout.CreateRegions(bounds,spread);
   var translated=CaptureLayout.CreateRegions(new Rectangle(-w,137,w,h),spread);
   var points=row.GetProperty("points");
   double worst=0;
   for(int i=0;i<6;i++)
   {
    double dx=regions[i].X+regions[i].Width/2.0-points[i].GetProperty("x").GetDouble();
    double dy=regions[i].Y+regions[i].Height/2.0-points[i].GetProperty("y").GetDouble();
    double error=Math.Sqrt(dx*dx+dy*dy);
    worst=Math.Max(worst,error);
    if(error>1.5) throw new Exception($"Frame {row.GetProperty("number")}, key {i}: {error:F3} px");
    if(translated[i].X!=regions[i].X-w || translated[i].Y!=regions[i].Y+137)
      throw new Exception("Monitor origin translation failed");
    if(!bounds.Contains(regions[i])) throw new Exception("Region outside field");
   }
   maximum=Math.Max(maximum,worst);
   output.Add(new { number=row.GetProperty("number").GetInt32(), width=w,height=h,spread,
    maxError=worst,regions});
   Console.WriteLine($"{w}x{h}, {(spread?"spread":"vertical")}: max center error {worst:F3} px");
  }
  // Переходы между опорными пропорциями и высокие разрешения.
  foreach(int h in new[]{720,1080,2160,8640})
   foreach(double aspect in new[]{4.0/3,1.4,1.6,1.7,16.0/9,21.0/9,32.0/9})
    foreach(bool spread in new[]{false,true})
     CaptureLayout.CreateRegions(new Rectangle(0,0,(int)Math.Round(h*aspect),h),spread);
  try { CaptureLayout.CreateRegions(new Rectangle(0,0,0,1080),false); throw new Exception("Empty field accepted"); }
  catch(ArgumentOutOfRangeException) {}
  try { CaptureLayout.CreateRegions(new Rectangle(0,0,900,1600),false); throw new Exception("Unmeasured portrait accepted"); }
  catch(ArgumentException) {}
  if(args.Length>0) File.WriteAllText(args[0],JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"PASS: 60 measured points; max error {maximum:F3} px; monitor offsets and geometry bounds checked.");
 }
}
