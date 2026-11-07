using Keynote.Core;
using Xunit;

namespace Keynote.Tests;

public class PreparedSongMappingsTests {
 [Fact] public void UnrelatedFileIsLeftUnchanged() {
  var song=new MidiSong("test",new[]{new MidiTrack(0,new[]{new MidiNote(60,80,TimeSpan.Zero,TimeSpan.FromSeconds(1),0)})});
  Assert.Same(song,PreparedSongMappings.Apply(new string('0',64),song));
 }
 [Fact] public void KnownFileWithUnexpectedNoteCountIsRejected() {
  var song=new MidiSong("bad",Array.Empty<MidiTrack>());
  Assert.Throws<InvalidDataException>(()=>PreparedSongMappings.Apply("eb78ac1bac84f3cc6030e4075f78845a2a0c37d01286047a439e4e4141da2e44",song));
 }
 [Fact] public void CatalogueMappingRejectsWrongNoteCount() {
  var song=new MidiSong("bad",Array.Empty<MidiTrack>());
  Assert.Throws<InvalidDataException>(()=>PreparedSongMappings.Apply("c91a977918bae9fc245257452b2253cc834018c9f7fba9f4f11e911aae9335f6",song));
 }
}
