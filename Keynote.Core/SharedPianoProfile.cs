namespace Keynote.Core;
public static class SharedPianoProfile {
 public static KeyboardMapping Create(KeyboardMapping reference) => new() {
  Name="Shared Visual Pianos layout (C4/C#4/D4 tested)", Verified=true,
  Keys=new Dictionary<int,string>(reference.Keys)
 };
}
