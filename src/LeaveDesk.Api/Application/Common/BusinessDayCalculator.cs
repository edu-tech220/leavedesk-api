namespace LeaveDesk.Api.Application.Common;

/// <summary>Cuenta dias habiles (lunes a viernes) en un rango inclusivo. No considera dias feriados.</summary>
public static class BusinessDayCalculator
{
    public static decimal CountBusinessDays(DateTime start, DateTime end)
    {
        if (start.Date > end.Date)
        {
            return 0m;
        }

        var count = 0;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
        }

        return count;
    }
}
