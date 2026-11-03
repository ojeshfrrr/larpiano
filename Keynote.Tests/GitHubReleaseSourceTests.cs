using System.Net;
using System.Net.Http;
using Keynote.Online;
namespace Keynote.Tests;
public class GitHubReleaseSourceTests {
 sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> respond) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)=>respond(r,t); }
 [Fact] public async Task ReadsPublicReleaseVersion() {using var client=new HttpClient(new Handler((r,t)=>{Assert.Equal("https://api.github.com/repos/ojeshfrrr/larpiano/releases/latest",r.RequestUri!.ToString());return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"tag_name\":\"v1.2.3\"}")});})); var result=await new UpdateChecker(new GitHubReleaseSource(client)).CheckAsync(new Version(1,0,0),CancellationToken.None);Assert.Equal(UpdateState.Available,result.State);Assert.Equal("1.2.3",result.LatestVersion);}
 [Fact] public async Task MissingReleaseIsNotAnUpdate() {using var client=new HttpClient(new Handler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));var result=await new GitHubReleaseSource(client).GetLatestAsync(CancellationToken.None);Assert.False(result.Found);Assert.Null(result.Error);}
 [Fact] public async Task InvalidResponseReportsError() {using var client=new HttpClient(new Handler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("not json")})));var result=await new GitHubReleaseSource(client).GetLatestAsync(CancellationToken.None);Assert.NotNull(result.Error);}
 [Fact] public async Task RateLimitReportsError() {using var client=new HttpClient(new Handler((r,t)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));var result=await new GitHubReleaseSource(client).GetLatestAsync(CancellationToken.None);Assert.Contains("403",result.Error);}
 [Fact] public async Task CancelsInFlightRequest() {using var client=new HttpClient(new Handler(async(r,t)=>{await Task.Delay(Timeout.Infinite,t);return new HttpResponseMessage(HttpStatusCode.OK);}));using var c=new CancellationTokenSource();var pending=new GitHubReleaseSource(client).GetLatestAsync(c.Token);c.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);}
}
