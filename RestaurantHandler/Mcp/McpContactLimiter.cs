using System.Threading.RateLimiting;

namespace RestaurantHandler.Mcp;

/// <summary>
/// Quota d'envois de <c>contact_teacher</c> par IP, en plus des 60 requêtes/minute de <c>/mcp</c>.
/// Consommé dans l'outil et non par une policy de route : toutes les requêtes MCP partagent la même
/// route, et la recherche ne doit pas être bloquée par des envois.
/// </summary>
public sealed class McpContactLimiter : IDisposable
{
    public const int PermitsPerHour = 5;

    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(
        ip =>
            RateLimitPartition.GetFixedWindowLimiter(
                ip,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }
            )
    );

    public bool TryAcquire(string ip)
    {
        using var lease = _limiter.AttemptAcquire(ip);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
