namespace QueueQr.Api.Dtos;

public sealed record CustomerLoginRequest(
    string Phone,
    DateOnly DateOfBirth
);
