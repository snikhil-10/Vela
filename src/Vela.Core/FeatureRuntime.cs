namespace Vela.Core;

public interface IFeatureService : IDisposable
{
    string Id { get; }
    void Start();
    void Stop();
}

public sealed class FeatureRuntime : IDisposable
{
    private readonly Dictionary<string, Func<IFeatureService>> factories = [];
    private readonly Dictionary<string, IFeatureService> running = [];
    public event Action<string, string>? Failed;
    public IReadOnlyCollection<string> Running => running.Keys;
    public void Register(string id, Func<IFeatureService> factory) => factories.Add(id, factory);
    public void Apply(IEnumerable<string> enabled)
    {
        var desired = enabled.Where(FeatureCatalog.Contains).ToHashSet();
        foreach (var id in running.Keys.Except(desired).ToArray())
        { var service = running[id]; running.Remove(id); StopAndDispose(service); }
        foreach (var id in desired.Except(running.Keys))
        {
            if (!factories.TryGetValue(id, out var factory)) continue; // On-demand tools have no resident service.
            IFeatureService? service = null;
            try { service = factory(); service.Start(); running.Add(id, service); }
            catch (Exception ex) { if(service is not null) StopAndDispose(service); Failed?.Invoke(id, ex.Message); }
        }
    }
    public void Dispose()
    {
        foreach (var service in running.Values) StopAndDispose(service);
        running.Clear();
    }
    private void StopAndDispose(IFeatureService service)
    {
        try { service.Stop(); } catch(Exception ex) { Failed?.Invoke(service.Id,ex.Message); }
        try { service.Dispose(); } catch(Exception ex) { Failed?.Invoke(service.Id,ex.Message); }
    }
}
