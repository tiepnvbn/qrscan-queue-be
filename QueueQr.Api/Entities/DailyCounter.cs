namespace QueueQr.Api.Entities;

public sealed class DailyCounter
{
    public Guid RoomId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public string CurrentShift { get; set; } = "A";

    public int NextNumber { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Room? Room { get; set; }
}
