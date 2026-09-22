using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using VentasExport.Configuration;

namespace VentasExport.Data;

/// <summary>Ejecuta scripts .sql tal como los correría SSMS: separa los lotes por "GO".</summary>
public sealed partial class SqlRunner(Settings settings)
{
    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    [GeneratedRegex(@"^(\d+)[-_ .]*(.*)$")]
    private static partial Regex NumberedFileName();

    /// <summary>Número al inicio del nombre del archivo ("03-pedidos.sql" -> 3); sin número va al final.</summary>
    public static int FileOrder(string path)
    {
        var match = NumberedFileName().Match(Path.GetFileNameWithoutExtension(path));
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }

    /// <summary>
    /// Nombre de la hoja: el primer comentario "--" del archivo (antes de cualquier código).
    /// Si no hay, el nombre del archivo sin el número.
    /// </summary>
    private static string SheetTitle(string path, string script)
    {
        foreach (var line in script.Split('\n').Select(l => l.Trim()))
        {
            if (line.Length == 0) continue;
            if (!line.StartsWith("--")) break;
            var title = line.TrimStart('-').Trim();
            if (title.Length > 0) return title;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        var match = NumberedFileName().Match(name);
        return match.Success && match.Groups[2].Length > 0 ? match.Groups[2].Value : name;
    }

    public async Task<List<QueryResult>> RunFileAsync(string path, CancellationToken ct)
    {
        var script = await File.ReadAllTextAsync(path, ct);
        var name = SheetTitle(path, script);
        var batches = GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b));

        await using var conn = new SqlConnection(settings.SqlConnectionString);
        await conn.OpenAsync(ct);

        var results = new List<QueryResult>();
        foreach (var batch in batches)
        {
            await using var cmd = new SqlCommand("SET NOCOUNT ON;\n" + batch, conn)
            {
                CommandTimeout = settings.SqlCommandTimeoutSeconds,
            };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            do
            {
                if (reader.FieldCount == 0) continue;

                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                var rows = new List<object?[]>();
                while (await reader.ReadAsync(ct))
                {
                    var values = new object?[reader.FieldCount];
                    reader.GetValues(values!);
                    for (var i = 0; i < values.Length; i++)
                        if (values[i] is DBNull) values[i] = null;
                    rows.Add(values);
                }

                var resultName = results.Count == 0 ? name : $"{name}_{results.Count + 1}";
                results.Add(new QueryResult(resultName, columns, rows));
            } while (await reader.NextResultAsync(ct));
        }

        return results;
    }
}
