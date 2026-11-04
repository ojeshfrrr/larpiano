using System.Diagnostics;
using Keynote.Core;
namespace Keynote.Playback;
public record PlaybackDiagnostics(int Scheduled, int Dispatched, int Failed, int Late, double AverageLateMs, double P95LateMs, double MaxLateMs, int CleanupFailures, int FocusLosses);
public sealed class PlaybackEngine : IDisposable {
 readonly IKeyboardInput input; readonly object gate=new(); readonly Dictionary<string,int> active=new(StringComparer.OrdinalIgnoreCase);
 PlaybackEvent[] timelineEvents=Array.Empty<PlaybackEvent>(); int eventIndex;
 CancellationTokenSource? cancellation; Task? worker; PlaybackState state=PlaybackState.Idle; string? error; double speed=1,positionSeconds; long anchor; bool disposed,inputPausePending;
 int scheduled,dispatched,failed,late,cleanupFailures,focusLosses; double lateSum,maxLate; readonly int[] latenessHistogram=new int[2001];
 public PlaybackEngine(IKeyboardInput input)=>this.input=input ?? throw new ArgumentNullException(nameof(input));
 public PlaybackState State { get { lock(gate)return state; } } public string? Error { get { lock(gate)return error; } }
 public TimeSpan Position { get { lock(gate) { Advance(); return TimeSpan.FromSeconds(positionSeconds); } } }
 public double Speed { get { lock(gate)return speed; } set { if(!double.IsFinite(value)||value<.5||value>2)throw new ArgumentOutOfRangeException(nameof(value)); lock(gate) { Advance(); speed=value; } } }
 public PlaybackDiagnostics Diagnostics { get { lock(gate) { int target=(int)Math.Ceiling(dispatched*.95),sum=0,p95=0; if(target>0) for(int i=0;i<latenessHistogram.Length;i++) { sum+=latenessHistogram[i]; if(sum>=target) { p95=i; break; } } return new(scheduled,dispatched,failed,late,dispatched==0?0:lateSum/dispatched,p95,maxLate,cleanupFailures,focusLosses); } } }
 void Advance() { long now=Stopwatch.GetTimestamp(); if(state==PlaybackState.Playing) positionSeconds+=(now-anchor)/(double)Stopwatch.Frequency*speed; anchor=now; }
 void RecordDispatch(double ms) { ms=Math.Max(0,ms); dispatched++; if(ms>10)late++; lateSum+=ms; maxLate=Math.Max(maxLate,ms); latenessHistogram[Math.Min(2000,(int)Math.Floor(ms))]++; }
 void Cleanup() { try { input.ReleaseAll(); } catch(Exception ex) { cleanupFailures++; error="Key release failed: "+ex.Message; state=PlaybackState.Faulted; throw; } }
 public static PlaybackEvent[] BuildEvents(MidiSong song,KeyboardMapping mapping,int octave=0) {
  ArgumentNullException.ThrowIfNull(song); ArgumentNullException.ThrowIfNull(mapping);
  if(mapping.Missing(song,octave).Length>0)throw new InvalidOperationException("Map every song note before playback.");
  if(mapping.Duplicates().Any())throw new InvalidOperationException("Resolve duplicate key assignments before playback.");
  if(mapping.Keys.Any(p=>!string.IsNullOrWhiteSpace(p.Value)&&!KeyboardMapping.IsValidKey(p.Value)))throw new InvalidOperationException("Mapped keys must be A–Z, 0–9, or Shift+key.");
  var notes=song.Notes.ToArray(); if(notes.Any(n=>n.Pitch is <0 or >127 || n.Start<TimeSpan.Zero || n.Duration<TimeSpan.Zero))throw new InvalidOperationException("Song contains invalid notes.");
  var events=notes.SelectMany((n,i)=>new[]{new PlaybackEvent(n.Start,mapping.Lookup(n.Pitch,octave)!.Trim().ToUpperInvariant(),true,i),new PlaybackEvent(n.Start+(n.Duration>TimeSpan.Zero?n.Duration:TimeSpan.FromTicks(1)),mapping.Lookup(n.Pitch,octave)!.Trim().ToUpperInvariant(),false,i)}).OrderBy(e=>e.Time).ThenBy(e=>e.IsDown?1:0).ThenBy(e=>e.Order).ToArray();
  var physical=new Dictionary<char,(string Binding,int Count)>();
  foreach(var e in events) { KeyboardMapping.TryParseKey(e.Key,out var key,out _); var current=physical.GetValueOrDefault(key); if(e.IsDown) { if(current.Count>0 && current.Binding!=e.Key)throw new InvalidOperationException($"Overlapping notes require both {current.Binding} and {e.Key} on the same physical key. Select another track or arrangement."); physical[key]=(e.Key,current.Count+1); } else if(current.Count>1)physical[key]=(e.Key,current.Count-1); else physical.Remove(key); }
  return events;
 }
 void SetPosition(TimeSpan position) {
  active.Clear(); eventIndex=0;
  while(eventIndex<timelineEvents.Length && (timelineEvents[eventIndex].Time<position || (timelineEvents[eventIndex].Time==position && !timelineEvents[eventIndex].IsDown))) {
   var e=timelineEvents[eventIndex++]; var count=active.GetValueOrDefault(e.Key);
   if(e.IsDown)active[e.Key]=count+1; else if(count>1)active[e.Key]=count-1; else active.Remove(e.Key);
  }
  positionSeconds=position.TotalSeconds; anchor=Stopwatch.GetTimestamp();
 }
 public void Seek(TimeSpan position) {
  lock(gate) {
   ObjectDisposedException.ThrowIf(disposed,this);
   if(state is not (PlaybackState.Playing or PlaybackState.Paused) || cancellation?.IsCancellationRequested!=false)throw new InvalidOperationException("No playback session is available to seek.");
   if(position<TimeSpan.Zero || position>timelineEvents[^1].Time)throw new ArgumentOutOfRangeException(nameof(position));
   if(state==PlaybackState.Playing) { Advance(); state=PlaybackState.Paused; }
   try { Cleanup(); } catch { cancellation.Cancel(); throw; }
   SetPosition(position); inputPausePending=false; error=null;
  }
 }
 public void Start(MidiSong song,KeyboardMapping mapping,int octave=0,TimeSpan? startPosition=null) {
  var events=BuildEvents(song,mapping,octave); if(events.Length==0)throw new InvalidOperationException("The MIDI file contains no playable notes.");
  var start=startPosition ?? TimeSpan.Zero; if(start<TimeSpan.Zero || start>events[^1].Time)throw new ArgumentOutOfRangeException(nameof(startPosition));
  lock(gate) { ObjectDisposedException.ThrowIf(disposed,this); if(worker is {IsCompleted:false})throw new InvalidOperationException("Playback is already running."); if(!input.IsTargetForeground)throw new InvalidOperationException("Select and focus the Roblox window first."); Cleanup(); cancellation?.Dispose(); cancellation=new(); timelineEvents=events; SetPosition(start); error=null;
   try { foreach(var key in active.Keys) { if(!input.IsTargetForeground)throw new InvalidOperationException("Focus the selected Roblox window before starting."); input.Press(key); } }
   catch { try { Cleanup(); } finally { active.Clear(); } throw; }
   anchor=Stopwatch.GetTimestamp(); state=PlaybackState.Playing; scheduled=events.Length; dispatched=failed=late=cleanupFailures=focusLosses=0; lateSum=maxLate=0; Array.Clear(latenessHistogram); var token=cancellation.Token; worker=Task.Run(()=>Run(token)); }
 }
 void PauseForFocus() { if(state!=PlaybackState.Playing)return; Advance(); focusLosses++; state=PlaybackState.Paused; error="Focus lost — return to Roblox and resume."; Cleanup(); }
 void PauseForInput(string reason) { Advance(); state=PlaybackState.Paused; inputPausePending=true; error=reason+" Playback paused. Release keys, then resume."; Cleanup(); }
 public bool AcknowledgeInputPause() { lock(gate) { if(state!=PlaybackState.Paused || !inputPausePending)return false; inputPausePending=false; return true; } }
 async Task Run(CancellationToken token) {
  Exception? failure=null;
  try { while(true) { double delay; lock(gate) {
   token.ThrowIfCancellationRequested();
   if(state==PlaybackState.Paused && inputPausePending && !input.IsTargetForeground)inputPausePending=false;
   if(state==PlaybackState.Playing && !input.IsTargetForeground) PauseForFocus();
   Advance();
   if(state==PlaybackState.Playing) {
    int batch=0; while(eventIndex<timelineEvents.Length && timelineEvents[eventIndex].Time.TotalSeconds<=positionSeconds && batch++<32) {
     token.ThrowIfCancellationRequested(); Advance(); if(!input.IsTargetForeground) { PauseForFocus(); break; }
     var e=timelineEvents[eventIndex]; var count=active.GetValueOrDefault(e.Key); bool sent=false;
     try { if(e.IsDown) { if(count==0) { input.Press(e.Key); sent=true; } active[e.Key]=count+1; } else if(count>0) { if(count==1) { input.Release(e.Key); sent=true; active.Remove(e.Key); } else active[e.Key]=count-1; } }
     catch(KeyboardInputBusyException ex) { PauseForInput(ex.Message); break; }
     catch(Exception) { failed++; if(!input.IsTargetForeground) { PauseForFocus(); break; } throw; }
     if(sent) RecordDispatch((positionSeconds-e.Time.TotalSeconds)/speed*1000); eventIndex++;
    }
    if(eventIndex==timelineEvents.Length) { positionSeconds=timelineEvents[^1].Time.TotalSeconds; break; }
    delay=state==PlaybackState.Playing?Math.Clamp((timelineEvents[eventIndex].Time.TotalSeconds-positionSeconds)/speed*1000,1,10):10;
   } else delay=10;
  } await Task.Delay(TimeSpan.FromMilliseconds(delay),token).ConfigureAwait(false); }
  } catch(OperationCanceledException) when(token.IsCancellationRequested) { } catch(Exception ex) { failure=ex; }
  finally { lock(gate) { inputPausePending=false; if(failure!=null) { error=failure.Message; state=PlaybackState.Faulted; } try { Cleanup(); } catch { } active.Clear(); if(state!=PlaybackState.Faulted) { state=PlaybackState.Stopped; error=null; } } }
 }
 public void Pause() { lock(gate) { if(state!=PlaybackState.Playing)return; Advance(); state=PlaybackState.Paused; error=null; try { Cleanup(); } catch { cancellation?.Cancel(); } } }
 public void Resume() { lock(gate) { if(state!=PlaybackState.Paused || cancellation?.IsCancellationRequested==true)return; if(!input.IsTargetForeground) { error="Focus the selected Roblox window before resuming."; return; } try { foreach(var key in active.Keys) { if(!input.IsTargetForeground) { Cleanup(); error="Focus the selected Roblox window before resuming."; return; } input.Press(key); } anchor=Stopwatch.GetTimestamp(); inputPausePending=false; error=null; state=PlaybackState.Playing; } catch(KeyboardInputBusyException ex) { try { PauseForInput(ex.Message); } catch { cancellation?.Cancel(); } } catch(Exception ex) { if(!input.IsTargetForeground) { try { Cleanup(); error="Focus the selected Roblox window before resuming."; } catch { cancellation?.Cancel(); } return; } failed++; error=ex.Message; state=PlaybackState.Faulted; cancellation?.Cancel(); } } }
 public async Task StopAsync() { Task? task; lock(gate) { cancellation?.Cancel(); task=worker; } if(task!=null)await task.ConfigureAwait(false); lock(gate) { try { Cleanup(); } catch { } } }
 public void Dispose() { lock(gate) { if(disposed)return; } StopAsync().GetAwaiter().GetResult(); input.Dispose(); lock(gate) { disposed=true; cancellation?.Dispose(); } }
}
