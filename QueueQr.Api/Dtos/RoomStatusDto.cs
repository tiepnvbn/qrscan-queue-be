namespace QueueQr.Api.Dtos;

public sealed record RoomStatusDto(
    Guid RoomId,
    string RoomSlug,
    string RoomName,
    DateOnly ServiceDate,
    int ServiceMinutes,
    int? CurrentNumber,
    int? NextNumber,
    int NextToTakeNumber,
    int WaitingCount,
    DateTimeOffset Now
);
