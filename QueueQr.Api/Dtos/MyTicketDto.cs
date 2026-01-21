namespace QueueQr.Api.Dtos;

public sealed record MyTicketDto(
    Guid TicketId,
    int Number,
    string DisplayNumber,
    string Status,
    int AheadCount,
    int EstimatedWaitMinutes,
    DateTimeOffset EstimatedServeTime
);
