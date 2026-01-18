namespace QueueQr.Api.Dtos;

public sealed record FeedbackRequest(
    int Stars,
    string? Comment
);
