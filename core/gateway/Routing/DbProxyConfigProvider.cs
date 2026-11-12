using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Routing;

/// <summary>Holds the current YARP configuration and tells YARP when it is replaced.</summary>
public sealed class DbProxyConfigProvider : IProxyConfigProvider
{
    private volatile MemoryConfig _config = new([], []);

    public IProxyConfig GetConfig() => _config;

    /// <summary>True once the routes were read from the database at least once.</summary>
    public bool IsLoaded { get; private set; }

    public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        var previous = _config;
        IsLoaded = true;
        _config = new MemoryConfig(routes, clusters);
        previous.SignalChange();
    }

    private sealed class MemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _changed = new();

        public MemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_changed.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }

        public void SignalChange() => _changed.Cancel();
    }
}
