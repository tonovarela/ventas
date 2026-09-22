# Compila en la arquitectura del host y genera binarios para la arquitectura destino (amd64/arm64).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY src/VentasExport/VentasExport.csproj VentasExport/
RUN dotnet restore VentasExport/VentasExport.csproj -a $TARGETARCH
COPY src/VentasExport/ VentasExport/
RUN dotnet publish VentasExport/VentasExport.csproj -c Release -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
ENV TZ=America/Mexico_City \
    SQL_DIR=/app/sql \
    OUTPUT_DIR=/app/output
COPY --from=build /app .
COPY sql/ /app/sql/
RUN mkdir -p /app/output && chown $APP_UID /app/output
USER $APP_UID
ENTRYPOINT ["dotnet", "VentasExport.dll"]
