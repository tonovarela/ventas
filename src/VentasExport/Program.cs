using System.Runtime.InteropServices;
using VentasExport.Common;
using VentasExport.Configuration;
using VentasExport.Drive;
using VentasExport.Jobs;

namespace VentasExport;

// Uso:
//   VentasExport             -> genera el Excel, lo sube y termina (lo invoca el crontab del servidor)
//   VentasExport auth        -> obtiene el GOOGLE_REFRESH_TOKEN (una sola vez)
//   VentasExport --no-upload -> solo genera el Excel local
//   VentasExport --dummy     -> sin SQL: genera un Excel de prueba y lo sube (para probar Drive)
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        DotNetEnv.Env.TraversePath().NoClobber().Load(); // .env solo en desarrollo; en Docker se usa --env-file

        using var cts = new CancellationTokenSource();
        using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; cts.Cancel(); });
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var command = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant() ?? "run";
        var upload = !args.Contains("--no-upload");
        var dummy = args.Contains("--dummy");

        try
        {
            var settings = Settings.Load();
            switch (command)
            {
                case "auth":
                    await new DriveUploader(settings).AuthorizeInteractiveAsync(cts.Token);
                    return 0;
                case "run":
                    if (dummy) await DummyJob.RunAsync(settings, upload, cts.Token);
                    else await ExportJob.RunAsync(settings, upload, cts.Token);
                    return 0;
                default:
                    Console.Error.WriteLine($"Comando desconocido: {command}. Usa: run | auth");
                    return 2;
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Cancelado.");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Log($"ERROR: {ex}");
            return 1;
        }
    }
}
