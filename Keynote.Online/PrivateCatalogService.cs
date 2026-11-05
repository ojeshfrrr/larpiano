using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Keynote.Online;

public sealed class PrivateCatalogService(HttpClient http, IMidiLibraryService library) {
 static readonly Uri Root = new("https://larpiano.portways.app/api/private/songs");
 static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
 static void CheckCode(string code) { if(code.Length is < 24 or > 256 || code.Any(c=>c is < '!' or > '~')) throw new ArgumentException("Enter the shared access code."); }
 static HttpRequestMessage Request(Uri uri,string code) { CheckCode(code); var request=new HttpRequestMessage(HttpMethod.Get,uri); request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",code); return request; }
 static async Task<byte[]> ReadBounded(HttpContent content,int limit,CancellationToken token) {
  if(content.Headers.ContentLength>limit)throw new InvalidDataException("Response exceeds the size limit.");
  await using var input=await content.ReadAsStreamAsync(token); using var output=new MemoryStream(); var buffer=new byte[16384]; int n;
  while((n=await input.ReadAsync(buffer,token))>0) { if(output.Length+n>limit)throw new InvalidDataException("Response exceeds the size limit."); await output.WriteAsync(buffer.AsMemory(0,n),token); }
  return output.ToArray();
 }
 public async Task<IReadOnlyList<RemappedSong>> ListAsync(string code,CancellationToken token) {
  using var request=Request(Root,code); using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
  var bytes=await ReadBounded(response.Content,1024*1024,token);
  var songs=JsonSerializer.Deserialize<RemappedSong[]>(bytes,new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? throw new InvalidDataException("Invalid private catalogue.");
  if(songs.Length>1000 || songs.Any(x=>x is null || !ValidHash(x.Sha256) || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length>200 || string.IsNullOrWhiteSpace(x.Artist) || x.Artist.Length>200 || x.Octave is < -2 or > 2))throw new InvalidDataException("Invalid private catalogue entry.");
  return songs;
 }
 public async Task<(LibraryEntry Entry,bool Duplicate)> DownloadAsync(RemappedSong song,string code,CancellationToken token) {
  if(!ValidHash(song.Sha256) || song.Octave is < -2 or > 2)throw new InvalidDataException("Invalid song identifier.");
  using var request=Request(new Uri(Root.AbsoluteUri+"/"+song.Sha256+"/midi"),code); using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
  var bytes=await ReadBounded(response.Content,MidiDownloadService.MaxBytes,token);
  if(bytes.Length<14 || !bytes.AsSpan(0,4).SequenceEqual("MThd"u8) || Convert.ToHexStringLower(SHA256.HashData(bytes))!=song.Sha256)throw new InvalidDataException("The song failed integrity verification.");
  token.ThrowIfCancellationRequested();
  return await library.AddAsync(bytes,new CatalogResult("Friends collection",song.Sha256,song.Title,song.Artist,song.Modification,song.License,song.Source,RecommendedOctave:song.Octave),token);
 }
}
