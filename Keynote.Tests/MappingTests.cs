using Keynote.Core;
using Keynote.Infrastructure;
using Keynote.Playback;
namespace Keynote.Tests;
public class MappingTests {
 [Theory] [InlineData("SHIFT+A",true)] [InlineData(" shift+1 ",true)] [InlineData("SHIFT+SHIFT+A",false)] [InlineData("CTRL+A",false)] [InlineData("!",false)]
 public void ShiftMappingsAreExplicitAndRestricted(string key,bool valid)=>Assert.Equal(valid,KeyboardMapping.IsValidKey(key));
 [Fact] public void RejectsOverlappingNaturalAndShiftedPhysicalKey() { var song=new MidiSong("collision",new[]{new MidiTrack(0,new[]{new MidiNote(60,90,TimeSpan.Zero,TimeSpan.FromSeconds(1),0),new MidiNote(61,90,TimeSpan.Zero,TimeSpan.FromSeconds(1),0)})}); var map=new KeyboardMapping{Keys=new(){{60,"A"},{61,"SHIFT+A"}}}; Assert.Throws<InvalidOperationException>(()=>PlaybackEngine.BuildEvents(song,map)); }
 [Fact] public void AllowsSequentialNaturalAndShiftedPhysicalKey() { var song=new MidiSong("sequence",new[]{new MidiTrack(0,new[]{new MidiNote(60,90,TimeSpan.Zero,TimeSpan.FromSeconds(1),0),new MidiNote(61,90,TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),0)})}); var map=new KeyboardMapping{Keys=new(){{60,"A"},{61,"SHIFT+A"}}}; Assert.Equal(4,PlaybackEngine.BuildEvents(song,map).Length); }
 static MidiSong Song()=>new("x",new[]{new MidiTrack(0,new[]{new MidiNote(60,90,TimeSpan.Zero,TimeSpan.FromSeconds(1),0)})});
 [Fact] public void OctaveAdjustmentLooksUpShiftedPitch() { var map=new KeyboardMapping { Keys=new(){{72,"Q"}} }; Assert.Null(map.Lookup(60)); Assert.Equal("Q",map.Lookup(60,1)); Assert.Empty(map.Missing(Song(),1)); Assert.Equal(new[]{60},map.Missing(Song())); }
 [Fact] public void InvalidAssignmentsAreRejectedBeforePlayback() { var map=new KeyboardMapping { Keys=new(){{60,"Ctrl+A"}} }; Assert.Throws<InvalidOperationException>(()=>PlaybackEngine.BuildEvents(Song(),map)); }
 [Fact] public async Task ProfileWithOutOfRangePitchIsRejected() { var file=Path.GetTempFileName(); try { await File.WriteAllTextAsync(file,"{\"Keys\":{\"128\":\"A\"}}"); await Assert.ThrowsAsync<InvalidDataException>(()=>new ProfileStore().LoadAsync(file)); } finally { File.Delete(file); } }
 [Fact] public void MissingAndDuplicateComparisonIgnoresWhitespaceAndCase() { var map=new KeyboardMapping { Keys=new(){{60," A "},{61,"a"}} }; Assert.Single(map.Duplicates()); Assert.Empty(map.Missing(Song())); }
}
