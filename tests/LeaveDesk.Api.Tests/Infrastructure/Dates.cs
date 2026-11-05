namespace LeaveDesk.Api.Tests.Infrastructure;

/// <summary>
/// Genera rangos de fechas futuros y predecibles (siempre lunes-a-X) para que el calculo
/// de dias habiles sea facil de verificar y los rangos de distintas pruebas no se solapen.
/// </summary>
internal static class Dates
{
    public static DateTime NextMonday(int weeksFromNow = 1)
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        daysUntilMonday = daysUntilMonday == 0 ? 7 : daysUntilMonday;
        return today.AddDays(daysUntilMonday + (weeksFromNow - 1) * 7);
    }
}
