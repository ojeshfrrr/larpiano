using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
namespace Keynote.Online;

public sealed record RemappedSong(string Sha256,string Title,string Artist,string Source,string License,string Profile,int Octave,int KeptNotes,int ExcludedNotes,int NotesAdjusted=0,string Attribution="",string Modification="") {
 public override string ToString()=>$"{Title}  •  {Artist}";
}

public sealed class RemappedCatalogService(HttpClient http, IMidiDownloadService downloads) {
 public static readonly Uri CatalogUri=new("https://larpiano.portways.app/api/remapped/songs");
 const int MaxCatalogBytes=1024*1024;
 static bool ValidHash(string? hash)=>hash is {Length:64} && hash.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
 static bool ValidText(string? text,int max)=>!string.IsNullOrWhiteSpace(text) && text.Length<=max && !text.Any(char.IsControl);
 static bool ValidSource(string? source)=>Uri.TryCreate(source,UriKind.Absolute,out var uri) && uri.Scheme==Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && (uri.Host.Equals("www.mutopiaproject.org",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("mutopiaproject.org",StringComparison.OrdinalIgnoreCase));
 static void Validate(RemappedSong item) {
  if(!ValidHash(item.Sha256) || !ValidText(item.Title,200) || !ValidText(item.Artist,200) || !ValidText(item.License,200) || !ValidText(item.Profile,200) || !ValidSource(item.Source) || item.Octave is < -2 or > 2 || item.KeptNotes<1 || item.ExcludedNotes<0 || item.NotesAdjusted<0 || item.Attribution is null || item.Attribution.Length>500 || item.Modification is null || item.Modification.Length>500)throw new InvalidDataException("The remapped song catalogue contains invalid metadata.");
 }
 public async Task<IReadOnlyList<RemappedSong>> ListAsync(CancellationToken token) {
  using var response=await http.GetAsync(CatalogUri,HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>MaxCatalogBytes)throw new InvalidDataException("The remapped song catalogue is too large.");
  await using var source=await response.Content.ReadAsStreamAsync(token); using var data=new MemoryStream(); var buffer=new byte[8192]; int n; while((n=await source.ReadAsync(buffer,token))>0) { if(data.Length+n>MaxCatalogBytes)throw new InvalidDataException("The remapped song catalogue is too large."); await data.WriteAsync(buffer.AsMemory(0,n),token); }
  data.Position=0; var items=await JsonSerializer.DeserializeAsync<RemappedSong[]>(data,new JsonSerializerOptions { PropertyNameCaseInsensitive=true },token) ?? throw new InvalidDataException("The remapped song catalogue is empty or invalid.");
  if(items.Length>5000)throw new InvalidDataException("The remapped song catalogue contains too many entries."); foreach(var item in items) { if(item is null)throw new InvalidDataException("The remapped song catalogue contains an invalid entry."); Validate(item); }
  if(items.Select(x=>x.Sha256).Distinct(StringComparer.Ordinal).Count()!=items.Length)throw new InvalidDataException("The remapped song catalogue contains duplicate identifiers."); return items;
 }
 public async Task<(LibraryEntry Entry,bool Duplicate)> DownloadAsync(RemappedSong item,IMidiLibraryService library,IProgress<double>? progress,CancellationToken token) {
  ArgumentNullException.ThrowIfNull(item); ArgumentNullException.ThrowIfNull(library); Validate(item);
  var uri=new Uri("https://larpiano.portways.app/api/remapped/songs/"+item.Sha256+"/midi"); var bytes=await downloads.DownloadAsync(uri,progress,token);
  if(!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)),item.Sha256,StringComparison.Ordinal))throw new InvalidDataException("The downloaded MIDI did not match its catalogue checksum.");
  token.ThrowIfCancellationRequested(); var metadata=new CatalogResult("larpiano",item.Sha256,item.Title,item.Artist,$"Mapped for {item.Profile}; {item.Modification} {item.Attribution}",item.License,item.Source,RecommendedOctave:item.Octave);
  return await library.AddAsync(bytes,metadata,token);
 }
}
