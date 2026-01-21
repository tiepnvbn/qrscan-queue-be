namespace QueueQr.Api.Services;

/// <summary>
/// Helper to calculate the current shift (A, B, C...) based on configured reset times.
/// </summary>
public static class ShiftCalculator
{
    private static readonly string[] ShiftPrefixes = ["A", "B", "C", "D", "E", "F"];

    /// <summary>
    /// Parse shift reset times from configuration string (e.g., "08:00,13:00,18:00").
    /// Returns list of TimeOnly representing shift boundaries.
    /// </summary>
    public static List<TimeOnly> ParseShiftResetTimes(string? resetTimesConfig)
    {
        if (string.IsNullOrWhiteSpace(resetTimesConfig))
        {
            // Default: 2 shifts per day at 00:00 and 13:00
            return [new TimeOnly(0, 0), new TimeOnly(13, 0)];
        }

        var times = new List<TimeOnly>();
        var parts = resetTimesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (TimeOnly.TryParse(part, out var time))
            {
                times.Add(time);
            }
        }

        if (times.Count == 0)
        {
            // Fallback to default
            return [new TimeOnly(0, 0), new TimeOnly(13, 0)];
        }

        // Sort to ensure chronological order
        times.Sort();
        return times;
    }

    /// <summary>
    /// Get the current shift prefix (A, B, C...) based on current time and reset times.
    /// </summary>
    public static string GetCurrentShiftPrefix(TimeOnly currentTime, List<TimeOnly> resetTimes)
    {
        if (resetTimes.Count == 0)
        {
            return "A";
        }

        // Find which shift we're in
        int shiftIndex = 0;
        for (int i = resetTimes.Count - 1; i >= 0; i--)
        {
            if (currentTime >= resetTimes[i])
            {
                shiftIndex = i;
                break;
            }
        }

        // Return corresponding prefix (A, B, C...)
        if (shiftIndex < ShiftPrefixes.Length)
        {
            return ShiftPrefixes[shiftIndex];
        }

        // Fallback for more than 6 shifts (unlikely)
        return $"S{shiftIndex + 1}";
    }

    /// <summary>
    /// Format a ticket number with its shift prefix (e.g., "A-001").
    /// </summary>
    public static string FormatTicketNumber(string shiftPrefix, int number)
    {
        return $"{shiftPrefix}-{number:D3}";
    }
}
