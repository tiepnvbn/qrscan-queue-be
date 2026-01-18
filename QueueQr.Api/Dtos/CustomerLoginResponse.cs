namespace QueueQr.Api.Dtos;

public sealed record CustomerLoginResponse(
    Guid CustomerId,
    int Points,
    int FreeCredits,
    string Tier
);
