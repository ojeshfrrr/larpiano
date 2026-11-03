using System.Diagnostics;
using System.Runtime.InteropServices;
using Keynote.Core;
namespace Keynote.Input;
public record RobloxWindow(nint Handle, string Title, uint ProcessId) { public override string ToString() => Title; }
public interface IWindowsInputPlatform {
 bool IsTargetForeground(nint target, uint expectedProcessId);
 bool IsKeyDown(ushort key);
 void SendKey(ushort key, bool up);
}
public sealed class WindowsKeyboardInput : IKeyboardInput {
 readonly IWindowsInputPlatform platform;
 public WindowsKeyboardInput() : this(new NativeWindowsInputPlatform()) { }
 public WindowsKeyboardInput(IWindowsInputPlatform platform) => this.platform=platform ?? throw new ArgumentNullException(nameof(platform));
 readonly HashSet<ushort> held = new(); readonly object gate = new(); public nint Target { get; set; } public uint TargetProcessId { get; set; }
 public static IReadOnlyList<RobloxWindow> FindWindows() { var result = new List<RobloxWindow>(); EnumWindows((h,l) => { if(IsWindowVisible(h) && IsRoblox(h)) { var title=new char[512]; int length=GetWindowText(h,title,title.Length); if(GetWindowThreadProcessId(h,out var pid)!=0 && pid!=0) result.Add(new(h,length>0 ? new string(title,0,length) : "(Untitled Roblox window)",pid)); } return true; },0); return result; }
 static bool IsRoblox(nint h) { if(GetWindowThreadProcessId(h,out var id)==0 || id==0)return false; try { using var p=Process.GetProcessById((int)id); return p.ProcessName.Equals("RobloxPlayerBeta",StringComparison.OrdinalIgnoreCase); } catch { return false; } }
 public bool IsTargetForeground => platform.IsTargetForeground(Target,TargetProcessId);
 public string[] HeldKeys { get { lock(gate) return held.Order().Select(k=>k==LeftShift?"Shift":bindings.GetValueOrDefault(k,((char)k).ToString())).ToArray(); } }
 const ushort LeftShift=0xA0;
 readonly Dictionary<ushort,string> bindings=new();
 static ushort Parse(string key,out bool shifted) { if(KeyboardMapping.TryParseKey(key,out var value,out shifted))return value; throw new ArgumentException("Use A–Z, 0–9, or Shift+key."); }
 public void Press(string key) { lock(gate) {
  if(!IsTargetForeground)throw new InvalidOperationException("Roblox is not the foreground window.");
  var k=Parse(key,out var shifted); var binding=key.Trim().ToUpperInvariant();
  if(bindings.TryGetValue(k,out var existing)) { if(existing==binding)return; throw new InvalidOperationException("A natural and shifted note share a held physical key. Shorten overlapping notes or choose another arrangement."); }
  if(held.Contains(LeftShift))throw new InvalidOperationException("A previous Shift release failed; stop and retry cleanup.");
  if(platform.IsKeyDown(k) || new ushort[]{0x10,0xA0,0xA1,0x11,0x12,0x5B,0x5C}.Any(platform.IsKeyDown))throw new KeyboardInputBusyException("Release mapped keys and keyboard modifiers before playback.");
  try {
   if(shifted) { platform.SendKey(LeftShift,false); held.Add(LeftShift); }
   if(!IsTargetForeground)throw new InvalidOperationException("Roblox lost foreground focus.");
   platform.SendKey(k,false); held.Add(k); bindings[k]=binding;
  } finally { if(shifted && held.Contains(LeftShift)) { platform.SendKey(LeftShift,true); held.Remove(LeftShift); } }
 } }
 public void Release(string key) { lock(gate) { var k=Parse(key,out _); if(!bindings.TryGetValue(k,out var binding) || binding!=key.Trim().ToUpperInvariant())return; platform.SendKey(k,true); held.Remove(k); bindings.Remove(k); } }
 public void ReleaseAll() { lock(gate) { var failures=new List<Exception>(); foreach(var k in held.ToArray()) { try { platform.SendKey(k,true); held.Remove(k); bindings.Remove(k); } catch(Exception ex) { failures.Add(ex); } } if(failures.Count>0) throw new AggregateException("One or more held keys could not be released; retry cleanup.",failures); } }
 sealed class NativeWindowsInputPlatform : IWindowsInputPlatform {
 public bool IsTargetForeground(nint target,uint expectedProcessId) { if(target==0 || expectedProcessId==0 || GetForegroundWindow()!=target || !IsWindow(target)) return false; if(GetWindowThreadProcessId(target,out var pid)==0)return false; return pid==expectedProcessId && IsRoblox(target); }
 public bool IsKeyDown(ushort key) => (GetAsyncKeyState(key) & 0x8000) != 0;
 public void SendKey(ushort key,bool up) { var data=new INPUT { type=1, U=new InputUnion { ki=CreateScanCodeInput(MapVirtualKey(key,4),up) } }; if(SendInput(1,new[]{data},Marshal.SizeOf<INPUT>())!=1) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"SendInput failed. The game may block synthetic input."); }
 }
 internal static KEYBDINPUT CreateScanCodeInput(uint scanCode,bool up) { if(scanCode==0 || scanCode>0xE0FF || (scanCode>0xFF && (scanCode & 0xFF00)!=0xE000) || (scanCode & 0xFF)==0)throw new InvalidOperationException("Windows could not map this key to a supported keyboard scan code."); return new() { wScan=(ushort)(scanCode & 0xFF),dwFlags=0x0008u | (up?0x0002u:0u) | ((scanCode & 0xFF00)==0xE000?0x0001u:0u) }; }
 public void Dispose() => ReleaseAll();
 [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
 [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
 [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public nuint dwExtraInfo; }
 [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx,dy; public uint mouseData,dwFlags,time; public nuint dwExtraInfo; }
 delegate bool EnumProc(nint h,nint l);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,nint l);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(nint h);
 [DllImport("user32.dll")] static extern bool IsWindow(nint h);
 [DllImport("user32.dll")] static extern nint GetForegroundWindow();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(nint h,[Out] char[] text,int length);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint h,out uint id);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll",EntryPoint="MapVirtualKeyW")] static extern uint MapVirtualKey(uint code,uint mapType);
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,INPUT[] inputs,int size);
}
