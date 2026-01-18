namespace QueueQr.Api.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }

    public required string Name { get; set; }
    public required string Slug { get; set; }

    public int ServiceMinutes { get; set; } = 10;

    public Site? Site { get; set; }
    public List<Ticket> Tickets { get; set; } = new();
}
