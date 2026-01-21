namespace QueueQr.Api.Dtos;

public sealed record RoomStatusDto(
    Guid RoomId,
    string RoomSlug,
    string RoomName,
    DateOnly ServiceDate,
    int ServiceMinutes,
    int? CurrentNumber,
    string? CurrentDisplayNumber,
    int? NextNumber,
    string? NextDisplayNumber,
    int NextToTakeNumber,
    string NextToTakeDisplayNumber,
    int WaitingCount,
    DateTimeOffset Now
);
