using System.Globalization;

namespace VentasExport.Reporting;

/// <summary>
/// Semana reportada, con el mismo criterio que los .sql: la que terminó el domingo anterior.
/// </summary>
public static class ReportWeek
{
    public static string For(DateTime now)
    {
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var lastSunday = monday.AddDays(-1);
        var fiscalYear = lastSunday.Year; // igual que @AnioFiscal = YEAR(@FechaFin)
        var weekNumber = ISOWeek.GetWeekOfYear(lastSunday); // igual que DATEPART(ISO_WEEK, @FechaFin)
        return $"{fiscalYear}-W{weekNumber:00}";
    }
}
