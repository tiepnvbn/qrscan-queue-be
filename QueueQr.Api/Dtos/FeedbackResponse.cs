namespace QueueQr.Api.Dtos;

public sealed record FeedbackResponse(
    Guid FeedbackId,
    Guid TicketId
);
