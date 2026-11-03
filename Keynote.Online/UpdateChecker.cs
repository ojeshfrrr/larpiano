using System.ComponentModel;
using System.Diagnostics;
namespace Keynote.Online;

public enum UpdateState { Available, Current, NoRelease, Unavailable, InvalidRelease }
public record UpdateResult(UpdateState State,string Message,string? LatestVersion=null);
public record ReleaseQuery(bool Found,string? Tag,string? Error=null);
public interface IReleaseSource { Task<ReleaseQuery> GetLatestAsync(CancellationToken cancellationToken); }

public sealed class UpdateChecker(IReleaseSource source) {
 public async Task<UpdateResult> CheckAsync(Version installed,CancellationToken token) {
  var release=await source.GetLatestAsync(token);
  if(release.Error!=null)return new(UpdateState.Unavailable,release.Error);
  if(!release.Found)return new(UpdateState.NoRelease,"No published release was found, or the signed-in account cannot access this repository.");
  var tag=release.Tag?.Trim(); var versionText=tag!=null && tag.StartsWith('v')?tag[1..]:tag;
  if(string.IsNullOrWhiteSpace(versionText) || versionText.Length>32 || versionText.Any(c=>!(char.IsAsciiDigit(c)||c=='.')) || !Version.TryParse(versionText,out var latest) || latest.Build<0 || latest.Revision>=0)
   return new(UpdateState.InvalidRelease,"The latest release does not use a supported vMAJOR.MINOR.PATCH version tag.",tag);
  var current=new Version(installed.Major,installed.Minor,Math.Max(0,installed.Build));
  return latest>current ? new(UpdateState.Available,$"Version {latest} is available. Open the releases page to download it.",latest.ToString()) : new(UpdateState.Current,$"You have the latest published version ({latest}).",latest.ToString());
 }
}

public sealed class GitHubCliReleaseSource : IReleaseSource {
 public async Task<ReleaseQuery> GetLatestAsync(CancellationToken cancellationToken) {
  var start=new ProcessStartInfo("gh") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in new[]{"api","--hostname","github.com","repos/ojeshfrrr/larpiano/releases/latest","--jq",".tag_name"})start.ArgumentList.Add(arg);
  using var process=new Process {StartInfo=start};
  try { if(!process.Start())return new(false,null,"Could not start GitHub CLI."); }
  catch(Win32Exception) {return new(false,null,"GitHub CLI is unavailable. Install it and sign in to the GitHub account with access to larpiano.");}
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(20));
  var output=process.StandardOutput.ReadToEndAsync(timeout.Token); var errors=process.StandardError.ReadToEndAsync(timeout.Token);
  try {await process.WaitForExitAsync(timeout.Token);var text=await output;var error=await errors;if(process.ExitCode==0)return new(true,text); if(error.Contains("HTTP 404",StringComparison.OrdinalIgnoreCase))return new(false,null); return new(false,null,"Could not check GitHub releases. Check your connection and GitHub CLI login.");}
  catch(OperationCanceledException) {try {if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){} if(cancellationToken.IsCancellationRequested)throw;return new(false,null,"The update check timed out. Try again.");}
 }
}
