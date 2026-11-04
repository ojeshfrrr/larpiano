using System.Windows;
using System.Windows.Controls;
using Keynote.Online;
namespace Keynote.App;
public partial class MainWindow {
 RemappedCatalogService? remappedCatalog;
 IReadOnlyList<RemappedSong> remappedSongs=Array.Empty<RemappedSong>();
 CancellationTokenSource? remappedCancellation;
 bool remappedLoaded;
 void MainTabChanged(object sender,SelectionChangedEventArgs e) { if(ReferenceEquals(e.Source,MainTabs) && ReferenceEquals(MainTabs.SelectedItem,RemappedTab) && !remappedLoaded && !WorkflowMode) _=LoadRemappedAsync(); }
 async void RefreshRemapped(object sender,RoutedEventArgs e)=>await LoadRemappedAsync();
 void CancelRemapped(object sender,RoutedEventArgs e)=>remappedCancellation?.Cancel();
 async Task LoadRemappedAsync() {
  if(onlineHttp==null || downloads==null)return;
  remappedCancellation?.Cancel(); var request=new CancellationTokenSource(); remappedCancellation=request;
  try { RemappedStatus.Text="Loading remapped songs…"; remappedCatalog ??=new RemappedCatalogService(onlineHttp,downloads); var items=await remappedCatalog.ListAsync(request.Token); request.Token.ThrowIfCancellationRequested(); remappedSongs=items; remappedLoaded=true; ApplyRemappedFilter(); RemappedStatus.Text=items.Count==0?"No arrangements available yet. Try Refresh later.":$"{items.Count} arrangements available. Select one to see the details."; }
  catch(OperationCanceledException) { if(ReferenceEquals(remappedCancellation,request))RemappedStatus.Text="Loading cancelled."; }
  catch(Exception ex) { if(ReferenceEquals(remappedCancellation,request))RemappedStatus.Text="Could not load the catalogue. Check your connection and select Refresh. "+ex.Message; }
  finally { if(ReferenceEquals(remappedCancellation,request))remappedCancellation=null; request.Dispose(); }
 }
 void RemappedFilterChanged(object sender,TextChangedEventArgs e) { if(RemappedList!=null)ApplyRemappedFilter(); }
 void ApplyRemappedFilter() { if(RemappedList==null || RemappedQuery==null)return; var q=RemappedQuery.Text.Trim(); RemappedList.ItemsSource=remappedSongs.Where(x=>x.Title.Contains(q,StringComparison.OrdinalIgnoreCase)||x.Artist.Contains(q,StringComparison.OrdinalIgnoreCase)).ToArray(); }
 void RemappedSelected(object sender,SelectionChangedEventArgs e) {
  if(RemappedList.SelectedItem is not RemappedSong item) { RemappedDownload.IsEnabled=false; RemappedDetails.Text="Select a song to see its source and mapping details."; return; }
  RemappedDownload.IsEnabled=true; RemappedDetails.Text=$"{item.Title}\n{item.Artist}\n\nPiano: {item.Profile}\nRecommended octave: {item.Octave:+0;-0;0}\nNotes: {item.KeptNotes}; omitted: {item.ExcludedNotes}\n{item.Modification}\n\n{item.Attribution}\nLicense: {item.License}\nSource: {item.Source}\n\nPrepared from the listed source; this individual arrangement has not been confirmed in Roblox. Test and verify your own mapping.";
 }
 async void DownloadRemapped(object sender,RoutedEventArgs e)=>await DownloadRemappedAsync();
 async Task DownloadRemappedAsync() {
  if(RemappedList.SelectedItem is not RemappedSong item || library==null || remappedCatalog==null)return;
  remappedCancellation?.Cancel(); var request=new CancellationTokenSource(); remappedCancellation=request; RemappedDownload.IsEnabled=false;
  try { RemappedStatus.Text="Downloading and checking MIDI…"; var progress=new Progress<double>(p=> { if(ReferenceEquals(remappedCancellation,request))RemappedStatus.Text=$"Downloading {p:P0}…"; }); var saved=await remappedCatalog.DownloadAsync(item,library,progress,request.Token); request.Token.ThrowIfCancellationRequested(); await library.SetRecommendedOctaveAsync(saved.Entry,item.Octave,request.Token); await ReloadLibrary(request.Token); var match=libraryEntries.FirstOrDefault(x=>x.Sha256==saved.Entry.Sha256); if(match==null)throw new InvalidOperationException("Saved song was not found in the local library."); PlayerSongSelector.SelectedItem=match; MainTabs.SelectedIndex=0; RemappedStatus.Text=saved.Duplicate?"Already in your library; selected for playback.":"Downloaded to your library and selected."; }
  catch(OperationCanceledException) { if(ReferenceEquals(remappedCancellation,request))RemappedStatus.Text="Download cancelled."; }
  catch(Exception ex) { if(ReferenceEquals(remappedCancellation,request))RemappedStatus.Text="Download failed: "+ex.Message; }
  finally { if(ReferenceEquals(remappedCancellation,request)) { remappedCancellation=null; RemappedDownload.IsEnabled=RemappedList.SelectedItem is RemappedSong; } request.Dispose(); }
 }
}
