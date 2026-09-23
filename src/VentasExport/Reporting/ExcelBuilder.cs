using ClosedXML.Excel;
using VentasExport.Data;

namespace VentasExport.Reporting;

/// <summary>Arma un libro con una hoja de resumen y una hoja por cada resultado.</summary>
public static class ExcelBuilder
{
    private static readonly char[] InvalidSheetChars = [':', '\\', '/', '?', '*', '[', ']'];

    private const string DateFormat = "dd/mm/yyyy";
    private const string DateTimeFormat = "dd/mm/yyyy hh:mm";
    private const string MoneyFormat = "\"$\"#,##0.00";

    public static void Build(string path, IReadOnlyList<QueryResult> results, DateTime generatedAt, string week)
    {
        using var wb = new XLWorkbook();

        var summary = wb.AddWorksheet("Resumen");
        summary.Cell(1, 1).Value = "Semana";
        summary.Cell(1, 2).Value = week;
        summary.Cell(2, 1).Value = "Generado";
        summary.Cell(2, 2).Value = generatedAt;
        summary.Cell(2, 2).Style.DateFormat.Format = DateTimeFormat;
        summary.Cell(4, 1).Value = "Hoja";
        summary.Cell(4, 2).Value = "Registros";
        summary.Range(4, 1, 4, 2).Style.Font.Bold = true;

        var row = 5;
        foreach (var result in results)
        {
            var ws = wb.AddWorksheet(SheetName(result.Name));
            WriteSheet(ws, result);

            summary.Cell(row, 1).Value = ws.Name;
            summary.Cell(row, 1).SetHyperlink(new XLHyperlink($"'{ws.Name}'!A1"));
            summary.Cell(row, 2).Value = result.Rows.Count;
            row++;
        }
        summary.Columns().AdjustToContents();

        wb.SaveAs(path);
    }

    private static void WriteSheet(IXLWorksheet ws, QueryResult result)
    {
        for (var c = 0; c < result.Columns.Length; c++)
            ws.Cell(1, c + 1).Value = result.Columns[c];

        var header = ws.Range(1, 1, 1, Math.Max(1, result.Columns.Length));
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");

        for (var r = 0; r < result.Rows.Count; r++)
        {
            var values = result.Rows[r];
            for (var c = 0; c < values.Length; c++)
                SetValue(ws.Cell(r + 2, c + 1), values[c]);
        }

        if (result.Columns.Length > 0)
        {
            ws.Range(1, 1, result.Rows.Count + 1, result.Columns.Length).SetAutoFilter();
            ws.SheetView.FreezeRows(1);
            // Ajustar con una muestra: recorrer hojas de decenas de miles de filas es muy lento.
            ws.Columns().AdjustToContents(1, Math.Min(result.Rows.Count + 1, 500));
        }
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? DateFormat : DateTimeFormat;
                break;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = DateFormat;
                break;
            case DateTimeOffset dto:
                cell.Value = dto.DateTime;
                cell.Style.DateFormat.Format = DateTimeFormat;
                break;
            case decimal or double or float:
                cell.Value = Convert.ToDouble(value);
                cell.Style.NumberFormat.Format = MoneyFormat;
                break;
            case byte or short or int or long:
                cell.Value = Convert.ToInt64(value);
                break;
            case bool b:
                cell.Value = b ? 1 : 0;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static string SheetName(string name)
    {
        var clean = new string(name.Select(ch => InvalidSheetChars.Contains(ch) ? '_' : ch).ToArray());
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
