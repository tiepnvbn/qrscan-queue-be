namespace QueueQr.Api.Dtos;

public record StaffTicketListResponse(
    IReadOnlyList<StaffTicketDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
