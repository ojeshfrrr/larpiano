using Keynote.Core;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
namespace Keynote.Midi;
public sealed class MidiImporter : IMidiImporter {
 public MidiSong Import(string path) {
  if (!new[]{".mid", ".midi"}.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a .mid or .midi file.");
  try {
   var file = MidiFile.Read(path);
   if(file.OriginalFormat == MidiFileFormat.MultiSequence) throw new InvalidDataException("MIDI format 2 contains independent sequences and is not supported.");
   var tempo=file.GetTempoMap();
   var tracks=file.GetTrackChunks().Select((chunk,index)=> {
    // A note-off pairs with the earliest open note of the same pitch and channel.
    // Reject unmatched events rather than silently dropping notes.
    var pending=new Dictionary<(int Channel,int Pitch),int>();
    foreach(var ev in chunk.Events) {
     if(ev is NoteOnEvent on && (int)on.Velocity>0) { var key=((int)on.Channel,(int)on.NoteNumber); pending[key]=pending.GetValueOrDefault(key)+1; }
     else if(ev is NoteOffEvent || ev is NoteOnEvent) { var note=(NoteEvent)ev; var key=((int)note.Channel,(int)note.NoteNumber); var count=pending.GetValueOrDefault(key); if(count==0) throw new InvalidDataException($"Track {index+1} has an unmatched note-off."); pending[key]=count-1; }
    }
    if(pending.Values.Any(count=>count>0)) throw new InvalidDataException($"Track {index+1} has notes without a note-off.");
    return new MidiTrack(index,chunk.GetNotes().Select(n=> {
     var start=TimeConverter.ConvertTo<MetricTimeSpan>(n.Time,tempo).TotalMicroseconds;
     var end=TimeConverter.ConvertTo<MetricTimeSpan>(n.Time+n.Length,tempo).TotalMicroseconds;
     return new MidiNote((int)n.NoteNumber,(int)n.Velocity,TimeSpan.FromMicroseconds(start),TimeSpan.FromMicroseconds(end-start),index);
    }).ToArray());
   }).ToArray();
   if(tracks.Length==0) throw new InvalidDataException("MIDI contains no tracks.");
   return new MidiSong(Path.GetFileNameWithoutExtension(path),tracks);
  } catch(Exception ex) when(ex is not IOException && ex is not InvalidDataException) { throw new InvalidDataException("Unable to parse MIDI: "+ex.Message,ex); }
 }
}
