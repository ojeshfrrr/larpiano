using System.Diagnostics;
using System.IO;
using System.Windows;
using Keynote.Online;
namespace Keynote.App;
public partial class MainWindow {
 static readonly System.Net.Http.HttpClient updateHttp=GitHubReleaseSource.CreateClient();
 static readonly System.Net.Http.HttpClient packageHttp=CreatePackageClient();
 readonly UpdateChecker updateChecker=new(new GitHubReleaseSource(updateHttp));
 CancellationTokenSource? updateCancellation;
 string? availableVersion;
 static System.Net.Http.HttpClient CreatePackageClient() {var client=GitHubReleaseSource.CreateClient(); client.Timeout=TimeSpan.FromMinutes(10);return client;}
 static string InstalledVersion => typeof(MainWindow).Assembly.GetName().Version is Version v ? $"{v.Major}.{v.Minor}.{Math.Max(0,v.Build)}" : "unknown";
 async void CheckForUpdates(object sender,RoutedEventArgs e) {
  if(updateCancellation!=null)return;
  var operation=new CancellationTokenSource(); updateCancellation=operation; CheckUpdateButton.IsEnabled=false; InstallUpdateButton.IsEnabled=false; UpdaterMessage.Text="Checking for updates…";
  try { var result=await updateChecker.CheckAsync(typeof(MainWindow).Assembly.GetName().Version ?? new Version(1,0,0),operation.Token); if(!operation.IsCancellationRequested) { availableVersion=result.State==UpdateState.Available?result.LatestVersion:null; UpdaterMessage.Text=availableVersion==null?result.Message:$"Version {availableVersion} is ready to download."; LatestUpdate.Text=result.LatestVersion==null?"Latest release: not available":$"Latest release: {result.LatestVersion}"; } }
  catch(OperationCanceledException) {if(!disposed)UpdaterMessage.Text="Update check cancelled.";}
  catch(Exception) {if(!disposed)UpdaterMessage.Text="Could not check for updates. Try again.";}
  finally {if(ReferenceEquals(updateCancellation,operation))updateCancellation=null;operation.Dispose();if(!disposed){CheckUpdateButton.IsEnabled=true;InstallUpdateButton.IsEnabled=availableVersion!=null;}}
 }
 async void InstallUpdate(object sender,RoutedEventArgs e) {
  if(updateCancellation!=null || availableVersion==null)return;
  var target=Environment.ProcessPath;
#pragma warning disable IL3000 // An empty assembly location identifies the supported single-file release.
  var isSingleFile = string.IsNullOrEmpty(typeof(MainWindow).Assembly.Location);
#pragma warning restore IL3000
  if(target==null || !Path.GetFileName(target).Equals("Keynote.App.exe",StringComparison.OrdinalIgnoreCase) || !isSingleFile) { UpdaterMessage.Text="Automatic updates require the single-file Windows release. Download the release once to enable them."; return; }
  var operation=new CancellationTokenSource(); updateCancellation=operation; CheckUpdateButton.IsEnabled=false; InstallUpdateButton.IsEnabled=false; CancelUpdateButton.Visibility=Visibility.Visible; UpdateProgress.Visibility=Visibility.Visible; UpdateProgress.Value=0;
  string staging=Path.Combine(Path.GetTempPath(),"larpiano-update-"+Guid.NewGuid().ToString("N")); bool handedOff=false;
  try {
   UpdaterMessage.Text="Downloading update…";
   var version=availableVersion;
   var progress=new Progress<double>(p=>{if(!disposed && ReferenceEquals(updateCancellation,operation)){UpdateProgress.Value=Math.Clamp(p*100,0,100);UpdaterMessage.Text=$"Downloading update… {p:P0}";}});
   var staged=await new UpdatePackageService(packageHttp).DownloadAsync(version,staging,progress,operation.Token);
   operation.Token.ThrowIfCancellationRequested();
   var fileVersion=FileVersionInfo.GetVersionInfo(staged);
   if (!Version.TryParse(fileVersion.FileVersion,out var actual) || new Version(actual.Major,actual.Minor,Math.Max(actual.Build,0)).ToString()!=version) throw new InvalidDataException("The update executable version does not match the release.");
   var script=Path.Combine(staging,"install.ps1"); await File.WriteAllTextAsync(script,InstallerScript,operation.Token);
   var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell", "v1.0", "powershell.exe")){UseShellExecute=false,CreateNoWindow=true};
   foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",script,"-ParentId",Environment.ProcessId.ToString(),"-Target",target,"-Source",staged})info.ArgumentList.Add(arg);
   if(Process.Start(info)==null)throw new InvalidOperationException("Could not start the installer.");
   handedOff=true; UpdaterMessage.Text="Installing update and restarting…"; Close();
  } catch(OperationCanceledException) {if(!disposed)UpdaterMessage.Text="Update cancelled.";}
  catch(Exception ex) {if(!disposed)UpdaterMessage.Text="Could not install the update: "+ex.Message;}
  finally {if(ReferenceEquals(updateCancellation,operation))updateCancellation=null;operation.Dispose();if(!handedOff){try{if(Directory.Exists(staging))Directory.Delete(staging,true);}catch(IOException){}catch(UnauthorizedAccessException){} if(!disposed){CheckUpdateButton.IsEnabled=true;InstallUpdateButton.IsEnabled=availableVersion!=null;CancelUpdateButton.Visibility=Visibility.Collapsed;UpdateProgress.Visibility=Visibility.Collapsed;}}}
 }
 void CancelUpdate(object sender,RoutedEventArgs e)=>updateCancellation?.Cancel();
 void OpenReleases(object sender,RoutedEventArgs e) {try {Process.Start(new ProcessStartInfo("https://github.com/ojeshfrrr/larpiano/releases"){UseShellExecute=true});}catch(Exception ex) {UpdaterMessage.Text="Could not open GitHub: "+ex.Message;} }
 const string InstallerScript="""
param([int]$ParentId,[string]$Target,[string]$Source)
$ErrorActionPreference = 'Stop'
$backup = $Target + '.previous'
$temp = $Target + '.update-' + [Guid]::NewGuid().ToString('N')
$replaced = $false
try {
  $parentProcess = Get-Process -Id $ParentId -ErrorAction SilentlyContinue
  if ($parentProcess) { if (-not $parentProcess.WaitForExit(30000)) { throw 'The app did not close. Please close it and try again.' } }
  if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Force }
  [IO.File]::Copy($Source, $temp, $false)
  [IO.File]::Replace($temp, $Target, $backup)
  $replaced = $true
  Start-Process -FilePath $Target -WorkingDirectory ([IO.Path]::GetDirectoryName($Target)) | Out-Null
} catch {
  $message = $_.Exception.Message
  if ($replaced -and (Test-Path -LiteralPath $backup)) {
    try { [IO.File]::Replace($backup, $Target, $null) } catch { $message += "`nPrevious version is saved at: $backup" }
  }
  if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
  Add-Type -AssemblyName PresentationFramework
  [System.Windows.MessageBox]::Show("Update failed: $message", 'larpiano update') | Out-Null
  exit 1
}
""";
}
