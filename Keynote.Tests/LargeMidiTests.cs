using Keynote.Midi;
namespace Keynote.Tests;
public class LargeMidiTests {
 [Fact] public void ImportsAndInspectsTwentyThousandNotes() {
  const int count=20000; var track=new MemoryStream(); for(int i=0;i<count;i++) { track.Write(new byte[]{0,0x90,60,100,1,0x80,60,0}); } track.Write(new byte[]{0,0xff,0x2f,0}); var data=track.ToArray(); using var file=new MemoryStream(); file.Write(new byte[]{77,84,104,100,0,0,0,6,0,0,0,1,0,96,77,84,114,107}); file.Write(new[]{(byte)(data.Length>>24),(byte)(data.Length>>16),(byte)(data.Length>>8),(byte)data.Length}); file.Write(data); var path=Path.Combine(Path.GetTempPath(),"keynote-large-"+Guid.NewGuid().ToString("N")+".mid"); try { File.WriteAllBytes(path,file.ToArray()); var inspection=new MidiInspector(new MidiImporter()).Inspect(path); Assert.Equal(count,inspection.Song.Notes.Count()); Assert.Equal(1,inspection.MaxChord); } finally { File.Delete(path); }
 }
}
