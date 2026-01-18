namespace QueueQr.Api.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset NowLocal { get; }
    DateOnly TodayLocal { get; }
}
