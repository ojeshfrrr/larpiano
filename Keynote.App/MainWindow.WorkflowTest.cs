using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Keynote.Online;
namespace Keynote.App;
public partial class MainWindow {
 sealed class WorkflowHttpHandler(byte[] midi) : HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
   token.ThrowIfCancellationRequested(); var query=request.RequestUri?.Query ?? "";
   if(request.RequestUri?.AbsolutePath=="/api/remapped/songs") { var hash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(midi)); var item=new RemappedSong(hash,"Remapped workflow song","Test composer","https://www.mutopiaproject.org/cgibin/piece-info.cgi?id=1","Public Domain","Visual Pianos",1,16,0,0,"Fixture","Test fixture"); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new[]{item}),Encoding.UTF8,"application/json")}); }
   if(request.RequestUri?.AbsolutePath.StartsWith("/api/remapped/songs/",StringComparison.Ordinal)==true)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(midi)});
   if(query.Contains("action=search",StringComparison.Ordinal))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("[{\"id\":42,\"name\":\"Workflow test song\",\"notes\":\"Original test fixture\"}]",Encoding.UTF8,"application/json")});
   if(query.Contains("action=get_midi",StringComparison.Ordinal))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(midi)});
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
  }
 }
 static HttpClient CreateWorkflowHttpClient() { var args=Environment.GetCommandLineArgs(); var index=Array.IndexOf(args,"--workflow-test"); var file=Path.GetFullPath(args[index+2]); return new HttpClient(new WorkflowHttpHandler(File.ReadAllBytes(file))) { Timeout=TimeSpan.FromSeconds(20) }; }
 async Task RunPublishedWorkflowTest() {
  var root=Path.GetDirectoryName(AppDataRoot)!; var output=Path.Combine(root,"workflow-result.json"); var result=new Dictionary<string,object>();
  try {
   void Capture(string name) { UpdateLayout(); var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(this); var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var stream=File.Create(Path.Combine(root,name+".png")); encoder.Save(stream); }
   Width=1280; Height=720; for(int tab=0;tab<7;tab++) { MainTabs.SelectedIndex=tab; await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture("screen-"+tab); }
   foreach(var size in new[]{(1280,720),(1366,768),(1920,1080)}) {Width=size.Item1;Height=size.Item2;MainTabs.SelectedIndex=0;await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle); if(Windows.ActualWidth<100 || PlayerSongSelector.ActualWidth<100)throw new InvalidOperationException("Player selectors clipped at "+size); Capture("size-"+size.Item1);}
   if(InstalledUpdate.Text!="Installed version: "+InstalledVersion || !UpdaterMessage.Text.Contains("Check GitHub",StringComparison.Ordinal))throw new InvalidOperationException("Updater initial state missing."); result["updaterNavigation"]=true; result["navigationAndResize"]=true;
   MainTabs.SelectedItem=ModernTab; Width=800; Height=600; await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout(); if(ModernList.ActualWidth<100 || ModernSearchButton.ActualWidth<60)throw new InvalidOperationException("Modern tab controls clipped at minimum size."); Capture("modern-tab"); if(ModernList.Items.Count!=ModernPicks.Length)throw new InvalidOperationException("Modern shortlist was not populated."); ModernQuery.Text="The Grants"; ModernList.SelectedIndex=0; if(!ModernDownloadButton.IsEnabled)throw new InvalidOperationException("Direct modern download was not available."); ModernQuery.Text="Clairo"; if(ModernList.Items.Count!=7)throw new InvalidOperationException("Modern artist filter failed."); ModernQuery.Text="Runaway"; if(ModernList.Items.Count!=1)throw new InvalidOperationException("Modern title filter failed."); ModernList.SelectedIndex=0; if(!ModernSearchButton.IsEnabled || !ModernLibraryButton.IsEnabled)throw new InvalidOperationException("Modern pick actions were not enabled."); FindModernInLibrary(this,new System.Windows.RoutedEventArgs()); if(MainTabs.SelectedIndex!=3 || LibraryQuery.Text!="Runaway")throw new InvalidOperationException("Modern library search failed."); MainTabs.SelectedItem=ModernTab; await SearchModernAsync(); if(MainTabs.SelectedIndex!=1 || SearchQuery.Text!="Kanye West Runaway" || SearchResults.Items.Count!=1)throw new InvalidOperationException("Modern source search failed."); LibraryQuery.Text=""; result["modernPicksSearch"]=true;
   MainTabs.SelectedIndex=1; if(MainTabs.SelectedIndex!=1)throw new InvalidOperationException("Discover navigation failed.");
   CatalogSelector.SelectedIndex=1; SearchQuery.Text="Workflow test song"; searchDebouncer?.Cancel(); await SearchOnlineAsync(0,false,CancellationToken.None);
   if(SearchResults.Items.Count!=1)throw new InvalidOperationException("Search result was not displayed: "+DiscoverStatus.Text);
   SearchResults.SelectedIndex=0; if(!ResultDetails.Text.Contains("Workflow test song",StringComparison.Ordinal))throw new InvalidOperationException("Selection details missing.");
   await DownloadSelectedAsync(); if(pendingEntry==null || currentInspection==null)throw new InvalidOperationException("Download or inspection failed: "+DiscoverStatus.Text);
   if(trackChoices.Count<2 || currentInspection.Song.Notes.Count()==0 || string.IsNullOrWhiteSpace(InspectionText.Text))throw new InvalidOperationException("Expected a multi-track fixture and notes.");
   foreach(var track in trackChoices)track.Selected=track.Index==1; var expected=currentInspection.Song.Tracks.Single(t=>t.Index==1).Notes.Count;
   if(expected==0)throw new InvalidOperationException("Selected track contains no notes.");
   var wait=System.Diagnostics.Stopwatch.StartNew(); while(PreflightText.Text=="Calculating compatibility…" && wait.Elapsed<TimeSpan.FromSeconds(5))await Task.Delay(20); if(!PreflightText.Text.Contains($"Selected notes: {expected}",StringComparison.Ordinal))throw new InvalidOperationException("Selected-track preflight was not displayed.");
   if(!await ImportSelectedAsync(true))throw new InvalidOperationException("Player import failed: "+DiscoverStatus.Text);
   if(MainTabs.SelectedIndex!=0 || song==null || song.Tracks.Count!=1 || song.Tracks[0].Index!=1 || song.Notes.Count()!=expected)throw new InvalidOperationException("Selected tracks were not committed to Player.");
   if(!Status.Text.Contains("verify your mapping",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unverified mapping did not block playback.");
   if(SongTitle.Text!="Workflow test song" || !SongSource.Text.Contains("MIDIsss",StringComparison.Ordinal) || SongSource.ToolTip is not string sourceDetails || !sourceDetails.Contains(pendingEntry.Source,StringComparison.Ordinal) || !sourceDetails.Contains(pendingEntry.License,StringComparison.Ordinal) || !sourceDetails.Contains(pendingEntry.Sha256,StringComparison.Ordinal))throw new InvalidOperationException("Player metadata was not retained.");
   if(!Progress.IsEnabled)throw new InvalidOperationException("Timeline was not enabled for the imported song."); Progress.Value=50; if(selectedPosition is not TimeSpan chosen || Math.Abs((chosen-songDuration/2).TotalMilliseconds)>1)throw new InvalidOperationException("Timeline did not select the requested position."); Progress.Value=0; if(selectedPosition!=TimeSpan.Zero)throw new InvalidOperationException("Timeline could not return to the beginning."); result["timelineSelection"]=true;
   var pitches=song.Notes.Select(n=>n.Pitch).Distinct().Order().ToArray(); const string testKeys="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
   if(pitches.Length>testKeys.Length)throw new InvalidOperationException("Fixture needs too many test bindings.");
   foreach(var row in rows) { var index=Array.IndexOf(pitches,row.Pitch); row.Key=index<0?"":testKeys[index].ToString(); }
   Status.Text="Manually verify your mapping for this piano before playback.";
   VerifiedCheck.IsChecked=true; VerifyProfile(this,new System.Windows.RoutedEventArgs());
   if(!profile.Verified || Status.Text!="Ready")throw new InvalidOperationException("Verified mapping left a stale Player error.");
   VerifiedCheck.IsChecked=false; VerifyProfile(this,new System.Windows.RoutedEventArgs());
   if(profile.Verified || !Status.Text.Contains("verify your mapping",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Removing verification did not restore the Player prerequisite message.");
   result["mappingStatusRefreshed"]=true;
   if(LibraryList.Items.Count!=1)throw new InvalidOperationException("Expected one isolated library entry."); var target=new Keynote.Input.RobloxWindow((nint)123,"Workflow Roblox",456); Windows.ItemsSource=new[]{target}; Windows.SelectedItem=target; if(input.Target!=target.Handle || input.TargetProcessId!=target.ProcessId)throw new InvalidOperationException("Player window selection did not set the target."); PlayerSongSelector.ItemsSource=new[]{pendingEntry with {RecommendedOctave=-1}}; PlayerSongSelector.SelectedIndex=0; var selectionWait=System.Diagnostics.Stopwatch.StartNew(); while(!LibraryStatus.Text.StartsWith("Selected:",StringComparison.Ordinal) && selectionWait.Elapsed<TimeSpan.FromSeconds(5))await Task.Delay(20);
   if(!LibraryStatus.Text.StartsWith("Selected:",StringComparison.Ordinal) || song?.Name!="Workflow test song" || arm!=null || !hotkeyController.Ready || Shift!=-1)throw new InvalidOperationException("Library selection did not prepare the song and suggested octave: "+LibraryStatus.Text);
   if(!File.Exists(PlayerSettingsPath))throw new InvalidOperationException("Mapping preferences were not saved.");
   result["librarySelectionReady"]=true; result["playerSelectorReady"]=true; if(input.Target!=target.Handle || input.TargetProcessId!=target.ProcessId)throw new InvalidOperationException("Song selection changed the target."); result["playerWindowSelected"]=true;
   MainTabs.SelectedIndex=0; await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture("player-loaded");
   MainTabs.SelectedItem=RemappedTab; await LoadRemappedAsync(); if(RemappedList.Items.Count!=1)throw new InvalidOperationException("Remapped catalogue did not load."); RemappedList.SelectedIndex=0; await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture("remapped-tab"); await DownloadRemappedAsync(); if(MainTabs.SelectedIndex!=0 || PlayerSongSelector.SelectedItem is not LibraryEntry selectedRemap || selectedRemap.RecommendedOctave!=1)throw new InvalidOperationException("Remapped song download or selection failed: "+RemappedStatus.Text); result["remappedDownloadAndSelection"]=true; ApplyRemappedProfile(); if(profile.Verified || profile.Keys.Count!=61)throw new InvalidOperationException("The bundled piano layout was not loaded safely."); result["unverifiedReferenceLayout"]=true;
   result["success"]=true; result["notes"]=expected; result["sha256"]=pendingEntry.Sha256; result["provider"]=pendingEntry.Provider; result["selectedTrack"]=1; result["dataRoot"]=AppDataRoot;
  } catch(Exception ex) { result["success"]=false; result["error"]=ex.ToString(); }
  await File.WriteAllTextAsync(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})); Close();
 }
}
