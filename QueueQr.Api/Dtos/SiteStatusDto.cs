namespace QueueQr.Api.Dtos;

public sealed record SiteStatusDto(
    string SiteSlug,
    DateTimeOffset Now,
    IReadOnlyList<RoomStatusDto> Rooms
);
