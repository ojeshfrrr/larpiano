using System.Text.Json;
using Keynote.Core;
namespace Keynote.Infrastructure;
public sealed class ProfileStore {
 static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
 public async Task SaveAsync(string path, KeyboardMapping profile) { await using var stream = File.Create(path); await JsonSerializer.SerializeAsync(stream, profile, Options); }
 public async Task<KeyboardMapping> LoadAsync(string path) { await using var stream = File.OpenRead(path); var p = await JsonSerializer.DeserializeAsync<KeyboardMapping>(stream) ?? throw new InvalidDataException("Empty profile."); if (p.Keys is null || p.Keys.Any(k => k.Key is < 0 or > 127 || k.Value is null)) throw new InvalidDataException("Invalid profile."); return p; }
}
