namespace ApiAlertaDengue.Services.Sync;

public static class WeeklySchedule
{
    /// <summary>Próxima ocorrência (UTC) do dia da semana/horário configurados, estritamente depois de <paramref name="nowUtc"/>.</summary>
    public static DateTimeOffset NextOccurrence(DateTimeOffset nowUtc, SyncOptions options, TimeZoneInfo timeZone)
    {
        var time = TimeSpan.Parse(options.TimeOfDay, System.Globalization.CultureInfo.InvariantCulture);
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone).DateTime;

        var daysAhead = ((int)options.DayOfWeek - (int)localNow.DayOfWeek + 7) % 7;
        var candidate = localNow.Date.AddDays(daysAhead) + time;
        if (candidate <= localNow) candidate = candidate.AddDays(7);

        // Horário inexistente na troca de horário de verão: avança uma hora.
        if (timeZone.IsInvalidTime(candidate)) candidate = candidate.AddHours(1);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(candidate, timeZone), TimeSpan.Zero);
    }

    public static TimeZoneInfo ResolveTimeZone(string id, ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning("Fuso horário '{TimeZone}' não encontrado; usando UTC.", id);
            return TimeZoneInfo.Utc;
        }
    }
}
