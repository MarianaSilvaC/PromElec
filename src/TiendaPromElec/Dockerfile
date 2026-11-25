# =====================================================================================
# Dockerfile de la API TiendaPromElec (.NET 8) — construcción en dos etapas (multi-stage)
#
# Etapa 1 "build": usa la imagen GRANDE del SDK (~800 MB) para restaurar, compilar y publicar.
# Etapa 2 "final": usa la imagen PEQUEÑA de ASP.NET (~220 MB), que solo sabe EJECUTAR.
#   La imagen final no lleva el SDK, el código fuente ni las herramientas de compilación:
#   es más ligera y tiene menos superficie de ataque.
#
# Construir (desde la carpeta raíz del repositorio):
#   docker build -t promelec-api:1.1 src/TiendaPromElec
# =====================================================================================

# ---------- Etapa 1: compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero SOLO el .csproj y la restauración de paquetes.
# Docker guarda esta capa en caché: si después solo cambia el código (y no los paquetes),
# el siguiente "docker build" no vuelve a descargar las dependencias.
COPY TiendaPromElec.csproj ./
RUN dotnet restore TiendaPromElec.csproj --runtime linux-x64

# Después, el resto del código. Se compila y publica en modo Release.
# --runtime linux-x64: solo se incluyen las librerías nativas de Linux x64 (SQL Server, SQLite),
# no las de Windows, macOS o ARM. La imagen queda más pequeña.
COPY . ./
RUN dotnet publish TiendaPromElec.csproj \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained false \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- Etapa 2: ejecución ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# La API escucha en el puerto 80 dentro del contenedor y corre en modo Producción
# (sin Swagger, con HSTS y sin páginas de error detalladas).
ENV ASPNETCORE_HTTP_PORTS=80 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80

# Solo se copia el resultado publicado de la etapa 1.
COPY --from=build /app/publish ./

# Carpetas propiedad del usuario sin privilegios, donde docker-compose monta volúmenes:
#   /app/keys -> llaves de Data Protection (persisten entre reinicios y se comparten entre instancias)
#   /app/data -> archivo de base de datos cuando se usa Database__Provider=Sqlite
RUN mkdir -p /app/keys /app/data && chown $APP_UID /app/keys /app/data
ENV DataProtection__KeysPath=/app/keys

# Seguridad: el proceso NO corre como root, sino como el usuario sin privilegios "app"
# que ya trae la imagen oficial de .NET 8.
USER $APP_UID

# Los secretos (cadena de conexión, Jwt__Key, SeedAdmin__Password) NO van en la imagen:
# se pasan al ejecutar el contenedor con variables de entorno (docker run -e / --env-file).
ENTRYPOINT ["dotnet", "TiendaPromElec.dll"]
