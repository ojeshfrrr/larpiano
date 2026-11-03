using Keynote.Core;
using Keynote.Midi;
using Keynote.Playback;
namespace Keynote.Tests;
public class MidiFixtureTests {
 static MidiSong Load(string name) => new MidiImporter().Import(Path.Combine(AppContext.BaseDirectory,"Fixtures",name));
 [Fact] public void IndependentScaleHasExpectedPitchesAndDurations() {
  var song=Load("test-c-major-scale.mid"); var notes=song.Notes.OrderBy(n=>n.Start).ToArray();
  Assert.Single(song.Tracks); Assert.Equal(new[]{60,62,64,65,67,69,71,72},notes.Select(n=>n.Pitch));
  for(int i=0;i<notes.Length;i++) { Assert.Equal(i*.5,notes[i].Start.TotalSeconds,5); Assert.Equal(.5,notes[i].Duration.TotalSeconds,5); }
 }
 [Fact] public void IndependentTracksAreSynchronized() {
  var song=Load("test-2-tracks-type-1.mid"); Assert.Equal(2,song.Tracks.Count); Assert.Equal(16,song.Notes.Count());
  Assert.All(song.Tracks,t=>Assert.Equal(.5,t.Notes.First().Start.TotalSeconds,5));
  Assert.Equal(new[]{60,61},song.Notes.Where(n=>n.Start==TimeSpan.FromSeconds(.5)).Select(n=>n.Pitch).Order());
 }
 [Fact] public void IndependentChordFilePreservesSimultaneousTracks() {
  var song=Load("test-multichannel-chords-1.mid"); Assert.Equal(3,song.Tracks.Count); Assert.Equal(24,song.Notes.Count());
  Assert.Equal(new[]{60,64,67},song.Notes.Where(n=>n.Start==TimeSpan.Zero).Select(n=>n.Pitch).Order());
 }
 [Fact] public void ImportedMultiTrackChordProducesMatchingPlaybackEvents() {
  var song=Load("test-multichannel-chords-1.mid"); var pitches=song.Notes.Select(n=>n.Pitch).Distinct().Order().ToArray();
  const string keys="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"; Assert.True(pitches.Length<=keys.Length);
  var mapping=new KeyboardMapping { Keys=pitches.Select((p,i)=>(p,i)).ToDictionary(x=>x.p,x=>keys[x.i].ToString()) };
  var events=PlaybackEngine.BuildEvents(song,mapping); Assert.Equal(song.Notes.Count()*2,events.Length);
  Assert.Equal(3,events.Count(e=>e.Time==TimeSpan.Zero && e.IsDown));
  foreach(var note in song.Notes) { var key=mapping.Lookup(note.Pitch)!; Assert.Contains(events,e=>e.Key==key && e.IsDown && e.Time==note.Start); Assert.Contains(events,e=>e.Key==key && !e.IsDown && e.Time==note.Start+note.Duration); }
 }
 [Fact] public void IndependentTempoMapChangesNoteLengths() {
  var song=Load("test-tempo-events-track-smf1.mid"); var notes=song.Notes.OrderBy(n=>n.Start).ToArray(); Assert.Equal(16,notes.Length);
  Assert.Equal(.6,notes[0].Duration.TotalSeconds,4); Assert.Equal(.5,notes[4].Duration.TotalSeconds,4); Assert.Equal(60d/140,notes[8].Duration.TotalSeconds,4); Assert.Equal(.375,notes[12].Duration.TotalSeconds,4);
  Assert.Equal(2.4,notes[4].Start.TotalSeconds,4); Assert.Equal(4.4,notes[8].Start.TotalSeconds,4);
 }
 [Fact] public void OverlappingSamePitchNotesArePairedInStartOrder() {
  byte[] b={77,84,104,100,0,0,0,6,0,0,0,1,0,96,77,84,114,107,0,0,0,20, 0,0x90,60,100, 48,0x90,60,90, 48,0x80,60,0, 48,0x80,60,0, 0,0xff,0x2f,0};
  WithFile(b,p=> { var notes=new MidiImporter().Import(p).Notes.OrderBy(n=>n.Start).ToArray(); Assert.Equal(2,notes.Length); Assert.Equal(0,notes[0].Start.TotalSeconds,5); Assert.Equal(.5,notes[0].Duration.TotalSeconds,5); Assert.Equal(.25,notes[1].Start.TotalSeconds,5); Assert.Equal(.5,notes[1].Duration.TotalSeconds,5); Assert.Equal(2,new MidiInspector(new MidiImporter()).Inspect(p).MaxChord); });
 }
 [Fact] public void InspectorNamesGeneralMidiPrograms() { byte[] b={77,84,104,100,0,0,0,6,0,0,0,1,0,96,77,84,114,107,0,0,0,15,0,0xc0,1,0,0x90,60,100,96,0x80,60,0,0,0xff,0x2f,0}; WithFile(b,p=> { var report=new MidiInspector(new MidiImporter()).Inspect(p); Assert.Contains("Bright Acoustic Piano",report.Tracks[0].Instruments); }); }
 [Fact] public void UnmatchedNoteIsRejected() { byte[] b={77,84,104,100,0,0,0,6,0,0,0,1,0,96,77,84,114,107,0,0,0,8,0,0x90,60,100,0,0xff,0x2f,0}; WithFile(b,p=>Assert.Throws<InvalidDataException>(()=>new MidiImporter().Import(p))); }
 [Fact] public void IndependentSequencesAreRejected() { var data=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","test-2-tracks-type-1.mid")); data[9]=2; WithFile(data,p=>Assert.Throws<InvalidDataException>(()=>new MidiImporter().Import(p))); }
 static void WithFile(byte[] bytes,Action<string> action) { var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".mid"); try { File.WriteAllBytes(path,bytes); action(path); } finally { File.Delete(path); } }
}
