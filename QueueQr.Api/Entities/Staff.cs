namespace QueueQr.Api.Entities;

public sealed class Staff
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Phone { get; set; }
    public required string PasswordHash { get; set; }
    public string? Name { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
