using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Keynote.Core;
namespace Keynote.App;
public sealed class PianoRoll : FrameworkElement {
 MidiSong? song; MidiNote[] notes=Array.Empty<MidiNote>(); int min,max; double durationSeconds;
 public MidiSong? Song { get=>song; set { song=value; notes=value?.Notes.ToArray() ?? Array.Empty<MidiNote>(); min=notes.Length==0?0:notes.Min(n=>n.Pitch); max=notes.Length==0?0:notes.Max(n=>n.Pitch); durationSeconds=notes.Length==0?0:notes.Max(n=>(n.Start+n.Duration).TotalSeconds); } }
 public KeyboardMapping? Mapping { get; set; } public int Octave { get; set; } public TimeSpan Position { get; set; }
 static readonly Brush Mapped=MakeBrush(167,139,250),Unmapped=MakeBrush(224,108,126),Label=MakeBrush(162,157,181); static readonly Pen GridPen=MakePen(50,46,65,.5),CursorPen=MakePen(242,237,255,1);
 static Brush MakeBrush(byte r,byte g,byte b) { var brush=new SolidColorBrush(Color.FromRgb(r,g,b)); brush.Freeze(); return brush; }
 static Pen MakePen(byte r,byte g,byte b,double width) { var pen=new Pen(MakeBrush(r,g,b),width); pen.Freeze(); return pen; }
 protected override void OnRender(DrawingContext dc) { base.OnRender(dc); double w=ActualWidth,h=ActualHeight; if(song==null || notes.Length==0 || w<60 || h<40)return; const double left=36,top=18; double chartW=w-left,chartH=h-top,duration=Math.Max(.001,durationSeconds),row=chartH/(max-min+1); var dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
  FormattedText Text(string text)=>new(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,Label,dpi);
  for(int i=0;i<=4;i++) {double x=left+chartW*i/4; dc.DrawLine(GridPen,new Point(x,top),new Point(x,h)); var time=TimeSpan.FromSeconds(duration*i/4); dc.DrawText(Text($"{(int)time.TotalMinutes}:{time.Seconds:00}"),new Point(Math.Min(x,w-30),1));}
  for(int p=min;p<=max;p++) { double y=h-(p-min+1)*row; if(p%12==0) {dc.DrawLine(GridPen,new Point(left,y),new Point(w,y)); if(row>=2)dc.DrawText(Text(Notes.Name(p)),new Point(2,Math.Clamp(y,top,h-12)));} }
  foreach(var n in notes) { double x=left+n.Start.TotalSeconds/duration*chartW,width=Math.Max(1,n.Duration.TotalSeconds/duration*chartW),y=h-(n.Pitch-min+1)*row; dc.DrawRectangle(Mapping?.Lookup(n.Pitch,Octave)==null?Unmapped:Mapped,null,new Rect(x,y,width,Math.Max(1,row-1))); }
  double cursor=left+Math.Clamp(Position.TotalSeconds/duration,0,1)*chartW; dc.DrawLine(CursorPen,new Point(cursor,top),new Point(cursor,h));
 }
}
