# Dockerización — TiendaPromElec

La API corre en un contenedor Docker a partir de la imagen `promelec-api:1.1` (345 MB). Usa como base la imagen oficial `mcr.microsoft.com/dotnet/aspnet:8.0`, escucha en el **puerto 80** y obtiene su base de datos de un contenedor de **SQL Server 2022**. Las dos piezas se levantan juntas con `docker compose`.

| Archivo | Para qué sirve |
|---|---|
| [`src/TiendaPromElec/Dockerfile`](../src/TiendaPromElec/Dockerfile) | Receta de la imagen de la API (multi-stage) |
| [`src/TiendaPromElec/.dockerignore`](../src/TiendaPromElec/.dockerignore) | Archivos que no entran a la imagen (`bin/`, `obj/`, configuración de desarrollo, secretos) |
| [`docker-compose.yml`](../docker-compose.yml) | Levanta la API + SQL Server con una red, volúmenes y verificación de salud |
| [`.env.example`](../.env.example) | Plantilla de secretos para `docker compose` (el `.env` real no se sube a git) |
| [`api.env.example`](../api.env.example) | Plantilla de variables para `docker run --env-file` (el `api.env` real no se sube a git) |

---

## 1. El Dockerfile explicado

La imagen se construye en **dos etapas (multi-stage)**:

| Etapa | Imagen base | Qué hace |
|---|---|---|
| `build` | `mcr.microsoft.com/dotnet/sdk:8.0` | Restaura paquetes, compila y publica en modo Release |
| `final` | `mcr.microsoft.com/dotnet/aspnet:8.0` | Solo **ejecuta**: recibe los archivos publicados y nada más |

| Optimización o control | Cómo se logra | Beneficio |
|---|---|---|
| Imagen final pequeña | Multi-stage: el SDK solo existe en la etapa `build` | Menos peso y menos superficie de ataque. Comprobado: la imagen no tiene `/usr/share/dotnet/sdk` ni archivos `.cs` |
| Construcciones rápidas | Se copia primero el `.csproj` y se hace `dotnet restore`; el código se copia después | Si solo cambia el código, Docker reutiliza la capa de paquetes en caché |
| Usuario sin privilegios | `USER $APP_UID` (usuario `app`, uid 1654) | Si alguien comprometiera la API, no sería `root` dentro del contenedor. Comprobado con `docker exec promelec-api id` |
| Sin secretos en la imagen | Los secretos llegan por variables de entorno al ejecutar | La imagen se puede publicar en Docker Hub sin exponer contraseñas (H-05) |
| Contexto mínimo | `.dockerignore` excluye `bin/`, `obj/`, `appsettings.Development.json`, `*.env`, certificados | Construcción más rápida; no se cuelan archivos locales ni sensibles |
| Modo producción | `ASPNETCORE_ENVIRONMENT=Production` | Sin Swagger, con HSTS y sin páginas de error detalladas |
| Llaves persistentes | Carpeta `/app/keys` (propiedad de `app`) montada como volumen | Los tokens antiforgery siguen siendo válidos tras reiniciar y entre varias instancias |
| Solo librerías de Linux x64 | `dotnet publish --runtime linux-x64` | No se incluyen librerías nativas de Windows, macOS ni ARM: la 1.1 pesa 345 MB en lugar de 401 MB |
| Motor de base de datos configurable | `Database__Provider=SqlServer` (por defecto) o `Sqlite`, con la carpeta `/app/data` para el archivo | La misma imagen sirve con SQL Server (local) o con SQLite (servidores con poca memoria, como el VPS de [Despliegue.md](Despliegue.md)) |

---

## 2. Requisitos

- **Docker Desktop** encendido, con contenedores Linux.
- Unos **4 GB** de RAM libres (SQL Server necesita al menos 2 GB).
- Los archivos de secretos, creados a partir de sus plantillas, en la raíz del repositorio:

```powershell
Copy-Item .env.example .env
Copy-Item api.env.example api.env
```

Después edita los dos archivos y pon valores propios y aleatorios (los mismos en ambos).

---

## 3. Opción A (recomendada): `docker compose`

Desde la raíz del repositorio (`PromElec/`):

```powershell
docker compose up -d --build
docker compose ps
```

Qué ocurre:

1. Se construye la imagen `promelec-api:1.1`.
2. Arranca `promelec-sql` (SQL Server 2022 Express). Su puerto **no** se publica fuera de Docker.
3. Compose espera a que SQL Server esté sano (`healthcheck` con `sqlcmd`) y luego arranca `promelec-api`.
4. Al arrancar, la API aplica las migraciones, crea los roles y el usuario `admin@promelec.local` con la contraseña de `ADMIN_PASSWORD`.
5. La API queda disponible en **http://localhost:8080**.

