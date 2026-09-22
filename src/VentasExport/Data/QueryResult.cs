namespace VentasExport.Data;

/// <summary>Un conjunto de resultados: se convierte en una hoja del Excel.</summary>
public sealed record QueryResult(string Name, string[] Columns, List<object?[]> Rows);
