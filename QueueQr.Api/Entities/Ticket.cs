namespace QueueQr.Api.Entities;

public sealed class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    public DateOnly ServiceDate { get; set; }
    public int Number { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Waiting;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CalledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? SkippedAt { get; set; }

    public Feedback? Feedback { get; set; }
}
