using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Keynote.Online;

public sealed class UpdatePackageService(HttpClient client) {
 const long MaxArchive = 200L * 1024 * 1024, MaxExecutable = 250L * 1024 * 1024;
 const string AssetName = "larpiano-windows-x64.zip";
 public async Task<string> DownloadAsync(string version, string stagingDirectory, IProgress<double>? progress, CancellationToken token) {
  if (!Version.TryParse(version, out var parsed) || parsed.Build < 0 || parsed.Revision >= 0 || parsed.ToString() != version)
   throw new InvalidDataException("Invalid update version.");
  var api = new Uri("https://api.github.com/repos/ojeshfrrr/larpiano/releases/latest");
  using var response = await client.GetAsync(api, HttpCompletionOption.ResponseHeadersRead, token);
  response.EnsureSuccessStatusCode();
  if (response.Content.Headers.ContentLength > 256 * 1024) throw new InvalidDataException("Release metadata is too large.");
  var metadata = await ReadLimitedAsync(response.Content, 256 * 1024, token);
  using var json = JsonDocument.Parse(metadata);
  if (json.RootElement.GetProperty("tag_name").GetString() != "v" + version) throw new InvalidDataException("The latest release changed. Check for updates again.");
  var assets = json.RootElement.GetProperty("assets").EnumerateArray().Where(x => x.GetProperty("name").GetString() == AssetName).ToArray();
  if (assets.Length != 1) throw new InvalidDataException("The Windows update package is missing or ambiguous.");
  var asset = assets[0];
  var digest = asset.GetProperty("digest").GetString();
  if (digest is null || digest.Length != 71 || !digest.StartsWith("sha256:", StringComparison.Ordinal) || !digest[7..].All(Uri.IsHexDigit)) throw new InvalidDataException("The release does not provide a valid SHA-256 digest.");
  var url = new Uri(asset.GetProperty("browser_download_url").GetString() ?? "");
  var expected = new Uri($"https://github.com/ojeshfrrr/larpiano/releases/download/v{version}/{AssetName}");
  if (url != expected) throw new InvalidDataException("The update package URL is not trusted.");
  Directory.CreateDirectory(stagingDirectory);
  string archivePath = Path.Combine(stagingDirectory, "update.zip"), executablePath = Path.Combine(stagingDirectory, "Keynote.App.exe");
  try {
   using var download = await GetAssetAsync(url, token);
   download.EnsureSuccessStatusCode();
   if (download.Content.Headers.ContentLength is long length && (length <= 0 || length > MaxArchive)) throw new InvalidDataException("Update package size is invalid.");
   await using (var source = await download.Content.ReadAsStreamAsync(token))
   await using (var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) {
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[81920]; long total = 0; int count;
    while ((count = await source.ReadAsync(buffer, token)) > 0) {
     total += count; if (total > MaxArchive) throw new InvalidDataException("Update package is too large.");
     hash.AppendData(buffer, 0, count); await destination.WriteAsync(buffer.AsMemory(0, count), token);
     if (download.Content.Headers.ContentLength is long size && size > 0) progress?.Report((double)total / size);
    }
    if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(digest[7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum did not match the release.");
   }
   using (var zip = ZipFile.OpenRead(archivePath)) {
    var entries = zip.Entries.Where(e => e.FullName.Equals("Keynote.App.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (entries.Length != 1 || entries[0].Length <= 0 || entries[0].Length > MaxExecutable) throw new InvalidDataException("The update package has no valid app executable.");
    await using var src = entries[0].Open();
    await using var dst = new FileStream(executablePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
    var buffer = new byte[81920]; long total = 0; int count;
    while ((count = await src.ReadAsync(buffer, token)) > 0) { total += count; if (total > MaxExecutable) throw new InvalidDataException("Executable is too large."); await dst.WriteAsync(buffer.AsMemory(0,count),token); }
   }
   File.Delete(archivePath);
   return executablePath;
  } catch { try { if (File.Exists(archivePath)) File.Delete(archivePath); if (File.Exists(executablePath)) File.Delete(executablePath); } catch (IOException) {} throw; }
 }
 async Task<HttpResponseMessage> GetAssetAsync(Uri url, CancellationToken token) {
  for (int i=0;i<4;i++) {
   using var request = new HttpRequestMessage(HttpMethod.Get,url);
   var response = await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
   if ((int)response.StatusCode is < 300 or >= 400) return response;
   var location = response.Headers.Location; response.Dispose();
   if (location == null) throw new InvalidDataException("Invalid update redirect.");
   url = location.IsAbsoluteUri ? location : new Uri(url,location);
   if (url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort || url.UserInfo.Length != 0 || url.Fragment.Length != 0 || !url.Host.Equals("release-assets.githubusercontent.com",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update redirect is not trusted.");
  }
  throw new InvalidDataException("Too many update redirects.");
 }
 static async Task<byte[]> ReadLimitedAsync(HttpContent content, int limit, CancellationToken token) {
  await using var src=await content.ReadAsStreamAsync(token); using var dst=new MemoryStream(); var bytes=new byte[8192]; int n;
  while ((n=await src.ReadAsync(bytes,token))>0) { if (dst.Length+n>limit) throw new InvalidDataException("Release metadata is too large."); dst.Write(bytes,0,n); }
  return dst.ToArray();
 }
}
