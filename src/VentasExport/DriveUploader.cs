using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace VentasExport;

/// <summary>
/// Sube el archivo a una carpeta de Drive usando OAuth de usuario.
/// Una Service Account no sirve aquí: no tiene cuota propia y no puede crear archivos
/// en carpetas de "Mi unidad" de otra persona.
/// </summary>
public sealed class DriveUploader(Settings settings)
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string RedirectUri = "http://localhost:53682/";
    private static readonly string[] Scopes = [DriveService.Scope.Drive];

    private GoogleAuthorizationCodeFlow CreateFlow() => new(new GoogleAuthorizationCodeFlow.Initializer
    {
        ClientSecrets = new ClientSecrets
        {
            ClientId = settings.GoogleClientId ?? throw new InvalidOperationException("Falta GOOGLE_CLIENT_ID"),
            ClientSecret = settings.GoogleClientSecret ?? throw new InvalidOperationException("Falta GOOGLE_CLIENT_SECRET"),
        },
        Scopes = Scopes,
        Prompt = "consent", // garantiza que Google entregue un refresh_token
    });

    /// <summary>
    /// Flujo de una sola vez para obtener el refresh token. Funciona dentro de un contenedor:
    /// el usuario abre la URL, autoriza y pega la URL de redirección (aunque el navegador marque error).
    /// </summary>
    public async Task AuthorizeInteractiveAsync(CancellationToken ct)
    {
        var flow = CreateFlow();
        var url = flow.CreateAuthorizationCodeRequest(RedirectUri).Build();

        Console.WriteLine("1. Abre esta URL en tu navegador e inicia sesión con la cuenta que tiene acceso a la carpeta:\n");
        Console.WriteLine(url);
        Console.WriteLine("\n2. Al terminar, el navegador irá a http://localhost:53682/?code=... y mostrará un error de conexión. Es normal.");
        Console.Write("3. Copia la URL completa de la barra de direcciones y pégala aquí: ");

        var pasted = Console.ReadLine()?.Trim() ?? "";
        var code = pasted.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? System.Web.HttpUtility.ParseQueryString(new Uri(pasted).Query)["code"]
            : pasted;
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("No se encontró el parámetro 'code' en lo que pegaste.");

        var token = await flow.ExchangeCodeForTokenAsync("user", code, RedirectUri, ct);
        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new InvalidOperationException("Google no devolvió refresh_token. Revoca el acceso de la app en https://myaccount.google.com/permissions y repite.");

        Console.WriteLine("\nListo. Agrega esta línea a tu .env:\n");
        Console.WriteLine($"GOOGLE_REFRESH_TOKEN={token.RefreshToken}");
    }

    /// <summary>Crea el archivo en la carpeta o, si ya existe uno con el mismo nombre, reemplaza su contenido.</summary>
    public async Task<string> UploadAsync(string localPath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.GoogleRefreshToken))
            throw new InvalidOperationException("Falta GOOGLE_REFRESH_TOKEN. Ejecuta el comando 'auth' primero.");
        if (string.IsNullOrEmpty(settings.DriveFolderId))
            throw new InvalidOperationException("Falta DRIVE_FOLDER_ID.");

        var credential = new UserCredential(CreateFlow(), "user", new TokenResponse { RefreshToken = settings.GoogleRefreshToken });
        using var drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "VentasExport",
        });

        var fileName = Path.GetFileName(localPath);
        var existingId = await FindFileAsync(drive, fileName, ct);

        await using var stream = File.OpenRead(localPath);
        Google.Apis.Upload.IUploadProgress progress;
        string? id;
        if (existingId is null)
        {
            var create = drive.Files.Create(
                new DriveFile { Name = fileName, Parents = [settings.DriveFolderId] }, stream, XlsxMime);
            create.SupportsAllDrives = true;
            create.Fields = "id, webViewLink";
            progress = await create.UploadAsync(ct);
            id = create.ResponseBody?.Id;
        }
        else
        {
            var update = drive.Files.Update(new DriveFile(), existingId, stream, XlsxMime);
            update.SupportsAllDrives = true;
            update.Fields = "id, webViewLink";
            progress = await update.UploadAsync(ct);
            id = existingId;
        }

        if (progress.Exception is not null) throw progress.Exception;
        return id ?? throw new InvalidOperationException("Drive no devolvió el id del archivo.");
    }

    private async Task<string?> FindFileAsync(DriveService drive, string fileName, CancellationToken ct)
    {
        var list = drive.Files.List();
        list.Q = $"name = '{fileName.Replace("'", "\\'")}' and '{settings.DriveFolderId}' in parents and trashed = false";
        list.Fields = "files(id)";
        list.SupportsAllDrives = true;
        list.IncludeItemsFromAllDrives = true;
        var result = await list.ExecuteAsync(ct);
        return result.Files.FirstOrDefault()?.Id;
    }
}
