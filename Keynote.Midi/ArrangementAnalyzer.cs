using Keynote.Core;
namespace Keynote.Midi;
public record ArrangementPreflight(int TotalNotes,int PlayableNotes,int MinPitch,int MaxPitch,int MaxConcurrent,int OverlappingNotes,IReadOnlyList<int> UnmappedPitches,IReadOnlyList<int> InvalidPitches,int ExcludedNotes);
public static class ArrangementAnalyzer {
 public static ArrangementPreflight Analyze(MidiSong song,KeyboardMapping mapping,int octave,IReadOnlyCollection<int> selectedTracks) {
  var all=song.Notes.ToArray(); var selected=song.Tracks.Where(t=>selectedTracks.Contains(t.Index)).SelectMany(t=>t.Notes).ToArray();
  var duplicates=mapping.Duplicates().ToHashSet(StringComparer.OrdinalIgnoreCase);
  var missing=selected.Select(n=>n.Pitch).Distinct().Where(p=>string.IsNullOrWhiteSpace(mapping.Lookup(p,octave))).Order().ToArray();
  var invalid=selected.Select(n=>n.Pitch).Distinct().Where(p=> { var key=mapping.Lookup(p,octave); return !string.IsNullOrWhiteSpace(key) && (!KeyboardMapping.IsValidKey(key) || duplicates.Contains(key.Trim())); }).Order().ToArray();
  var missingSet=missing.Concat(invalid).ToHashSet(); int playable=selected.Count(n=>!missingSet.Contains(n.Pitch));
  var edges=selected.SelectMany(n=>new[]{(Time:n.Start,Delta:1),(Time:n.Start+(n.Duration>TimeSpan.Zero?n.Duration:TimeSpan.FromTicks(1)),Delta:-1)}).OrderBy(e=>e.Time).ThenBy(e=>e.Delta); int active=0,max=0; foreach(var edge in edges) { active+=edge.Delta; max=Math.Max(max,active); }
  int overlaps=0; foreach(var group in selected.GroupBy(n=>n.Pitch)) { var ends=new PriorityQueue<TimeSpan,TimeSpan>(); foreach(var note in group.OrderBy(n=>n.Start)) { while(ends.TryPeek(out var end,out _) && end<=note.Start)ends.Dequeue(); if(ends.Count>0)overlaps++; var stop=note.Start+(note.Duration>TimeSpan.Zero?note.Duration:TimeSpan.FromTicks(1)); ends.Enqueue(stop,stop); } }
  return new(selected.Length,playable,selected.Length==0?0:selected.Min(n=>n.Pitch),selected.Length==0?0:selected.Max(n=>n.Pitch),max,overlaps,missing,invalid,all.Length-selected.Length);
 }
}
