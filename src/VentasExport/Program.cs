using System.Globalization;
using System.Runtime.InteropServices;
using VentasExport;

// Uso:
//   VentasExport            -> genera el Excel, lo sube y termina (lo invoca el crontab del servidor)
//   VentasExport auth       -> obtiene el GOOGLE_REFRESH_TOKEN (una sola vez)
//   VentasExport --no-upload -> solo genera el Excel local

DotNetEnv.Env.TraversePath().NoClobber().Load(); // .env solo en desarrollo; en Docker se usa --env-file

using var cts = new CancellationTokenSource();
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; cts.Cancel(); });
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var command = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant() ?? "run";
var upload = !args.Contains("--no-upload");

try
{
    var settings = Settings.Load();
    switch (command)
    {
        case "auth":
            await new DriveUploader(settings).AuthorizeInteractiveAsync(cts.Token);
            return 0;
        case "run":
            await RunJobAsync(settings, upload, cts.Token);
            return 0;
        default:
            Console.Error.WriteLine($"Comando desconocido: {command}. Usa: run | auth");
            return 2;
    }
}
catch (OperationCanceledException)
{
    Log("Cancelado.");
    return 0;
}
catch (Exception ex)
{
    Log($"ERROR: {ex}");
    return 1;
}

static async Task RunJobAsync(Settings settings, bool upload, CancellationToken ct)
{
    var now = DateTime.Now;
    // Mismo criterio que los .sql: la semana reportada es la que terminó el domingo anterior.
    var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
    var lastSunday = monday.AddDays(-1);
    var fiscalYear = lastSunday.Year; // igual que @AnioFiscal = YEAR(@FechaFin)
    var weekNumber = ISOWeek.GetWeekOfYear(lastSunday); // igual que DATEPART(ISO_WEEK, @FechaFin)
    var week = $"{fiscalYear}-W{weekNumber:00}";

    var files = Directory.GetFiles(settings.SqlDirectory, "*.sql")
        .Where(f => !settings.SqlExclude.Contains(Path.GetFileName(f)))
        .OrderBy(SqlRunner.FileOrder)
        .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
        .ToList();
    if (files.Count == 0)
        throw new InvalidOperationException($"No hay archivos .sql en {settings.SqlDirectory}");

    Log($"Semana {week}: ejecutando {files.Count} consultas de {settings.SqlDirectory}");
    var runner = new SqlRunner(settings);
    var results = new List<QueryResult>();
    foreach (var file in files)
    {
        var started = DateTime.UtcNow;
        var fileResults = await runner.RunFileAsync(file, ct);
        results.AddRange(fileResults);
        Log($"  {Path.GetFileName(file)} -> '{string.Join("', '", fileResults.Select(r => r.Name))}': {fileResults.Sum(r => r.Rows.Count)} filas ({(DateTime.UtcNow - started).TotalSeconds:0.0}s)");
    }

    Directory.CreateDirectory(settings.OutputDirectory);
    var path = Path.Combine(settings.OutputDirectory, $"{week}.xlsx");
    ExcelBuilder.Build(path, results, now, week);
    Log($"Excel generado: {path}");

    if (!upload) return;
    var id = await new DriveUploader(settings).UploadAsync(path, ct);
    Log($"Subido a Drive: https://drive.google.com/file/d/{id}/view");
}

static void Log(string message) => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
