using System.Security.Cryptography;

namespace Keynote.Online;

public sealed record ModernDownload(string Artist, string Title, Uri DownloadUri, string Sha256, string Source, int Octave);

public sealed class ModernDownloads(IMidiDownloadService downloads, IMidiLibraryService library) {
 public static readonly IReadOnlyList<ModernDownload> Available = Array.AsReadOnly(new[] {
  new ModernDownload("The Weeknd", "Blinding Lights", new Uri("https://free-piano-tutorials-midi.s3.amazonaws.com/The%20Weeknd%20-%20Blinding%20Lights/The%20Weeknd%20-%20Blinding%20Lights.mid"), "5a18fdae05c35021ca88f213c444712708136c4bb021112cd8fb8d0fe0180ad9", "https://www.freepianotutorials.net/2020/05/the-weeknd-blinding-lights-piano.html", 0),
  new ModernDownload("The Weeknd", "Call Out My Name", new Uri("https://free-piano-tutorials-midi.s3.amazonaws.com/The%20Weeknd%20-%20Call%20Out%20My%20Name/The%20Weeknd%20-%20Call%20Out%20My%20Name.mid"), "29375650e2a5d2a1e50d7ea55eddaa814f6c74cc4c1ff3a7834a81af6def2b33", "https://www.freepianotutorials.net/2020/06/the-weeknd-call-out-my-name-piano.html", 1)
  ,new ModernDownload("Lana Del Rey", "The Grants", new Uri("https://www.patreon.com/file?h=80164739&m=193520036"), "7a04ad9170359c27b282204c0d399ba5758b0b8530e5ceb1e157195225ca201d", "https://www.patreon.com/posts/lana-del-rey-80164739", 0)
  ,new ModernDownload("Lana Del Rey", "Candy Necklace", new Uri("https://www.patreon.com/file?h=80484800&m=194548695"), "e39368a775c49a267631e4dc4c764c2c60eafe4e3bbe2eae6a0ee33823f9403d", "https://www.patreon.com/posts/lana-del-rey-80484800", 0)
  ,new ModernDownload("Billie Eilish", "I Love You", new Uri("https://free-piano-tutorials-midi.s3.amazonaws.com/Billie%20Eilish%20-%20I%20Love%20You/Billie%20Eilish%20-%20I%20Love%20You.mid"), "5207d8691494ba0cb2e9bcc7a7cbe32c89ae24841dede7d476dcc81edf3461b1", "https://www.freepianotutorials.net/2020/03/billie-eilish-i-love-you-piano-tutorial.html", 0)
  ,new ModernDownload("Billie Eilish", "idontwannabeyouanymore", new Uri("https://free-piano-tutorials-midi.s3.amazonaws.com/Billie%20Eilish%20-%20idontwannabeyouanymore/Billie%20Eilish%20-%20idontwannabeyouanymore.mid"), "27436460658d88e52fc416a3c91c5d6e27aa198f30e4cade6c508d34abf7d17e", "https://www.freepianotutorials.net/2022/07/billie-eilish-idontwannabeyouanymore.html", 0)
 });
 public static ModernDownload? Find(string artist, string title) => Available.FirstOrDefault(x => x.Artist.Equals(artist, StringComparison.OrdinalIgnoreCase) && x.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
 public static bool IsApprovedEndpoint(Uri uri) => Available.Any(x => string.Equals(x.DownloadUri.AbsoluteUri, uri.AbsoluteUri, StringComparison.Ordinal));
 public static bool IsApprovedPatreonRedirect(Uri original, Uri redirected) {
  if(!IsApprovedEndpoint(original) || !original.Host.Equals("www.patreon.com",StringComparison.OrdinalIgnoreCase) || redirected.Scheme!=Uri.UriSchemeHttps || !redirected.IsDefaultPort || !string.IsNullOrEmpty(redirected.UserInfo) || !string.IsNullOrEmpty(redirected.Fragment) || !redirected.Host.Equals("c10.patreonusercontent.com",StringComparison.OrdinalIgnoreCase))return false;
  var post=original.Query.StartsWith("?h=80164739&",StringComparison.Ordinal)?"80164739":"80484800";
  return redirected.AbsolutePath.StartsWith("/4/patreon-media/p/post/"+post+"/",StringComparison.Ordinal) && redirected.AbsolutePath.EndsWith(".mid",StringComparison.OrdinalIgnoreCase);
 }
 public async Task<(LibraryEntry Entry, bool Duplicate)> DownloadAsync(ModernDownload item, IProgress<double>? progress, CancellationToken token) {
  if (!Available.Contains(item)) throw new InvalidOperationException("Unknown modern arrangement.");
  var bytes = await downloads.DownloadAsync(item.DownloadUri, progress, token);
  if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), item.Sha256, StringComparison.Ordinal)) throw new InvalidDataException("The creator's MIDI has changed; the download could not be verified.");
  token.ThrowIfCancellationRequested();
  var provider=item.DownloadUri.Host.Equals("www.patreon.com",StringComparison.OrdinalIgnoreCase)?"NC Pianist":"Free Piano Tutorials";
  return await library.AddAsync(bytes, new CatalogResult(provider, item.Sha256, item.Title, item.Artist, "Creator-provided original piano arrangement; verify the mapping before playback.", "Personal copy; check creator and rights holder terms", item.Source, RecommendedOctave: item.Octave), token);
 }
}
