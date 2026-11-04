using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Keynote.App;

public partial class MainWindow {
 sealed record ModernPick(string Artist,string Title) { public override string ToString() => $"{Title}  •  {Artist}"; }
 static readonly ModernPick[] ModernPicks = {
  new("Kanye West","Runaway"),new("Kanye West","I Wonder"),new("Kanye West","Heartless"),new("Kanye West","Flashing Lights"),new("Kanye West","Ghost Town"),new("Kanye West","Violent Crimes"),new("Kanye West","Hey Mama"),
  new("Clairo","Bags"),new("Clairo","Sofia"),new("Clairo","Pretty Girl"),new("Clairo","Amoeba"),new("Clairo","Bubble Gum"),new("Clairo","Juna"),new("Clairo","Sexy to Someone"),
  new("Laufey","From the Start"),new("Laufey","Valentine"),new("Laufey","Promise"),new("Laufey","Let You Break My Heart Again"),new("Laufey","Falling Behind"),new("Laufey","Bewitched"),new("Laufey","Like the Movies"),
  new("The Weeknd","Blinding Lights"),new("The Weeknd","Die for You"),new("The Weeknd","Save Your Tears"),new("The Weeknd","Starboy"),new("The Weeknd","Call Out My Name"),new("The Weeknd","The Hills"),new("The Weeknd","Out of Time"),
  new("Frank Ocean","Pink + White"),new("Frank Ocean","Ivy"),new("Frank Ocean","Thinkin Bout You"),new("Tyler, The Creator","See You Again"),new("Tyler, The Creator","EARFQUAKE"),new("Steve Lacy","Bad Habit"),new("Steve Lacy","Dark Red"),new("SZA","Snooze"),new("SZA","Good Days"),new("Billie Eilish","BIRDS OF A FEATHER"),new("Billie Eilish","WILDFLOWER"),new("Lana Del Rey","Young and Beautiful"),new("Lana Del Rey","Summertime Sadness"),new("Joji","Glimpse of Us"),new("Joji","SLOW DANCING IN THE DARK"),new("Daniel Caesar","Best Part"),new("Mac DeMarco","Chamber of Reflection")
 };
 void InitializeModernPicks() => ApplyModernFilter();
 void ModernFilterChanged(object sender,TextChangedEventArgs e) => ApplyModernFilter();
 void ApplyModernFilter() { if(ModernList==null || ModernQuery==null)return; var q=ModernQuery.Text.Trim(); ModernList.ItemsSource=ModernPicks.Where(p=>p.Artist.Contains(q,StringComparison.OrdinalIgnoreCase)||p.Title.Contains(q,StringComparison.OrdinalIgnoreCase)).ToArray(); }
 void ModernSelected(object sender,SelectionChangedEventArgs e) { if(ModernDetails==null || ModernSearchButton==null || ModernLibraryButton==null)return; var pick=ModernList.SelectedItem as ModernPick; ModernSearchButton.IsEnabled=pick!=null; ModernLibraryButton.IsEnabled=pick!=null; ModernDetails.Text=pick==null?"Select a song to search for a MIDI or find it in your library.":$"{pick.Title}\n{pick.Artist}\n\nSearch results may be unavailable or have limited usage rights. Your library may contain a different arrangement."; }
 async void SearchModern(object sender,RoutedEventArgs e) => await SearchModernAsync();
 async Task SearchModernAsync() { if(ModernList.SelectedItem is not ModernPick pick)return; MainTabs.SelectedIndex=1; CatalogSelector.SelectedIndex=0; SearchQuery.Text=$"{pick.Artist} {pick.Title}"; searchDebouncer?.Cancel(); await SearchOnlineAsync(0,true,CancellationToken.None); }
 void FindModernInLibrary(object sender,RoutedEventArgs e) { if(ModernList.SelectedItem is not ModernPick pick)return; LibraryQuery.Text=pick.Title; MainTabs.SelectedIndex=3; }
 void ModernDoubleClick(object sender,MouseButtonEventArgs e) { if(ModernList.SelectedItem is ModernPick)SearchModern(sender,e); }
}
