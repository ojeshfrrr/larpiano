using System.IO;
using System.Runtime.InteropServices;
using Keynote.Infrastructure;

namespace Keynote.App;

public partial class MainWindow {
 void EnsureDesktopShortcut() {
  try {
   var executable=Environment.ProcessPath;
   if (executable==null || !Path.GetFileName(executable).Equals("Keynote.App.exe",StringComparison.OrdinalIgnoreCase)) return;
#pragma warning disable IL3000 // The portable release is a single-file executable.
   if (!string.IsNullOrEmpty(typeof(MainWindow).Assembly.Location)) return;
#pragma warning restore IL3000
   var temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if (Path.GetFullPath(executable).StartsWith(temporary,StringComparison.OrdinalIgnoreCase)) return;
   var desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
   DesktopShortcutService.Ensure(executable,desktop,AppDataRoot,CreateDesktopShortcut);
  } catch (Exception) { }
 }
 static void CreateDesktopShortcut(string path,string executable) {
  var shellType=Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows shortcut support is unavailable.");
  object? shell=null, shortcut=null;
  try {
   shell=Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Windows shortcut support is unavailable.");
   dynamic dynamicShell=shell; shortcut=dynamicShell.CreateShortcut(path); dynamic link=shortcut;
   link.TargetPath=executable; link.WorkingDirectory=Path.GetDirectoryName(executable); link.IconLocation=executable+",0"; link.Description="Open larpiano"; link.Save();
  } finally { if(shortcut!=null && Marshal.IsComObject(shortcut))Marshal.FinalReleaseComObject(shortcut); if(shell!=null && Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell); }
 }
}
