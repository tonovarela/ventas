using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using VentasExport.Data;

namespace VentasExport.Reporting;

/// <summary>Arma un libro con una hoja por cada resultado.</summary>
public static partial class ExcelBuilder
{
    private static readonly char[] InvalidSheetChars = [':', '\\', '/', '?', '*', '[', ']'];

    private const string DateFormat = "mm/dd/yyyy";
    private const string DateTimeFormat = "mm/dd/yyyy hh:mm";
    // Coma para miles y punto decimal (1,234,567.89). En Google Sheets los separadores los decide la
    // región de la hoja, que DriveUploader fija en es_MX.
    private const string AmountFormat = "#,##0.00";

    /// <summary>Columnas de importes (por nombre), sin importar el tipo con el que lleguen de SQL.</summary>
    [GeneratedRegex(@"^(importe|imp[A-Z_]|saldo|venta|meta|facturado|comision|corriente|d\d+_)", RegexOptions.IgnoreCase)]
    private static partial Regex MoneyColumn();

    public static void Build(string path, IReadOnlyList<QueryResult> results)
    {
        using var wb = new XLWorkbook();

        foreach (var result in results)
            WriteSheet(wb.AddWorksheet(SheetName(result.Name)), result);

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

        var isMoney = result.Columns.Select(name => MoneyColumn().IsMatch(name)).ToArray();

        for (var r = 0; r < result.Rows.Count; r++)
        {
            var values = result.Rows[r];
            for (var c = 0; c < values.Length; c++)
            {
                if (isMoney[c] && TryGetAmount(values[c], out var amount))
                {
                    var cell = ws.Cell(r + 2, c + 1);
                    cell.Value = amount;
                    cell.Style.NumberFormat.Format = AmountFormat;
                }
                else
                {
                    SetValue(ws.Cell(r + 2, c + 1), values[c]);
                }
            }
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
                cell.Style.NumberFormat.Format = AmountFormat;
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

    private static bool TryGetAmount(object? value, out double amount)
    {
        switch (value)
        {
            case decimal or double or float or byte or short or int or long:
                amount = Convert.ToDouble(value);
                return true;
            case string s:
                return double.TryParse(s.Replace("$", "").Replace(",", "").Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
            default:
                amount = 0;
                return false;
        }
    }

    private static string SheetName(string name)
    {
        var clean = new string(name.Select(ch => InvalidSheetChars.Contains(ch) ? '_' : ch).ToArray());
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
