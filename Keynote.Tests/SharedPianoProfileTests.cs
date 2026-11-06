using Keynote.Core;
namespace Keynote.Tests;
public class SharedPianoProfileTests {
 static KeyboardMapping Reference()=>new(){Name="Visual Pianos reference layout (not verified)",Keys=new(){{60,"T"},{61,"SHIFT+T"}}};
 [Fact] public void AcceptsEmptyDefault() => Assert.True(SharedPianoProfile.ShouldApply(new(){Name="Empty mapping"},Reference()));
 [Fact] public void AcceptsUnchangedPreviousReference() => Assert.True(SharedPianoProfile.ShouldApply(Reference(),Reference()));
 [Fact] public void PreservesChangedReference() {var current=Reference();current.Keys[60]="Q";Assert.False(SharedPianoProfile.ShouldApply(current,Reference()));}
 [Fact] public void PreservesCustomAndVerifiedProfiles() {var custom=Reference();custom.Name="My mapping";Assert.False(SharedPianoProfile.ShouldApply(custom,Reference()));var verified=Reference();verified.Verified=true;Assert.False(SharedPianoProfile.ShouldApply(verified,Reference()));}
}
