namespace QueueQr.Api.Dtos;

public sealed record TakeTicketRequest(
    Guid? CustomerId
);
