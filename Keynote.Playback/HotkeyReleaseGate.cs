namespace Keynote.Playback;
public static class HotkeyReleaseGate {
 // This is a bounded release check after WM_HOTKEY, not a keyboard listener.
 public static async Task<bool> WaitAsync(Func<bool> released,CancellationToken token,TimeSpan? timeout=null) {
  var limit=timeout ?? TimeSpan.FromSeconds(2); var started=System.Diagnostics.Stopwatch.StartNew();
  while(!released()) { token.ThrowIfCancellationRequested(); if(started.Elapsed>=limit)return false; await Task.Delay(20,token).ConfigureAwait(true); }
  token.ThrowIfCancellationRequested(); return true;
 }
}
