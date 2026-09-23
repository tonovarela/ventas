using VentasExport.Common;
using VentasExport.Configuration;
using VentasExport.Data;
using VentasExport.Drive;

namespace VentasExport.Reporting;

/// <summary>Genera el Excel en OUTPUT_DIR y, si se pide, lo sube a Drive.</summary>
public static class ReportPublisher
{
    public static async Task PublishAsync(
        Settings settings, IReadOnlyList<QueryResult> results, string fileName, string week,
        DateTime generatedAt, bool upload, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.OutputDirectory);
        var path = Path.Combine(settings.OutputDirectory, fileName);
        ExcelBuilder.Build(path, results, generatedAt, week);
        Logger.Log($"Excel generado: {path}");

        if (!upload) return;
        var id = await new DriveUploader(settings).UploadAsync(path, ct);
        Logger.Log($"Subido a Drive: https://drive.google.com/file/d/{id}/view");
    }
}
