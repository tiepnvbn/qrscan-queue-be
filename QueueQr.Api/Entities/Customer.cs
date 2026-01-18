namespace QueueQr.Api.Entities;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }

    public int Points { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Ticket> Tickets { get; set; } = new();
}
