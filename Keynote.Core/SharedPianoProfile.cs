namespace Keynote.Core;
public static class SharedPianoProfile {
 public static bool ShouldApply(KeyboardMapping current,KeyboardMapping reference) {
  if(current.Keys.Count==0 && string.Equals(current.Name,"Empty mapping",StringComparison.Ordinal))return true;
  if(current.Verified || !string.Equals(current.Name,"Visual Pianos reference layout (not verified)",StringComparison.Ordinal) || current.Keys.Count!=reference.Keys.Count)return false;
  return current.Keys.All(k=>reference.Keys.TryGetValue(k.Key,out var value) && string.Equals(value,k.Value,StringComparison.OrdinalIgnoreCase));
 }
}
