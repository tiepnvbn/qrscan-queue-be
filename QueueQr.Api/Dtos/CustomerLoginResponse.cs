namespace QueueQr.Api.Dtos;

public sealed record CustomerLoginResponse(
    Guid CustomerId,
    string? Name,
    int Points,
    int FreeCredits,
    string Tier
);
