using Keynote.Online;
namespace Keynote.Tests;
public class ModernSongMatchingTests {
 static RemappedSong Song(string title,string artist="Kanye West",string id="1")=>new(id,title,artist,"source","license","profile",0,1,0);
 [Fact] public void MatchesDescribedArrangement() {var a=Song("Runaway (short arrangement) [octave-adjusted 144]");Assert.Same(a,ModernSongMatching.FindShared("Kanye West","Runaway",new[]{a}));}
 [Fact] public void PrefersExactTitle() {var a=Song("Runaway"); var b=Song("Runaway (short arrangement)",id:"2");Assert.Same(a,ModernSongMatching.FindShared("Kanye West","Runaway",new[]{a,b}));}
 [Fact] public void RejectsDifferentSongAndArtist() {Assert.Null(ModernSongMatching.FindShared("Kanye West","Runaway",new[]{Song("Runaway Train"),Song("Runaway (short)","Another Artist")}));}
 [Fact] public void DoesNotGuessBetweenVariants() {Assert.Null(ModernSongMatching.FindShared("Kanye West","Runaway",new[]{Song("Runaway (short)"),Song("Runaway [full]",id:"2")}));}
}

