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
            new("Dummy", ["Id", "Cliente", "Monto", "Importe", "Fecha"],
            [
                [1, "Cliente A", 1500.50m, "1,234,567.89", now.Date],
                [2, "Cliente B", 320.00m, "-98765.4", now.Date.AddDays(-1)],
                [3, "Cliente C", 98765.43m, "0.5", now],
                [4, "Cliente D", -1234567.891m, "12345678901.5", now],
            ]),
        };

        // Nombre con timestamp para no pisar nunca un archivo real de la semana.
        return ReportPublisher.PublishAsync(settings, results, $"DUMMY-{now:yyyyMMdd-HHmmss}.xlsx", upload, ct);
    }
}
