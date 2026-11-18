namespace Praktikaportaal.Worker;

public static class Schedule
{
    /// <summary>
    /// The next occurrence of <paramref name="runAt"/> (local time) strictly after <paramref name="now"/>:
    /// today if it has not passed yet, otherwise tomorrow.
    /// </summary>
    public static DateTime NextRun(DateTime now, TimeOnly runAt)
    {
        var today = now.Date.Add(runAt.ToTimeSpan());
        return today > now ? today : today.AddDays(1);
    }

    /// <summary>How long to wait from <paramref name="now"/> until the next daily run.</summary>
    public static TimeSpan DelayUntilNextRun(DateTime now, TimeOnly runAt) => NextRun(now, runAt) - now;
}
