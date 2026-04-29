namespace QueueQr.Api.Entities;

public sealed class QrScanLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public required string Token { get; set; }
    public Guid? CustomerId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
}