Detener y limpiar:

```powershell
docker compose down        # detiene los contenedores; los datos quedan en el volumen
docker compose down -v     # además BORRA los volúmenes (base de datos y llaves)
```

---

## 4. Opción B (la de la guía): `docker build` + `docker run`

```powershell
# 1. Construir la imagen
docker build -t promelec-api:1.1 src/TiendaPromElec

# 2. Levantar solo la base de datos (si no está arriba)
docker compose up -d sqlserver

# 3. Ejecutar la API conectada a la red de compose, con los secretos en api.env
docker run -d --name promelec-api-run --network promelec_default -p 8081:80 --env-file api.env -v promelec_promelec-keys:/app/keys promelec-api:1.1
```

La API queda en **http://localhost:8081**. Se usa `--env-file` en lugar de `-e` para que los secretos no aparezcan en la pantalla ni en el historial de la terminal. Para detenerla: `docker rm -f promelec-api-run`.

---

## 5. Probar la API desde un cliente

```powershell
curl.exe http://localhost:8080/health
curl.exe http://localhost:8080/api/Product/2
curl.exe -i http://localhost:8080/api/Order
```

En un navegador, `http://localhost:8080/` muestra el catálogo.

Resultados verificados el 25/11/2026:

| Prueba | Resultado |
|---|---|
| `GET /health` | 200 `Healthy` |
| `GET /api/Product` | 200 con 10 productos |
| `GET /api/Order` sin token | 401 |
| Login del Admin y `GET /api/Order` | 200 con 5 pedidos |
| `DELETE /api/Product/2` (tiene pedidos) | 409 |
| `GET /swagger` | No disponible en producción |
| Cabeceras de `GET /` | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, CSP; sin `Server` |
| Usuario del proceso | `uid=1654(app)` |
| Segunda instancia con `docker run` en el puerto 8081 | 200; las dos instancias comparten la base de datos y las llaves |

---

## 6. Subir la imagen a Docker Hub

Hazlo con tu cuenta; necesitas un usuario en [hub.docker.com](https://hub.docker.com). En lugar de la contraseña, usa un **Personal Access Token**: en Docker Hub, *Account settings → Personal access tokens → Generate new token*, con permisos *Read & Write*.

```powershell
# 1. Iniciar sesión (te pide el token; pégalo, no se ve al escribir)
docker login -u TU_USUARIO

# 2. Etiquetar la imagen con tu usuario: versión 1.1 y latest
docker tag promelec-api:1.1 TU_USUARIO/promelec-api:1.1
docker tag promelec-api:1.1 TU_USUARIO/promelec-api:latest

# 3. Subir las dos etiquetas
docker push TU_USUARIO/promelec-api:1.1
docker push TU_USUARIO/promelec-api:latest
```

**Imagen publicada del proyecto:** [hub.docker.com/r/marianasilcam05/promelec-api](https://hub.docker.com/r/marianasilcam05/promelec-api), con las etiquetas `1.0`, `1.1` y `latest` (la 1.1 es la versión actual).

Para descargarla y ejecutarla en cualquier equipo con Docker:

```powershell
docker pull marianasilcam05/promelec-api:1.1
```

Para usar la imagen publicada en otra máquina (por ejemplo, un servidor Linux), en `.env` pon `API_IMAGE=marianasilcam05/promelec-api:1.1` y ejecuta `docker compose pull api` y `docker compose up -d`.

---

## 7. Advertencias normales en el log del contenedor

| Advertencia | Por qué aparece | ¿Es un problema? |
|---|---|---|
| `Failed to determine the https port for redirect` | El contenedor solo habla HTTP; el HTTPS lo pone el proxy inverso (Nginx) delante | No. Es la arquitectura habitual: TLS en el proxy |
| `No XML encryptor configured` | Las llaves de Data Protection se guardan sin cifrar dentro del volumen | Aceptable en local. En la nube se cifrarían con un certificado o un servicio de llaves |

---

## 8. Mejoras posibles

- Usar la variante *chiseled* de la imagen de .NET (`aspnet:8.0-jammy-chiseled-extra`), que no trae shell ni gestor de paquetes y pesa menos.
- Crear un usuario de SQL Server con permisos mínimos para la API, en lugar de `sa`.
- Escanear la imagen en busca de vulnerabilidades con `docker scout cves promelec-api:1.1`.
- Guardar los secretos en un gestor (Docker secrets, Azure Key Vault) en lugar de archivos `.env`.
