namespace QueueQr.Api.Dtos;

public record StaffTicketDto(
    Guid TicketId,
    int Number,
    string DisplayNumber,
    string? CustomerName,
    string RoomSlug,
    string RoomName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CalledAt,
    DateTimeOffset? CompletedAt,
    int ServiceMinutes
);
