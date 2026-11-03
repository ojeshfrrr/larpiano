using Keynote.Playback;
namespace Keynote.Tests;
public class HotkeyReleaseGateTests {
 [Fact] public async Task WaitsForReleaseBeforeReturning() { bool released=false; var task=HotkeyReleaseGate.WaitAsync(()=>released,CancellationToken.None,TimeSpan.FromSeconds(1)); Assert.False(task.IsCompleted); released=true; Assert.True(await task); }
 [Fact] public async Task CancellationPreventsADeferredStart() { using var cts=new CancellationTokenSource(); var task=HotkeyReleaseGate.WaitAsync(()=>false,cts.Token); cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task); }
 [Fact] public async Task HeldShortcutTimesOut() { Assert.False(await HotkeyReleaseGate.WaitAsync(()=>false,CancellationToken.None,TimeSpan.FromMilliseconds(25))); }
}
