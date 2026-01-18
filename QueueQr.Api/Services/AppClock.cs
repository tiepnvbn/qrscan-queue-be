namespace QueueQr.Api.Services;

public sealed class AppClock : IClock
{
    private readonly TimeZoneInfo _timeZone;

    public AppClock(IConfiguration configuration)
    {
        var timeZoneId = configuration["TimeZoneId"] ?? "SE Asia Standard Time";
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset NowLocal => TimeZoneInfo.ConvertTime(UtcNow, _timeZone);

    public DateOnly TodayLocal => DateOnly.FromDateTime(NowLocal.DateTime);
}
