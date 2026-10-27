namespace ApiAlertaDengue.Helpers;

/// <summary>
/// Semana epidemiológica brasileira: começa no domingo e a semana 1 do ano é a primeira
/// que contém pelo menos 4 dias do ano novo (ou seja, a que contém 4 de janeiro).
/// </summary>
public static class EpidemiologicalWeek
{
    public readonly record struct Week(int Year, int Number)
    {
        /// <summary>Formato AAAASS usado pelo InfoDengue (ex.: 202510).</summary>
        public int Key => Year * 100 + Number;
    }

    public static Week From(DateOnly date)
    {
        var year = date.Year;
        if (date < FirstWeekStart(year)) year--;
        else if (date >= FirstWeekStart(year + 1)) year++;

        var number = (date.DayNumber - FirstWeekStart(year).DayNumber) / 7 + 1;
        return new Week(year, number);
    }

    public static DateOnly StartOf(Week week) =>
        FirstWeekStart(week.Year).AddDays((week.Number - 1) * 7);

    public static Week Subtract(Week week, int weeks) =>
        From(StartOf(week).AddDays(-7 * weeks));

    /// <summary>Converte o formato AAAASS em semana. Retorna false para valores inválidos.</summary>
    public static bool TryParseKey(int key, out Week week)
    {
        week = new Week(key / 100, key % 100);
        return week.Year is >= 1900 and <= 2200 && week.Number is >= 1 and <= 53;
    }

    private static DateOnly FirstWeekStart(int year)
    {
        var jan4 = new DateOnly(year, 1, 4);
        return jan4.AddDays(-(int)jan4.DayOfWeek);
    }
}
