using System.Threading.RateLimiting;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppWebhookRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter =
        PartitionedRateLimiter.Create<string, string>(partitionKey =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));

    public bool TryAcquire(string siteId, string externalContactId)
    {
        var partitionKey = $"{siteId.Trim()}:{externalContactId.Trim()}";
        using var lease = _limiter.AttemptAcquire(partitionKey);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
