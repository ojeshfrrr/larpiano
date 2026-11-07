namespace Keynote.Core;

public static partial class PreparedSongMappings {
 const string CantHelpHash="eb78ac1bac84f3cc6030e4075f78845a2a0c37d01286047a439e4e4141da2e44";
 static readonly int[] CantHelpChanges={8,82,37,53,51,51,70,56,113,82,119,57,123,79,137,51,258,62,310,82,316,57,320,79,345,47,358,37,376,45,381,48,438,43,462,58,470,51,473,47,516,63,567,43,595,57,596,69,607,63,612,47,623,61,679,58,713,47,725,47,752,47,804,63,901,51,964,93,991,49,996,47,1085,61,1133,47,1145,45,1167,63,1176,63,1180,63,1199,57,1200,69,1289,44,1291,68,1297,43,1299,55,1317,86,1318,88,1319,90,1320,93,1321,95,1324,43};
 public static MidiSong Apply(string sha256,MidiSong song) {
  int count; int[] changes;
  if(string.Equals(sha256,CantHelpHash,StringComparison.OrdinalIgnoreCase)){count=1325;changes=CantHelpChanges;}
  else if(Additional.TryGetValue(sha256,out var mapping)){count=mapping.Count;changes=mapping.Changes;}
  else if(More.TryGetValue(sha256,out var more)){count=more.Count;changes=more.Changes;}
  else return song;
  var ordered=song.Notes.OrderBy(n=>n.Start).ThenBy(n=>n.Track).ThenBy(n=>n.Pitch).ToArray();
  if(ordered.Length!=count)throw new InvalidDataException("The song no longer matches its prepared mapping.");
  for(int i=0;i<changes.Length;i+=2){int index=changes[i], pitch=changes[i+1];var note=ordered[index];if((pitch-note.Pitch)%12!=0)throw new InvalidDataException("The song no longer matches its prepared mapping.");ordered[index]=note with { Pitch=pitch };}
  var byTrack=ordered.GroupBy(n=>n.Track).ToDictionary(g=>g.Key,g=>(IReadOnlyList<MidiNote>)g.ToArray());
  return song with { Tracks=song.Tracks.Select(t=>t with { Notes=byTrack.GetValueOrDefault(t.Index) ?? Array.Empty<MidiNote>() }).ToArray() };
 }
}
