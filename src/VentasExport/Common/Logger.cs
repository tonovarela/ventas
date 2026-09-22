namespace VentasExport.Common;

public static class Logger
{
    public static void Log(string message) => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
}
