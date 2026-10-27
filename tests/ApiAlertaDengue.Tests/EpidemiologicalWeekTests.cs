using ApiAlertaDengue.Helpers;

namespace ApiAlertaDengue.Tests;

public class EpidemiologicalWeekTests
{
    [Theory]
    [InlineData(2025, 1, 1, 2025, 1)]   // quarta; semana 1 de 2025 começa em 29/12/2024
    [InlineData(2024, 12, 29, 2025, 1)] // domingo que inicia a semana 1 de 2025
    [InlineData(2024, 12, 28, 2024, 52)]
    [InlineData(2025, 3, 2, 2025, 10)]  // domingo
    [InlineData(2025, 3, 8, 2025, 10)]  // sábado
    [InlineData(2021, 1, 1, 2020, 53)]  // 2020 teve 53 semanas epidemiológicas
    [InlineData(2021, 1, 3, 2021, 1)]
    [InlineData(2026, 1, 1, 2025, 53)]  // 2025 também fecha com 53 semanas
    public void From_ReturnsBrazilianEpidemiologicalWeek(int y, int m, int d, int expectedYear, int expectedWeek)
    {
        var week = EpidemiologicalWeek.From(new DateOnly(y, m, d));

        Assert.Equal(expectedYear, week.Year);
        Assert.Equal(expectedWeek, week.Number);
    }

    [Fact]
    public void Key_UsesYearWeekFormat()
    {
        Assert.Equal(202510, new EpidemiologicalWeek.Week(2025, 10).Key);
    }

    [Fact]
    public void StartOf_IsAlwaysSunday_AndRoundTrips()
    {
        for (var date = new DateOnly(2019, 1, 1); date < new DateOnly(2027, 1, 1); date = date.AddDays(1))
        {
            var week = EpidemiologicalWeek.From(date);
            var start = EpidemiologicalWeek.StartOf(week);

            Assert.Equal(DayOfWeek.Sunday, start.DayOfWeek);
            Assert.InRange(date.DayNumber - start.DayNumber, 0, 6);
            Assert.Equal(week, EpidemiologicalWeek.From(start));
        }
    }

    [Fact]
    public void Subtract_CrossesYearBoundary()
    {
        var result = EpidemiologicalWeek.Subtract(new EpidemiologicalWeek.Week(2025, 2), 3);

        Assert.Equal(new EpidemiologicalWeek.Week(2024, 51), result);
    }

    [Theory]
    [InlineData(202510, true)]
    [InlineData(202500, false)]
    [InlineData(202554, false)]
    [InlineData(10, false)]
    public void TryParseKey_ValidatesRange(int key, bool expected)
    {
        Assert.Equal(expected, EpidemiologicalWeek.TryParseKey(key, out _));
    }
}
