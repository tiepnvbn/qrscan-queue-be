namespace QueueQr.Api.Dtos;

public record StaffLoginResponse(
    Guid StaffId,
    string? Name,
    string Token,
    string SiteSlug,
    string SiteName
);
