using Keynote.Core;
using Melanchall.DryWetMidi.Core;
namespace Keynote.Midi;
public record TrackInspection(int Index,int Notes,string Instruments,bool NonPiano) { public override string ToString()=>$"Track {Index+1}: {Notes} notes; {Instruments}"; }
public record MidiInspection(MidiSong Song,IReadOnlyList<TrackInspection> Tracks,string Tempos,int MinPitch,int MaxPitch,int MaxChord,string Unsupported,bool PianoCandidate) {
 public string Summary=>$"{Tracks.Count} tracks • {Song.Notes.Count()} notes • {(int)Song.Duration.TotalMinutes}:{Song.Duration.Seconds:00}\nPitch: {MinPitch}–{MaxPitch} • Max concurrent notes: {MaxChord}\nTempo: {Tempos}\nUnsupported: {Unsupported}";
}
public sealed class MidiInspector(IMidiImporter importer) {
 public MidiInspection Inspect(string path) {
  var song=importer.Import(path); var file=MidiFile.Read(path); var chunks=file.GetTrackChunks().ToArray(); var tempos=new List<double>(); var channels=new HashSet<int>(); var unsupported=new HashSet<string>(); var tracks=new List<TrackInspection>();
  for(int i=0;i<chunks.Length;i++) { var programs=new HashSet<int>(); bool percussion=false; foreach(var ev in chunks[i].Events) { if(ev is ProgramChangeEvent p)programs.Add((int)p.ProgramNumber+1); if(ev is NoteOnEvent on && (int)on.Velocity>0) { channels.Add((int)on.Channel); if((int)on.Channel==9)percussion=true; } if(ev is SetTempoEvent t && t.MicrosecondsPerQuarterNote>0)tempos.Add(60000000d/t.MicrosecondsPerQuarterNote); if(ev is ControlChangeEvent)unsupported.Add("controllers / sustain"); if(ev is PitchBendEvent)unsupported.Add("pitch bend"); if(ev is NoteAftertouchEvent or ChannelAftertouchEvent)unsupported.Add("aftertouch"); if(ev is NormalSysExEvent or EscapeSysExEvent)unsupported.Add("system exclusive messages"); }
   var instruments=programs.Count==0?"No program change in this track; channel program may be set elsewhere":"GM programs "+string.Join(", ",programs.Order().Select(p=>$"{p} ({System.Text.RegularExpressions.Regex.Replace(((Melanchall.DryWetMidi.Standards.GeneralMidiProgram)(p-1)).ToString(), "(?<!^)([A-Z])", " $1")})")); if(percussion)instruments+=", percussion channel"; tracks.Add(new(i,song.Tracks[i].Notes.Count,instruments,percussion || programs.Any(p=>p>8))); }
  var notes=song.Notes.ToArray(); if(notes.Length>0)unsupported.Add("velocity dynamics"); var min=notes.Length==0?0:notes.Min(n=>n.Pitch); var max=notes.Length==0?0:notes.Max(n=>n.Pitch); var edges=notes.SelectMany(n=>new[]{(Time:n.Start,Delta:1),(Time:n.Start+(n.Duration>TimeSpan.Zero?n.Duration:TimeSpan.FromTicks(1)),Delta:-1)}).OrderBy(e=>e.Time).ThenBy(e=>e.Delta); int active=0,chord=0; foreach(var edge in edges) { active+=edge.Delta; chord=Math.Max(chord,active); }
  return new(song,tracks,tempos.Count==0?"120 BPM (default)":string.Join(", ",tempos.Distinct().Take(10).Select(t=>$"{t:F1} BPM")),min,max,chord,unsupported.Count==0?"none detected":string.Join(", ",unsupported),tracks.Count(t=>t.Notes>0)==1 && channels.Count==1 && tracks.All(t=>!t.NonPiano));
 }
}

