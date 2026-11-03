using Keynote.Core;
namespace Keynote.Playback;
/// <summary>Routes hotkeys without changing window activation. All delegates are invoked on the caller's thread.</summary>
public sealed class PlaybackHotkeyController(Func<PlaybackState> state, Func<bool> foreground, Action start, Action pause, Action resume, Func<Task> stop,Func<bool>? acknowledgeInputPause=null) {
 bool ready;
 public bool Ready => ready;
 public void SetReady(bool value) => ready=value;
 public void Toggle() {
  if(state()==PlaybackState.Paused && acknowledgeInputPause?.Invoke()==true)return;
  switch(state()) {
   case PlaybackState.Playing: pause(); break;
   case PlaybackState.Paused: if(foreground()) resume(); break;
   case PlaybackState.Idle or PlaybackState.Stopped or PlaybackState.Faulted when ready && foreground(): ready=false; try { start(); } catch { ready=true; throw; } break;
  }
 }
 public Task EmergencyStop() { ready=false; return stop(); }
}
