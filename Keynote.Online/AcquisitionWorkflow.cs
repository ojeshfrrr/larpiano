using Keynote.Core;
using Keynote.Midi;
namespace Keynote.Online;
public record AcquisitionInspection(LibraryEntry Entry,string Path,MidiInspection Inspection,bool Duplicate);
public record PreparedSong(MidiSong Song,LibraryEntry Entry);
public sealed class AcquisitionWorkflow(IReadOnlyDictionary<string,IMidiCatalogProvider> providers,IMidiDownloadService downloads,IMidiLibraryService library,IMidiImporter importer) {
 public async Task<AcquisitionInspection> DownloadAndInspectAsync(CatalogResult result,IProgress<double>? progress,CancellationToken token) {
  if(!providers.TryGetValue(result.Provider,out var provider))throw new NotSupportedException("This provider does not support direct MIDI download.");
  var bytes=await downloads.DownloadAsync(provider.DownloadUri(result),progress,token); token.ThrowIfCancellationRequested();
  var saved=await library.AddAsync(bytes,result,token); token.ThrowIfCancellationRequested();
  var path=await library.OpenAsync(saved.Entry,token); var inspection=await Task.Run(()=>new MidiInspector(importer).Inspect(path),token); token.ThrowIfCancellationRequested();
  return new(saved.Entry,path,inspection,saved.Duplicate);
 }
 public async Task<AcquisitionInspection> InspectAsync(LibraryEntry entry,CancellationToken token) {
  var path=await library.OpenAsync(entry,token); var inspection=await Task.Run(()=>new MidiInspector(importer).Inspect(path),token); token.ThrowIfCancellationRequested(); return new(entry,path,inspection,true);
 }
 public async Task<PreparedSong> PrepareAsync(LibraryEntry entry,IReadOnlyCollection<int> selectedTracks,CancellationToken token) {
  if(selectedTracks.Count==0)throw new InvalidDataException("Select at least one track."); var path=await library.OpenAsync(entry,token);
  var loaded=await Task.Run(()=>importer.Import(path),token); token.ThrowIfCancellationRequested();
  var wanted=selectedTracks.ToHashSet(); if(wanted.Any(index=>!loaded.Tracks.Any(t=>t.Index==index)))throw new InvalidDataException("The selected track no longer exists.");
  var selected=loaded with { Name=entry.Title, Tracks=loaded.Tracks.Where(t=>wanted.Contains(t.Index)).ToArray() }; if(!selected.Notes.Any())throw new InvalidDataException("Select at least one track containing notes."); token.ThrowIfCancellationRequested(); return new(selected,entry);
 }
}
