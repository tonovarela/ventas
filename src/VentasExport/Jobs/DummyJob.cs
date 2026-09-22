using VentasExport.Configuration;
using VentasExport.Data;
using VentasExport.Reporting;

namespace VentasExport.Jobs;

/// <summary>Prueba la subida a Drive sin tocar SQL Server: mismo ExcelBuilder y DriveUploader, datos falsos.</summary>
public static class DummyJob
{
    public static Task RunAsync(Settings settings, bool upload, CancellationToken ct)
    {
        var now = DateTime.Now;
        var results = new List<QueryResult>
        {
            new("Dummy", ["Id", "Cliente", "Monto", "Fecha"],
            [
                [1, "Cliente A", 1500.50m, now.Date],
                [2, "Cliente B", 320.00m, now.Date.AddDays(-1)],
                [3, "Cliente C", 98765.43m, now],
            ]),
        };

        // Nombre con timestamp para no pisar nunca un archivo real de la semana.
        return ReportPublisher.PublishAsync(settings, results, $"DUMMY-{now:yyyyMMdd-HHmmss}.xlsx", "DUMMY", now, upload, ct);
    }
}
