using System.Collections.Concurrent;
using System.Text.Json;
namespace Keynote.Online;
public record ProviderSearchStatus(string Provider,bool Succeeded,string? Error,DateTimeOffset? LastSuccess,bool HasMore);
public record DiscoveryBatch(IReadOnlyList<CatalogResult> Results,IReadOnlyList<ProviderSearchStatus> Providers);
public record CatalogSource(string Provider,string Id,string SourceUrl,string? License=null);
public sealed class DiscoverySearchService {
 readonly IReadOnlyDictionary<string,IMidiCatalogProvider> providers;
 readonly ConcurrentDictionary<string,(DateTimeOffset At,DiscoveryBatch Batch)> cache=new();
 readonly ConcurrentDictionary<string,DateTimeOffset> lastSuccess=new();
 readonly TimeSpan cacheLifetime;
 public DiscoverySearchService(IReadOnlyDictionary<string,IMidiCatalogProvider> providers,TimeSpan? cacheLifetime=null) { this.providers=providers; this.cacheLifetime=cacheLifetime ?? TimeSpan.FromMinutes(3); }
 public async Task<DiscoveryBatch> SearchAsync(string query,int page,IReadOnlyCollection<string>? selected,CancellationToken token) {
  if(page<0)throw new ArgumentOutOfRangeException(nameof(page)); query=query.Trim(); if(query.Length is 0 or >200)throw new ArgumentException("Enter a search of up to 200 characters."); var chosen=providers.Where(p=>selected==null || selected.Contains(p.Key,StringComparer.Ordinal)).OrderBy(p=>p.Key).ToArray(); if(chosen.Length==0)throw new ArgumentException("Select at least one provider.");
  var key=$"{page}|{string.Join("|",chosen.Select(x=>x.Key))}|{query.ToUpperInvariant()}"; if(cache.TryGetValue(key,out var cached) && DateTimeOffset.UtcNow-cached.At<cacheLifetime) { token.ThrowIfCancellationRequested(); return cached.Batch; }
  async Task<(IReadOnlyList<CatalogResult>,ProviderSearchStatus)> Query(KeyValuePair<string,IMidiCatalogProvider> p) { try { var response=await p.Value.SearchAsync(query,page,token); var now=DateTimeOffset.UtcNow; lastSuccess[p.Key]=now; return(response.Results,new(p.Key,true,null,now,response.HasMore)); } catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; } catch(Exception ex) { lastSuccess.TryGetValue(p.Key,out var last); return(Array.Empty<CatalogResult>(),new(p.Key,false,ex.Message,last==default?null:last,false)); } }
  var batches=await Task.WhenAll(chosen.Select(Query)); token.ThrowIfCancellationRequested(); var merged=Merge(batches.SelectMany(x=>x.Item1)); var result=new DiscoveryBatch(merged,batches.Select(x=>x.Item2).ToArray());
  if(result.Providers.All(p=>p.Succeeded)) { if(cache.Count>=30) { var oldest=cache.OrderBy(x=>x.Value.At).FirstOrDefault(); if(oldest.Key!=null)cache.TryRemove(oldest.Key,out _); } cache[key]=(DateTimeOffset.UtcNow,result); } return result;
 }
 public static IReadOnlyList<CatalogResult> Merge(IEnumerable<CatalogResult> input) {
  var results=new List<CatalogResult>(); var indexes=new Dictionary<string,int>(StringComparer.Ordinal);
  int anonymous=0; foreach(var item in input) {
   // A hash proves byte identity; otherwise merge only the same provider's identical stable ID.
   var hash=item.Sha256; string identity=hash is {Length:64} && hash.All(Uri.IsHexDigit) ? "sha256:"+hash.ToLowerInvariant() : Uri.TryCreate(item.DirectUrl,UriKind.Absolute,out var direct) && direct.Scheme==Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(item.Artist) && !item.Artist.Equals("Unknown",StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Description) ? "direct:"+JsonSerializer.Serialize(new[]{direct.AbsoluteUri,item.Title.Trim().ToUpperInvariant(),item.Artist.Trim().ToUpperInvariant(),item.Description.Trim().ToUpperInvariant()}) : !string.IsNullOrWhiteSpace(item.Id) ? "provider:"+JsonSerializer.Serialize(new[]{item.Provider.ToUpperInvariant(),item.Id}) : "anonymous:"+(anonymous++);
   var sources=item.Sources is {Count:>0}?item.Sources:new[]{new CatalogSource(item.Provider,item.Id,item.SourceUrl,item.License)};
   if(indexes.TryGetValue(identity,out var index)) { var current=results[index]; var joined=(current.Sources ?? new[]{new CatalogSource(current.Provider,current.Id,current.SourceUrl,current.License)}).Concat(sources).Distinct().ToArray(); results[index]=current with {Sources=joined}; }
   else { indexes[identity]=results.Count; results.Add(item with {Sources=sources}); }
  } return results;
 }
}
public sealed class SearchHistoryStore(string path) {
 readonly string file=Path.GetFullPath(path); const int Limit=30;
 public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken token) { try { if(!File.Exists(file))return Array.Empty<string>(); var json=await File.ReadAllTextAsync(file,token); return (JsonSerializer.Deserialize<string[]>(json)??Array.Empty<string>()).Where(s=>!string.IsNullOrWhiteSpace(s)&&s.Length<=200).Distinct(StringComparer.OrdinalIgnoreCase).Take(Limit).ToArray(); } catch(JsonException) { return Array.Empty<string>(); } }
 public static IReadOnlyList<string> Add(IReadOnlyList<string> history,string query) { query=query.Trim(); return new[]{query}.Concat(history).Where(s=>!string.IsNullOrWhiteSpace(s)&&s.Length<=200).Distinct(StringComparer.OrdinalIgnoreCase).Take(Limit).ToArray(); }
 public static IReadOnlyList<string> Suggest(IReadOnlyList<string> history,string prefix)=>history.Where(s=>s.Contains(prefix.Trim(),StringComparison.OrdinalIgnoreCase)).Take(8).ToArray();
 public async Task SaveAsync(IReadOnlyList<string> history,CancellationToken token) { Directory.CreateDirectory(Path.GetDirectoryName(file)!); var tmp=file+"."+Guid.NewGuid().ToString("N")+".tmp"; try { await File.WriteAllTextAsync(tmp,JsonSerializer.Serialize(history),token); File.Move(tmp,file,true); } finally { if(File.Exists(tmp))File.Delete(tmp); } }
}
public sealed class SearchDebouncer : IDisposable {
 CancellationTokenSource? pending; public async Task ScheduleAsync(Func<CancellationToken,Task> action,TimeSpan delay) { pending?.Cancel(); var cts=new CancellationTokenSource(); pending=cts; try { await Task.Delay(delay,cts.Token); await action(cts.Token); } catch(OperationCanceledException) when(cts.IsCancellationRequested) { } finally { if(ReferenceEquals(pending,cts))pending=null; cts.Dispose(); } } public void Cancel()=>pending?.Cancel(); public void Dispose()=>Cancel();
}
