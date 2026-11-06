using Keynote.Core;
namespace Keynote.Tests;
public class SharedPianoProfileTests {
 [Fact] public void CopiesTheSharedKeyboardAndEnablesPlayback() {
  var reference=new KeyboardMapping { Name="reference",Verified=false,Keys=new(){{60,"T"},{61,"SHIFT+T"},{62,"Y"}} };
  var shared=SharedPianoProfile.Create(reference);
  Assert.True(shared.Verified); Assert.Equal(reference.Keys,shared.Keys);
  shared.Keys[60]="Q"; Assert.Equal("T",reference.Keys[60]);
 }
}
