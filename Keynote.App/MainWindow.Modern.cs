using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Keynote.Online;
namespace Keynote.App;

public partial class MainWindow {
 sealed record ModernPick(string Artist,string Title,RemappedSong? Shared=null) { public override string ToString() => $"{Title}  •  {Artist}"+(Shared!=null?"  •  Friends":""); }
 IReadOnlyList<RemappedSong> friendsSongs=Array.Empty<RemappedSong>();
 string? friendsCode;
 static readonly ModernPick[] ModernPicks = {
  new("Kanye West","Runaway"),new("Kanye West","I Wonder"),new("Kanye West","Heartless"),new("Kanye West","Flashing Lights"),new("Kanye West","Ghost Town"),new("Kanye West","Violent Crimes"),new("Kanye West","Hey Mama"),
  new("Kanye West","Jail"),
  new("Kanye West","Pure Souls"),
  new("Lana Del Rey","The Grants"),
  new("Lana Del Rey","Candy Necklace"),
  new("Clairo","Bags"),new("Clairo","Sofia"),new("Clairo","Pretty Girl"),new("Clairo","Amoeba"),new("Clairo","Bubble Gum"),new("Clairo","Juna"),new("Clairo","Sexy to Someone"),
  new("Laufey","From the Start"),new("Laufey","Valentine"),new("Laufey","Promise"),new("Laufey","Let You Break My Heart Again"),new("Laufey","Falling Behind"),new("Laufey","Bewitched"),new("Laufey","Like the Movies"),
  new("The Weeknd","Blinding Lights"),new("The Weeknd","Die for You"),new("The Weeknd","Save Your Tears"),new("The Weeknd","Starboy"),new("The Weeknd","Call Out My Name"),new("The Weeknd","The Hills"),new("The Weeknd","Out of Time"),
  new("Frank Ocean","Pink + White"),new("Frank Ocean","Ivy"),new("Frank Ocean","Thinkin Bout You"),new("Tyler, The Creator","See You Again"),new("Tyler, The Creator","EARFQUAKE"),new("Steve Lacy","Bad Habit"),new("Steve Lacy","Dark Red"),new("SZA","Snooze"),new("SZA","Good Days"),new("Billie Eilish","BIRDS OF A FEATHER"),new("Billie Eilish","WILDFLOWER"),new("Lana Del Rey","Young and Beautiful"),new("Lana Del Rey","Summertime Sadness"),new("Joji","Glimpse of Us"),new("Joji","SLOW DANCING IN THE DARK"),new("Daniel Caesar","Best Part"),new("Mac DeMarco","Chamber of Reflection")
 };
 void InitializeModernPicks() => ApplyModernFilter();
 void ModernFilterChanged(object sender,TextChangedEventArgs e) => ApplyModernFilter();
 void ApplyModernFilter() { if(ModernList==null || ModernQuery==null)return; var q=ModernQuery.Text.Trim(); ModernList.ItemsSource=ModernPicks.Concat(friendsSongs.Select(s=>new ModernPick(s.Artist,s.Title,s))).Where(p=>p.Artist.Contains(q,StringComparison.OrdinalIgnoreCase)||p.Title.Contains(q,StringComparison.OrdinalIgnoreCase)).ToArray(); }
 async void ConnectFriends(object sender,RoutedEventArgs e) {
  if(onlineHttp==null || library==null)return;
  FriendsConnect.IsEnabled=false;
  try { var code=FriendsCode.Password; FriendsStatus.Text="Connecting…"; var songs=await new PrivateCatalogService(onlineHttp,library).ListAsync(code,CancellationToken.None); friendsCode=code; FriendsCode.Clear(); friendsSongs=songs; ApplyModernFilter(); FriendsStatus.Text=$"Friends collection connected: {songs.Count} arrangements."; }
  catch(Exception) { friendsCode=null; friendsSongs=Array.Empty<RemappedSong>(); ApplyModernFilter(); FriendsStatus.Text="Could not connect. Check your code or try again later."; }
  finally { FriendsConnect.IsEnabled=true; }
 }
 CancellationTokenSource? modernDownloadCancellation;
 LibraryEntry? FindLocalModern(ModernPick pick) => pick.Shared!=null ? libraryEntries.FirstOrDefault(e=>e.Sha256==pick.Shared.Sha256) : libraryEntries.FirstOrDefault(e => e.Artist.Equals(pick.Artist,StringComparison.OrdinalIgnoreCase) && (e.Title.Equals(pick.Title,StringComparison.OrdinalIgnoreCase) || e.Title.StartsWith(pick.Title+" ",StringComparison.OrdinalIgnoreCase)));
 void ModernSelected(object sender,SelectionChangedEventArgs e) {
  modernDownloadCancellation?.Cancel();
  if(ModernDetails==null || ModernSearchButton==null || ModernLibraryButton==null || ModernDownloadButton==null)return;
  var pick=ModernList.SelectedItem as ModernPick; ModernSearchButton.IsEnabled=pick!=null; ModernLibraryButton.IsEnabled=pick!=null;
  var local=pick==null?null:FindLocalModern(pick); var direct=pick==null?null:ModernDownloads.Find(pick.Artist,pick.Title);
  ModernDownloadButton.IsEnabled=pick!=null && (local!=null || direct!=null || pick.Shared!=null && friendsCode!=null);
  ModernDownloadButton.Content=local!=null?"Select from library":"Download and select";
  ModernDetails.Text=pick==null?"Select a song to search for a MIDI or find it in your library.":$"{pick.Title}\n{pick.Artist}\n\n"+(local!=null?"A personal copy is available in your library.":pick.Shared!=null?"Available from the friends collection. Verify your piano mapping before playback.":direct!=null?"A free original arrangement is available from its creator. Check your piano mapping and adjust any unmapped notes before playing.":"Direct download is unavailable for this title. Search MIDI sources or import your own copy.");
 }
 async void DownloadModern(object sender,RoutedEventArgs e) {
  if(ModernList.SelectedItem is not ModernPick pick || library==null || downloads==null)return;
  modernDownloadCancellation?.Cancel(); var request=new CancellationTokenSource(); modernDownloadCancellation=request; ModernDownloadButton.IsEnabled=false;
  try {
   var local=FindLocalModern(pick);
   if(local==null) {
    ModernDetails.Text="Downloading and verifying the creator's MIDI…";
    var progress=new Progress<double>(p=> { if(ReferenceEquals(modernDownloadCancellation,request))ModernDetails.Text=$"Downloading {p:P0}…"; });
    var saved=pick.Shared!=null && friendsCode!=null && onlineHttp!=null
     ? await new PrivateCatalogService(onlineHttp,library).DownloadAsync(pick.Shared,friendsCode,request.Token)
     : await new ModernDownloads(downloads,library).DownloadAsync(ModernDownloads.Find(pick.Artist,pick.Title) ?? throw new InvalidOperationException("No direct download is available for this song."),progress,request.Token);
    request.Token.ThrowIfCancellationRequested(); await ReloadLibrary(request.Token);
    local=libraryEntries.FirstOrDefault(x=>x.Sha256==saved.Entry.Sha256) ?? throw new InvalidOperationException("The saved song was not found in your library.");
   }
   request.Token.ThrowIfCancellationRequested();
   if(!ReferenceEquals(ModernList.SelectedItem,pick))return;
   if(ReferenceEquals(PlayerSongSelector.SelectedItem,local))await LoadLibrarySelection(local,()=>PlayerSongSelector.SelectedItem as LibraryEntry);
   else PlayerSongSelector.SelectedItem=local;
   MainTabs.SelectedIndex=0;
  } catch(OperationCanceledException) { } catch(Exception ex) { if(ReferenceEquals(modernDownloadCancellation,request))ModernDetails.Text="Could not download or select this song: "+ex.Message; }
  finally { if(ReferenceEquals(modernDownloadCancellation,request)) { modernDownloadCancellation=null; var selected=ModernList.SelectedItem as ModernPick; ModernDownloadButton.IsEnabled=selected!=null && (FindLocalModern(selected)!=null || ModernDownloads.Find(selected.Artist,selected.Title)!=null || selected.Shared!=null && friendsCode!=null); } request.Dispose(); }
 }
 async void SearchModern(object sender,RoutedEventArgs e) => await SearchModernAsync();
 async Task SearchModernAsync() { if(ModernList.SelectedItem is not ModernPick pick)return; MainTabs.SelectedIndex=1; CatalogSelector.SelectedIndex=0; SearchQuery.Text=$"{pick.Artist} {pick.Title}"; searchDebouncer?.Cancel(); await SearchOnlineAsync(0,true,CancellationToken.None); }
 void FindModernInLibrary(object sender,RoutedEventArgs e) { if(ModernList.SelectedItem is not ModernPick pick)return; LibraryQuery.Text=pick.Title; MainTabs.SelectedIndex=3; }
 void ModernDoubleClick(object sender,MouseButtonEventArgs e) { if(ModernList.SelectedItem is ModernPick)SearchModern(sender,e); }
}
