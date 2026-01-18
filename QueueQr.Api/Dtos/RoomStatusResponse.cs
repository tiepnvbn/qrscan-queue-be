namespace QueueQr.Api.Dtos;

public sealed record RoomStatusResponse(
    string SiteSlug,
    string RoomSlug,
    RoomStatusDto Status,
    MyTicketDto? MyTicket
);
