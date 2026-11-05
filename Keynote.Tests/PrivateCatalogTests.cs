using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Keynote.Midi;
using Keynote.Online;
using Xunit;

namespace Keynote.Tests;

public class PrivateCatalogTests {
 sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> run) : HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => run(request,token);
 }
 [Fact] public async Task AuthorizedDownloadValidatesAndStoresFixture() {
  var bytes=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","test-c-major-scale.mid"));
  var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
  var song=new RemappedSong(hash,"Fixture","Artist","https://example.org","Personal","Test",1,8,0);
  var seen=new List<string>();
  using var http=new HttpClient(new Handler((request,_)=> {
   Assert.Equal("Bearer",request.Headers.Authorization?.Scheme); Assert.Equal(new string('a',32),request.Headers.Authorization?.Parameter);
   seen.Add(request.RequestUri!.AbsolutePath);
   return Task.FromResult(seen.Count==1 ? new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new[]{song}),Encoding.UTF8,"application/json")} : new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
  }));
  var root=Path.Combine(Path.GetTempPath(),"private-catalog-"+Guid.NewGuid().ToString("N"));
  try {
   var library=new MidiLibraryService(root,new MidiImporter()); var service=new PrivateCatalogService(http,library);
   var item=Assert.Single(await service.ListAsync(new string('a',32),CancellationToken.None));
   var result=await service.DownloadAsync(item,new string('a',32),CancellationToken.None);
   Assert.Equal(hash,result.Entry.Sha256); Assert.Equal(1,result.Entry.RecommendedOctave);
   Assert.Equal(bytes,await File.ReadAllBytesAsync(await library.OpenAsync(result.Entry,CancellationToken.None)));
   Assert.Equal("/api/private/songs/"+hash+"/midi",seen[1]);
  } finally { if(Directory.Exists(root))Directory.Delete(root,true); }
 }
 [Fact] public async Task InvalidCodeNeverSendsRequest() {
  using var http=new HttpClient(new Handler((_,_)=>throw new Exception("Should not send")));
  var service=new PrivateCatalogService(http,new MidiLibraryService(Path.GetTempPath(),new MidiImporter()));
  await Assert.ThrowsAsync<ArgumentException>(()=>service.ListAsync("short",CancellationToken.None));
 }
}
