namespace Keynote.Infrastructure;

public static class DesktopShortcutService {
 public static void Ensure(string executable, string desktopDirectory, string stateDirectory, Action<string,string> createShortcut) {
  if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) || string.IsNullOrWhiteSpace(desktopDirectory) || !Directory.Exists(desktopDirectory)) return;
  var marker=Path.Combine(stateDirectory,"desktop-shortcut.txt");
  if (File.Exists(marker) && string.Equals(File.ReadAllText(marker),executable,StringComparison.OrdinalIgnoreCase)) return;
  var shortcut=Path.Combine(desktopDirectory,"larpiano.lnk");
  if (!File.Exists(shortcut)) createShortcut(shortcut,executable);
  Directory.CreateDirectory(stateDirectory);
  File.WriteAllText(marker,executable);
 }
}
