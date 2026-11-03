namespace Keynote.Online;
public static class TranscriptionJobRunner {
 public static async Task<TranscriptionStatus> WaitAsync(ISongToMidiProvider provider,string id,IProgress<TranscriptionStatus>? progress,CancellationToken token,TimeSpan? pollInterval=null) {
  while(true) { token.ThrowIfCancellationRequested(); var status=await provider.GetStatusAsync(id,token); progress?.Report(status); if(status.Status=="succeeded")return status; if(status.Status=="failed")throw new InvalidOperationException(status.Error ?? "Conversion failed."); await Task.Delay(pollInterval ?? TimeSpan.FromSeconds(5),token); }
 }
}
