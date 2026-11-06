namespace Keynote.Online;

public static class ModernSongMatching {
 public static RemappedSong? FindShared(string artist, string title, IEnumerable<RemappedSong> songs) {
  var candidates=songs.Where(s=>s.Artist.Equals(artist,StringComparison.OrdinalIgnoreCase)).ToArray();
  var exact=candidates.Where(s=>s.Title.Equals(title,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(exact.Length==1)return exact[0];
  if(exact.Length>1)return null;
  var variants=candidates.Where(s=>s.Title.StartsWith(title+" (",StringComparison.OrdinalIgnoreCase) || s.Title.StartsWith(title+" [",StringComparison.OrdinalIgnoreCase)).ToArray();
  return variants.Length==1?variants[0]:null;
 }
}
