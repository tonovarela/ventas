using VentasExport.Data;

namespace VentasExport.Reporting;

/// <summary>Semana reportada, tal como la calculan los .sql (semana_iso, fecha_inicio, fecha_fin).</summary>
public sealed record ReportWeek(string Iso, DateTime? Start, DateTime? End)
{
    private const string IsoColumn = "semana_iso";
    private const string StartColumn = "fecha_inicio";
    private const string EndColumn = "fecha_fin";

    /// <summary>
    /// Toma la semana del primer resultado que traiga la columna semana_iso (01-encabezados.sql),
    /// para que el nombre del archivo coincida con los datos.
    /// </summary>
    public static ReportWeek FromResults(IEnumerable<QueryResult> results)
    {
        foreach (var result in results)
        {
            var iso = Array.FindIndex(result.Columns, c => c.Equals(IsoColumn, StringComparison.OrdinalIgnoreCase));
            if (iso < 0) continue;

            var row = result.Rows.FirstOrDefault(r => r[iso] is not null)
                ?? throw new InvalidOperationException($"'{result.Name}' no devolvió filas con {IsoColumn}");
            return new ReportWeek(
                row[iso]!.ToString()!.Trim(),
                DateAt(result, row, StartColumn),
                DateAt(result, row, EndColumn));
        }

        throw new InvalidOperationException($"Ninguna consulta devolvió la columna {IsoColumn}");
    }

    private static DateTime? DateAt(QueryResult result, object?[] row, string column)
    {
        var i = Array.FindIndex(result.Columns, c => c.Equals(column, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? null : row[i] switch
        {
            DateTime dt => dt,
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            _ => null,
        };
    }

    public override string ToString() => Iso;
}
