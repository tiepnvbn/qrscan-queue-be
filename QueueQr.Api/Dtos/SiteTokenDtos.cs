namespace QueueQr.Api.Dtos;

public sealed record VerifySiteTokenRequest(string Token);

public sealed record VerifySiteTokenResponse(
    bool Success,
    string? SessionToken,
    string? SiteSlug,
    string? SiteName,
    string? ErrorMessage
);

public sealed record SiteQrTokenDto(
    string Token,
    string QrUrl,
    DateTimeOffset ExpiresAt
);

public sealed record TakeMultiRoomTicketRequest(
    string[] RoomSlugs,
    Guid? CustomerId,
    string? SessionToken
);

public sealed record MultiRoomTicketResult(
    Guid TicketId,
    string RoomSlug,
    string RoomName,
    int Number,
    string DisplayNumber
);

public sealed record TakeMultiRoomTicketResponse(
    MultiRoomTicketResult[] Tickets
);
