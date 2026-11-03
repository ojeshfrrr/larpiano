using System.Diagnostics;
using System.Windows;
using Keynote.Online;
namespace Keynote.App;
public partial class MainWindow {
 readonly UpdateChecker updateChecker=new(new GitHubCliReleaseSource());
 CancellationTokenSource? updateCancellation;
 static string InstalledVersion => typeof(MainWindow).Assembly.GetName().Version is Version v ? $"{v.Major}.{v.Minor}.{Math.Max(0,v.Build)}" : "unknown";
 async void CheckForUpdates(object sender,RoutedEventArgs e) {
  if(updateCancellation!=null)return;
  var operation=new CancellationTokenSource(); updateCancellation=operation; CheckUpdateButton.IsEnabled=false; UpdaterMessage.Text="Checking GitHub releases…";
  try { var result=await updateChecker.CheckAsync(typeof(MainWindow).Assembly.GetName().Version ?? new Version(1,0,0),operation.Token); if(!operation.IsCancellationRequested) { UpdaterMessage.Text=result.Message; LatestUpdate.Text=result.LatestVersion==null?"Latest release: not available":$"Latest release: {result.LatestVersion}"; } }
  catch(OperationCanceledException) {if(!disposed)UpdaterMessage.Text="Update check cancelled.";}
  catch(Exception) {if(!disposed)UpdaterMessage.Text="Could not check for updates. Try again.";}
  finally {if(ReferenceEquals(updateCancellation,operation))updateCancellation=null;operation.Dispose();if(!disposed)CheckUpdateButton.IsEnabled=true;}
 }
 void OpenReleases(object sender,RoutedEventArgs e) {try {Process.Start(new ProcessStartInfo("https://github.com/ojeshfrrr/larpiano/releases"){UseShellExecute=true});}catch(Exception ex) {UpdaterMessage.Text="Could not open GitHub: "+ex.Message;} }
}
