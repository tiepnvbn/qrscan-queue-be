namespace QueueQr.Api.Dtos;

public sealed record RoomCatalogDto(
    Guid RoomId,
    string RoomSlug,
    string RoomName,
    int ServiceMinutes
);

public sealed record SiteCatalogDto(
    Guid SiteId,
    string SiteSlug,
    string SiteName,
    IReadOnlyList<RoomCatalogDto> Rooms
);
