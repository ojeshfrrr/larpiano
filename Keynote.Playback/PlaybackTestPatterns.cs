using Keynote.Core;
namespace Keynote.Playback;
public enum PlaybackTestKind { SingleNote,FiveNoteMelody,ThreeNoteChord,Tempo,PauseResume,EmergencyStop }
public static class PlaybackTestPatterns {
 public static MidiSong Create(PlaybackTestKind kind,IReadOnlyList<int> pitches) {
  if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));
  int required=kind switch { PlaybackTestKind.FiveNoteMelody or PlaybackTestKind.Tempo=>5, PlaybackTestKind.ThreeNoteChord=>3, _=>1 };
  if(pitches.Distinct().Count()<required || pitches.Any(p=>p is <0 or >127))throw new InvalidOperationException($"This test requires {required} distinct mapped MIDI pitches.");
  var selected=pitches.Distinct().Take(required).ToArray(); var notes=new List<MidiNote>();
  if(kind==PlaybackTestKind.ThreeNoteChord) foreach(var pitch in selected)notes.Add(new(pitch,100,TimeSpan.Zero,TimeSpan.FromMilliseconds(750),0));
  else if(kind is PlaybackTestKind.FiveNoteMelody or PlaybackTestKind.Tempo) for(int i=0;i<selected.Length;i++)notes.Add(new(selected[i],100,TimeSpan.FromMilliseconds(i*500),TimeSpan.FromMilliseconds(350),0));
  else notes.Add(new(selected[0],100,TimeSpan.Zero,TimeSpan.FromSeconds(kind is PlaybackTestKind.PauseResume or PlaybackTestKind.EmergencyStop ? 15 : .75),0));
  return new($"Test: {kind}",new[]{new MidiTrack(0,notes)});
 }
}
