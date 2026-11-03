using System.Runtime.InteropServices;
namespace Keynote.Input;
[Flags] public enum HotkeyModifiers : uint { Alt=1, Control=2, Shift=4 }
public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey) {
 public override string ToString() => $"{(Modifiers.HasFlag(HotkeyModifiers.Control)?"Ctrl + ":"")}{(Modifiers.HasFlag(HotkeyModifiers.Alt)?"Alt + ":"")}{(Modifiers.HasFlag(HotkeyModifiers.Shift)?"Shift + ":"")}F{VirtualKey-0x6F}";
 public bool IsValid => Modifiers != 0 && (Modifiers & ~(HotkeyModifiers.Control|HotkeyModifiers.Alt|HotkeyModifiers.Shift))==0 && VirtualKey is >=0x70 and <=0x7B;
}
public enum HotkeyCommand { TogglePlayback=1, EmergencyStop=2 }
public interface IGlobalHotkeyService : IDisposable { event Action<HotkeyCommand>? Triggered; bool Register(Hotkey toggle, Hotkey emergencyStop, out string message); void Dispatch(int id); }
public interface IHotkeyPlatform { bool Register(int id,Hotkey hotkey); bool Unregister(int id); }
public sealed class GlobalHotkeyService(IHotkeyPlatform platform) : IGlobalHotkeyService {
 readonly Dictionary<int,Hotkey> registered=new(); bool disposed;
 public event Action<HotkeyCommand>? Triggered;
 public bool Register(Hotkey toggle,Hotkey emergencyStop,out string message) {
  ObjectDisposedException.ThrowIf(disposed,this); message="";
  if(!toggle.IsValid || !emergencyStop.IsValid || toggle==emergencyStop) { message="Choose two distinct shortcuts with at least one modifier and F1–F12."; return false; }
  var old=registered.ToArray();
  foreach(var item in old) { if(!platform.Unregister(item.Key)) { bool restored=true; foreach(var previous in old) if(!registered.ContainsKey(previous.Key)) { if(platform.Register(previous.Key,previous.Value)) registered[previous.Key]=previous.Value; else restored=false; } message=restored ? "Could not unregister an existing shortcut." : "Could not unregister or restore shortcuts; restart KEYNOTE."; return false; } registered.Remove(item.Key); }
  var requested=new[]{new KeyValuePair<int,Hotkey>(1,toggle),new KeyValuePair<int,Hotkey>(2,emergencyStop)};
  foreach(var item in requested) {
   if(!platform.Register(item.Key,item.Value)) {
    bool cleanupFailed=false;
    foreach(var added in registered.ToArray()) { if(platform.Unregister(added.Key)) registered.Remove(added.Key); else cleanupFailed=true; }
    bool restoreFailed=cleanupFailed;
    foreach(var previous in old) { if(registered.ContainsKey(previous.Key)) continue; if(platform.Register(previous.Key,previous.Value)) registered[previous.Key]=previous.Value; else restoreFailed=true; }
    message=restoreFailed ? "Shortcut conflict; previous shortcuts could not all be restored. Restart KEYNOTE." : "Shortcut registration failed; it may already be in use.";
    return false;
   }
   registered[item.Key]=item.Value;
  }
  return true;
 }
 public void Dispatch(int id) { if(!disposed && registered.ContainsKey(id)) Triggered?.Invoke((HotkeyCommand)id); }
 public void Dispose() { if(disposed)return; foreach(var id in registered.Keys.ToArray()) if(platform.Unregister(id)) registered.Remove(id); if(registered.Count>0) throw new InvalidOperationException("Some global shortcuts could not be unregistered."); disposed=true; }
}
public sealed class WindowsHotkeyPlatform(nint hwnd) : IHotkeyPlatform {
 const uint NoRepeat=0x4000;
 public static bool AreReleased(Hotkey hotkey) { bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0; return !Down((int)hotkey.VirtualKey) && (!hotkey.Modifiers.HasFlag(HotkeyModifiers.Control)||!Down(0x11)) && (!hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)||!Down(0x12)) && (!hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)||!Down(0x10)); }
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 public bool Register(int id,Hotkey hotkey) => RegisterHotKey(hwnd,id,(uint)hotkey.Modifiers|NoRepeat,hotkey.VirtualKey);
 public bool Unregister(int id) => UnregisterHotKey(hwnd,id);
 [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
 [DllImport("user32.dll",SetLastError=true)] static extern bool UnregisterHotKey(nint hwnd,int id);
}
