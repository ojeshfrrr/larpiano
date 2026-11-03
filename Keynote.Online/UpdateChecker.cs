using System.Net;
using System.Net.Http;
using System.Text.Json;
namespace Keynote.Online;

public enum UpdateState { Available, Current, NoRelease, Unavailable, InvalidRelease }
public record UpdateResult(UpdateState State,string Message,string? LatestVersion=null);
public record ReleaseQuery(bool Found,string? Tag,string? Error=null);
public interface IReleaseSource { Task<ReleaseQuery> GetLatestAsync(CancellationToken cancellationToken); }

public sealed class UpdateChecker(IReleaseSource source) {
 public async Task<UpdateResult> CheckAsync(Version installed,CancellationToken token) {
  var release=await source.GetLatestAsync(token);
  if(release.Error!=null)return new(UpdateState.Unavailable,release.Error);
  if(!release.Found)return new(UpdateState.NoRelease,"No published release was found.");
  var tag=release.Tag?.Trim(); var versionText=tag!=null && tag.StartsWith('v')?tag[1..]:tag;
  if(string.IsNullOrWhiteSpace(versionText) || versionText.Length>32 || versionText.Any(c=>!(char.IsAsciiDigit(c)||c=='.')) || !Version.TryParse(versionText,out var latest) || latest.Build<0 || latest.Revision>=0)
   return new(UpdateState.InvalidRelease,"The latest release does not use a supported vMAJOR.MINOR.PATCH version tag.",tag);
  var current=new Version(installed.Major,installed.Minor,Math.Max(0,installed.Build));
  return latest>current ? new(UpdateState.Available,$"Version {latest} is available. Open the releases page to download it.",latest.ToString()) : new(UpdateState.Current,$"You have the latest published version ({latest}).",latest.ToString());
 }
}

public sealed class GitHubReleaseSource(HttpClient client) : IReleaseSource {
 public static HttpClient CreateClient() { var client=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(20)}; client.DefaultRequestHeaders.UserAgent.ParseAdd("larpiano-updater/1.0"); client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); return client; }
 public async Task<ReleaseQuery> GetLatestAsync(CancellationToken cancellationToken) {
  try {
   using var response=await client.GetAsync("https://api.github.com/repos/ojeshfrrr/larpiano/releases/latest",HttpCompletionOption.ResponseHeadersRead,cancellationToken);
   if(response.StatusCode==HttpStatusCode.NotFound)return new(false,null);
   if(!response.IsSuccessStatusCode)return new(false,null,$"GitHub could not complete the update check (HTTP {(int)response.StatusCode}). Try again later.");
   if(response.Content.Headers.ContentLength>65536)return new(false,null,"GitHub's release response was too large.");
   await using var stream=await response.Content.ReadAsStreamAsync(cancellationToken); using var buffer=new MemoryStream(); var chunk=new byte[4096]; int read;
   while((read=await stream.ReadAsync(chunk,cancellationToken))>0) {if(buffer.Length+read>65536)return new(false,null,"GitHub's release response was too large.");buffer.Write(chunk,0,read);}
   using var json=JsonDocument.Parse(buffer.ToArray());
   if(!json.RootElement.TryGetProperty("tag_name",out var tag)||tag.ValueKind!=JsonValueKind.String)return new(false,null,"The GitHub release response did not contain a version.");
   return new(true,tag.GetString());
  } catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) {return new(false,null,"The update check timed out. Try again.");}
  catch(HttpRequestException) {return new(false,null,"Could not reach GitHub. Check your connection and try again.");}
  catch(JsonException) {return new(false,null,"GitHub returned an invalid release response.");}
 }
}
