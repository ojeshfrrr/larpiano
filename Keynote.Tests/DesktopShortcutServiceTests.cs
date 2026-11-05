using Keynote.Infrastructure;
namespace Keynote.Tests;
public class DesktopShortcutServiceTests {
 [Fact] public void CreatesOnceAndRespectsRemoval() {
  var root=Path.Combine(Path.GetTempPath(),"larpiano-shortcut-test-"+Guid.NewGuid().ToString("N"));var desktop=Path.Combine(root,"desktop");var state=Path.Combine(root,"state");Directory.CreateDirectory(desktop);var exe=Path.Combine(root,"Keynote.App.exe");File.WriteAllText(exe,"app");var calls=0;
  try { void Create(string path,string target){calls++;Assert.Equal(exe,target);File.WriteAllText(path,"shortcut");} DesktopShortcutService.Ensure(exe,desktop,state,Create);Assert.Equal(1,calls);File.Delete(Path.Combine(desktop,"larpiano.lnk"));DesktopShortcutService.Ensure(exe,desktop,state,Create);Assert.Equal(1,calls);Assert.False(File.Exists(Path.Combine(desktop,"larpiano.lnk"))); } finally {Directory.Delete(root,true);}
 }
 [Fact] public void DoesNotOverwriteExistingShortcut() {
  var root=Path.Combine(Path.GetTempPath(),"larpiano-shortcut-test-"+Guid.NewGuid().ToString("N"));var desktop=Path.Combine(root,"desktop");Directory.CreateDirectory(desktop);var exe=Path.Combine(root,"Keynote.App.exe");File.WriteAllText(exe,"app");var link=Path.Combine(desktop,"larpiano.lnk");File.WriteAllText(link,"original");
  try {DesktopShortcutService.Ensure(exe,desktop,Path.Combine(root,"state"),(p,t)=>throw new Exception("must not overwrite"));Assert.Equal("original",File.ReadAllText(link));}finally{Directory.Delete(root,true);}
 }
 [Fact] public void MovedAppCanCreateMissingShortcut() {
  var root=Path.Combine(Path.GetTempPath(),"larpiano-shortcut-test-"+Guid.NewGuid().ToString("N"));var desktop=Path.Combine(root,"desktop");var state=Path.Combine(root,"state");Directory.CreateDirectory(desktop);var first=Path.Combine(root,"first.exe");var second=Path.Combine(root,"second.exe");File.WriteAllText(first,"app");File.WriteAllText(second,"app");
  try {void Create(string p,string t)=>File.WriteAllText(p,t); DesktopShortcutService.Ensure(first,desktop,state,Create);File.Delete(Path.Combine(desktop,"larpiano.lnk"));DesktopShortcutService.Ensure(second,desktop,state,Create);Assert.Equal(second,File.ReadAllText(Path.Combine(desktop,"larpiano.lnk")));}finally{Directory.Delete(root,true);}
 }
}
