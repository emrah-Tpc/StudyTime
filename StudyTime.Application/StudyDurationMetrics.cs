namespace StudyTime.Application;

/// <summary>
/// Çalışma süresi: &lt;1 dk sayılmaz; KPI metni saniye hassasiyetinde; grafikler için tam dakikaya yuvarlama.
/// </summary>
public static class StudyDurationMetrics
{
    /// <summary>1 dakikanın altındaki oturumlar 0; aksi halde en yakın tam dakika (1dk 35sn → 2).</summary>
    public static int ToChartMinutes(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60) return 0;
        return (int)Math.Round(duration.TotalMinutes);
    }

    public static int SumChartMinutes(IEnumerable<TimeSpan> durations) =>
        durations.Sum(ToChartMinutes);

    /// <summary>KPI ve metin alanları: "35sn", "1dk 35sn", "2,5s".</summary>
    public static string FormatDisplay(TimeSpan total)
    {
        if (total.TotalSeconds < 1) return "0dk";

        if (total.TotalHours >= 1)
            return $"{Math.Round(total.TotalHours, 1)}s";

        if (total.TotalSeconds < 60)
            return $"{(int)Math.Round(total.TotalSeconds)}sn";

        var minutes = (int)total.TotalMinutes;
        var seconds = total.Seconds;
        return seconds == 0 ? $"{minutes}dk" : $"{minutes}dk {seconds}sn";
    }

    public static int ToChartMinutesFromTotalSeconds(double totalSeconds)
    {
        if (totalSeconds < 60) return 0;
        return (int)Math.Round(totalSeconds / 60.0);
    }
}
