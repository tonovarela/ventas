# ventas-export

Ejecuta las consultas de `sql/` (excepto `ventas.sql`), genera `<AñoFiscal>-W<Semana>.xlsx` (ej. `2026-W38.xlsx`)
con una hoja por consulta más una hoja `Resumen`, y lo sube a una carpeta compartida de Google Drive.
Si el archivo de esa semana ya existe en la carpeta, se reemplaza su contenido (no se duplica).

Stack: .NET 8 · Microsoft.Data.SqlClient · ClosedXML · Google.Apis.Drive.v3.

El contenedor se ejecuta una vez y termina; la programación la hace el crontab del servidor.

## Comandos

| Comando | Qué hace |
|---|---|
| *(ninguno)* o `run` | Genera el Excel, lo sube a Drive y termina |
| `--no-upload` | Solo genera el Excel en `/app/output` |
| `auth` | Obtiene el `GOOGLE_REFRESH_TOKEN` (una sola vez) |

## 1. Credenciales de Google (una sola vez)

La carpeta es compartida (no es una Unidad compartida), así que se usa OAuth con la cuenta de un
usuario que tenga permiso de **Editor** en la carpeta. Una Service Account no funciona en este caso.

1. En [Google Cloud Console](https://console.cloud.google.com/) crea un proyecto y habilita **Google Drive API**.
2. **Pantalla de consentimiento de OAuth** → tipo *Externo* (o *Interno* si es Workspace) → agrega tu correo como usuario de prueba.
3. **Publica la app ("En producción")**. Si se queda en *Prueba*, el refresh token caduca a los 7 días
   y la tarea deja de subir archivos. Google mostrará la advertencia "app no verificada"; es normal en uso interno.
4. **Credenciales → Crear ID de cliente OAuth → App de escritorio**. Copia el ID y el secreto en `.env`.
5. Obtén el refresh token:

   ```bash
   cp .env.example .env    # y llena SQL_* y GOOGLE_CLIENT_*
   docker build -t ventas-export .
   docker run --rm -it --env-file .env ventas-export auth
   ```

   Abre la URL, autoriza y pega la URL de `localhost` a la que te redirige (aunque el navegador marque error).
   Copia el `GOOGLE_REFRESH_TOKEN` que imprime a tu `.env`.
6. `DRIVE_FOLDER_ID` es la última parte de la URL de la carpeta: `https://drive.google.com/drive/folders/<ID>`.

## 2. Probar

```bash
docker run --rm --env-file .env -v "$PWD/output:/app/output" ventas-export
```

## 3. Publicar en Docker Hub (amd64 + arm64)

```bash
docker login
docker buildx build --platform linux/amd64,linux/arm64 -t <usuario>/ventas-export:latest --push .
```

## 4. Programar en el crontab del servidor

```bash
docker pull <usuario>/ventas-export:latest
crontab -e
```

```cron
# Lunes 07:00 (hora del servidor)
0 7 * * 1  docker run --rm --env-file /opt/ventas-export/.env -v /opt/ventas-export/output:/app/output <usuario>/ventas-export:latest >> /var/log/ventas-export.log 2>&1
```

- Usa rutas absolutas: cron no corre desde tu directorio.
- El usuario del crontab debe poder ejecutar `docker` (grupo `docker` o crontab de root).
- El proceso termina con código `1` si algo falla; el detalle queda en el log.
- La semana del reporte se calcula con `TZ` del contenedor (`America/Mexico_City` por defecto), no con la del servidor.

## Notas

- El `.env` nunca entra a la imagen (`.dockerignore`); se pasa con `--env-file`. No uses comillas en los valores.
- Las consultas van dentro de la imagen. Para cambiarlas sin reconstruir, monta `./sql:/app/sql:ro` (ver `docker-compose.yml`).
- Los scripts se separan por `GO` igual que en SSMS, así que `USE DataIA` y los `DECLARE` funcionan tal cual.
- El usuario de SQL necesita lectura en `DataIA`, `etl_mstr` y `litocrm`.


## Para desarrollo

dotnet run --project src/VentasExport -- --no-upload