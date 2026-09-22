using VentasExport.Common;
using VentasExport.Configuration;
using VentasExport.Data;
using VentasExport.Reporting;

namespace VentasExport.Jobs;

/// <summary>Job semanal: ejecuta los .sql, arma el Excel de la semana y lo sube.</summary>
public static class ExportJob
{
    public static async Task RunAsync(Settings settings, bool upload, CancellationToken ct)
    {
        var now = DateTime.Now;
        var week = ReportWeek.For(now);

        var files = Directory.GetFiles(settings.SqlDirectory, "*.sql")
            .Where(f => !settings.SqlExclude.Contains(Path.GetFileName(f)))
            .OrderBy(SqlRunner.FileOrder)
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
            throw new InvalidOperationException($"No hay archivos .sql en {settings.SqlDirectory}");

        Logger.Log($"Semana {week}: ejecutando {files.Count} consultas de {settings.SqlDirectory}");
        var runner = new SqlRunner(settings);
        var results = new List<QueryResult>();
        foreach (var file in files)
        {
            var started = DateTime.UtcNow;
            var fileResults = await runner.RunFileAsync(file, ct);
            results.AddRange(fileResults);
            Logger.Log($"  {Path.GetFileName(file)} -> '{string.Join("', '", fileResults.Select(r => r.Name))}': {fileResults.Sum(r => r.Rows.Count)} filas ({(DateTime.UtcNow - started).TotalSeconds:0.0}s)");
        }

        await ReportPublisher.PublishAsync(settings, results, $"{week}.xlsx", week, now, upload, ct);
    }
}
