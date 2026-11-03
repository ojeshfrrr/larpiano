using Keynote.Online;
namespace Keynote.Tests;
public class UpdateCheckerTests {
 sealed class Source(ReleaseQuery query) : IReleaseSource { public Task<ReleaseQuery> GetLatestAsync(CancellationToken token) {token.ThrowIfCancellationRequested();return Task.FromResult(query);} }
 [Theory]
 [InlineData("v1.0.1", UpdateState.Available)]
 [InlineData("v1.0.0", UpdateState.Current)]
 [InlineData("v0.9.9", UpdateState.Current)]
 [InlineData("v1.0.1-beta", UpdateState.InvalidRelease)]
 [InlineData("v1.0", UpdateState.InvalidRelease)]
 [InlineData("not-a-version", UpdateState.InvalidRelease)]
 public async Task ComparesStableReleaseTags(string tag,UpdateState state) {var result=await new UpdateChecker(new Source(new(true,tag))).CheckAsync(new Version(1,0,0,0),CancellationToken.None);Assert.Equal(state,result.State);}
 [Fact] public async Task ReportsMissingReleaseAndAccessAmbiguity() {var r=await new UpdateChecker(new Source(new(false,null))).CheckAsync(new Version(1,0,0),CancellationToken.None);Assert.Equal(UpdateState.NoRelease,r.State);Assert.Contains("cannot access",r.Message);}
 [Fact] public async Task ReportsSourceFailure() {var r=await new UpdateChecker(new Source(new(false,null,"unavailable"))).CheckAsync(new Version(1,0,0),CancellationToken.None);Assert.Equal(UpdateState.Unavailable,r.State);}
 [Fact] public async Task HonorsCancellation() {using var c=new CancellationTokenSource();c.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new UpdateChecker(new Source(new(true,"v1.0.1"))).CheckAsync(new Version(1,0,0),c.Token));}
}
