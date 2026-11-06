using System.Net;
using Keynote.Midi;
using Keynote.Online;
using Xunit;

namespace Keynote.Tests;

public class ModernDownloadsTests {
 sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => send(request,token);
 }
 [Fact] public async Task RejectsPatreonRedirectToUnapprovedHost() {
  using var http=new HttpClient(new Handler((_,_)=> { var response=new HttpResponseMessage(HttpStatusCode.Found); response.Headers.Location=new Uri("https://example.com/file.mid"); return Task.FromResult(response); }));
  var item=ModernDownloads.Find("Lana Del Rey","The Grants")!;
  await Assert.ThrowsAsync<HttpRequestException>(()=>new MidiDownloadService(http).DownloadAsync(item.DownloadUri,null,CancellationToken.None));
 }
 [Fact] public void ApprovedEndpointsAreExact() {
  foreach(var item in ModernDownloads.Available) {
   MidiDownloadService.ValidateEndpoint(item.DownloadUri);
   Assert.Throws<InvalidOperationException>(()=>MidiDownloadService.ValidateEndpoint(new Uri(item.DownloadUri.AbsoluteUri+"?other=1")));
  }
  Assert.Throws<InvalidOperationException>(()=>MidiDownloadService.ValidateEndpoint(new Uri("https://free-piano-tutorials-midi.s3.amazonaws.com/other.mid")));
  Assert.Null(ModernDownloads.Find("Kanye West","Runaway"));
 }
 [Fact] public void PatreonRedirectMustMatchApprovedPost() {
  var item=ModernDownloads.Find("Niall Horan","You Could Start A Cult")!;
  Assert.True(ModernDownloads.IsApprovedPatreonRedirect(item.DownloadUri,new Uri("https://c10.patreonusercontent.com/4/patreon-media/p/post/84720121/example/1.mid?token=test")));
  Assert.False(ModernDownloads.IsApprovedPatreonRedirect(item.DownloadUri,new Uri("https://c10.patreonusercontent.com/4/patreon-media/p/post/81977759/example/1.mid?token=test")));
 }
 [Fact] public void DropboxRedirectRequiresApprovedSourceAndDropboxHost() {
  var item=ModernDownloads.Find("Cigarettes After Sex","K.")!;
  Assert.True(ModernDownloads.IsApprovedDropboxRedirect(item.DownloadUri,new Uri("https://uc123abc.dl.dropboxusercontent.com/cd/0/inline/token/file?dl=1")));
  Assert.True(ModernDownloads.IsApprovedDropboxRedirect(item.DownloadUri,new Uri("https://uc123abc.dl.dropboxusercontent.com/cd/0/get/token/file?dl=1")));
  Assert.False(ModernDownloads.IsApprovedDropboxRedirect(item.DownloadUri,new Uri("https://uc123abc.dl.dropboxusercontent.com/cd/0/other/token/file")));
  Assert.False(ModernDownloads.IsApprovedDropboxRedirect(item.DownloadUri,new Uri("https://uc123abc.dl.dropboxusercontent.com.evil.example/cd/0/inline/token/file")));
  Assert.False(ModernDownloads.IsApprovedDropboxRedirect(new Uri("https://www.dropbox.com/other"),new Uri("https://uc123abc.dl.dropboxusercontent.com/cd/0/inline/token/file")));
 }
 [Fact] public async Task ChangedFileIsRejectedBeforeSaving() {
  var data=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","test-c-major-scale.mid"));
  using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(data)})));
  var dir=Path.Combine(Path.GetTempPath(),"modern-"+Guid.NewGuid().ToString("N"));
  try {
   var library=new MidiLibraryService(dir,new MidiImporter());
   var service=new ModernDownloads(new MidiDownloadService(http),library);
   await Assert.ThrowsAsync<InvalidDataException>(()=>service.DownloadAsync(ModernDownloads.Available[0],null,CancellationToken.None));
   Assert.False(Directory.Exists(dir));
  } finally { if(Directory.Exists(dir))Directory.Delete(dir,true); }
 }
 [Fact] public async Task CancellationPreventsSaving() {
  using var http=new HttpClient(new Handler(async (_,token)=> { await Task.Delay(Timeout.Infinite,token); return new HttpResponseMessage(HttpStatusCode.OK); }));
  var dir=Path.Combine(Path.GetTempPath(),"modern-"+Guid.NewGuid().ToString("N"));
  try {
   var service=new ModernDownloads(new MidiDownloadService(http),new MidiLibraryService(dir,new MidiImporter()));
   using var cancel=new CancellationTokenSource(); var task=service.DownloadAsync(ModernDownloads.Available[0],null,cancel.Token); cancel.Cancel();
   await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
   Assert.False(Directory.Exists(dir));
  } finally { if(Directory.Exists(dir))Directory.Delete(dir,true); }
 }
}
