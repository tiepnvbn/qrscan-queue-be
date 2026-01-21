namespace QueueQr.Api.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }

    public required string Name { get; set; }
    public required string Slug { get; set; }

    public int ServiceMinutes { get; set; } = 10;    
    /// <summary>
    /// Shift reset times in HH:mm format, comma-separated (e.g., "08:00,13:00,18:00").
    /// If null or empty, defaults to "00:00,13:00" (2 shifts per day).
    /// </summary>
    public string? ShiftResetTimes { get; set; }
    public Site? Site { get; set; }
    public List<Ticket> Tickets { get; set; } = new();
}
