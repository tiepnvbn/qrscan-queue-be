namespace QueueQr.Api.Entities;

public sealed class Site
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Room> Rooms { get; set; } = new();
}
