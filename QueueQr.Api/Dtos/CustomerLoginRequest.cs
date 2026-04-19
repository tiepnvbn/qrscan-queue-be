namespace QueueQr.Api.Dtos;

public sealed record CustomerLoginRequest(
    string Phone,
    string? Name = null,
    DateOnly? DateOfBirth = null
);
