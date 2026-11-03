using System.Collections.ObjectModel;

namespace Keynote.Core;
public record MidiNote(int Pitch, int Velocity, TimeSpan Start, TimeSpan Duration, int Track);
public record MidiTrack(int Index, IReadOnlyList<MidiNote> Notes);
public record MidiSong(string Name, IReadOnlyList<MidiTrack> Tracks) {
 public IEnumerable<MidiNote> Notes => Tracks.SelectMany(t => t.Notes);
 public TimeSpan Duration => Notes.Select(n => n.Start + n.Duration).DefaultIfEmpty().Max();
}
public record PianoKey(int Pitch, string Key);
public sealed class KeyboardMapping {
 public string Name { get; set; } = "Untitled";
 public bool Verified { get; set; }
 public Dictionary<int,string> Keys { get; set; } = new();
 public static bool IsValidKey(string? key) => TryParseKey(key,out _,out _);
 public static bool TryParseKey(string? key,out char baseKey,out bool shifted) { baseKey=default; shifted=false; if(string.IsNullOrWhiteSpace(key))return false; var value=key.Trim().ToUpperInvariant(); if(value.StartsWith("SHIFT+",StringComparison.Ordinal)) { shifted=true; value=value[6..]; } if(value.Length!=1 || !((value[0]>='A' && value[0]<='Z') || (value[0]>='0' && value[0]<='9')))return false; baseKey=value[0]; return true; }
 public string? Lookup(int pitch, int octave = 0) => Keys.GetValueOrDefault(pitch + octave * 12);
 public int[] Missing(MidiSong song, int octave = 0) => song.Notes.Select(n => n.Pitch).Distinct().Where(p => string.IsNullOrWhiteSpace(Lookup(p, octave))).Order().ToArray();
 public IEnumerable<string> Duplicates() => Keys.Where(k => !string.IsNullOrWhiteSpace(k.Value)).GroupBy(k => k.Value.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
}
public record PlaybackEvent(TimeSpan Time, string Key, bool IsDown, int Order);
public record PlaybackConfiguration(double Speed = 1, int Octave = 0);
public enum PlaybackState { Idle, Ready, Playing, Paused, Stopped, Faulted }
public interface IMidiImporter { MidiSong Import(string path); }
public sealed class KeyboardInputBusyException(string message) : InvalidOperationException(message) { }
public interface IKeyboardInput : IDisposable { bool IsTargetForeground { get; } void Press(string key); void Release(string key); void ReleaseAll(); }
public static class Notes { public static string Name(int pitch) => new[]{"C","C#","D","D#","E","F","F#","G","G#","A","A#","B"}[pitch % 12] + (pitch / 12 - 1); }
