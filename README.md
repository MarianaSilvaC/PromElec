# PromElec — Tienda API segura (.NET 8)

Web API de artículos electrónicos promocionales de **PromElec**, modernizada en el reto **"Proyecto Escudo PromElec: Seguridad, Pruebas y Despliegue Inteligente"**. Se partió del código original de PromElec y se le hicieron cuatro cosas:

- **Seguridad:** un análisis basado en el **OWASP Top 10** encontró **16 vulnerabilidades**, y todas se corrigieron.
- **Pruebas:** **10 unitarias** y **20 de integración**; las 30 pasan.
- **Docker:** imagen multi-stage publicada en **Docker Hub**.
- **Despliegue:** en un **servidor Linux**, detrás de **Nginx** con HTTPS y **balanceo de carga** entre dos instancias.

| | Enlace |
|---|---|
| Repositorio | [github.com/MarianaSilvaC/PromElec](https://github.com/MarianaSilvaC/PromElec) |
| Imagen en Docker Hub | [hub.docker.com/r/marianasilcam05/promelec-api](https://hub.docker.com/r/marianasilcam05/promelec-api) (`1.1` y `latest`) |
| Despliegue en servidor Linux | `https://45.132.240.125:8443`: certificado autofirmado, ver [evidencias](docs/evidencias/README.md) |

**Tecnologías:** C# · ASP.NET Core 8 (LTS) · Entity Framework Core 8 · ASP.NET Core Identity + JWT · SQL Server 2022 / SQLite · xUnit · Moq · WebApplicationFactory · Docker · Nginx · Visual Studio Code.

---

## Contenido del repositorio

```
PromElec/
├─ PromElec.sln                        Solución (API + 2 proyectos de pruebas)
├─ global.json                         Fija el SDK de .NET 8
├─ .config/dotnet-tools.json           Herramienta dotnet-ef 8 (migraciones)
├─ src/TiendaPromElec/                 La API
│  ├─ Controllers/                     Product, Order, Auth
│  ├─ Services/  Repositories/         Reglas de negocio y acceso a datos (con interfaces)
│  ├─ Dtos/                            Datos de entrada/salida con validación
│  ├─ Security/  Infrastructure/       JWT, cabeceras de seguridad, errores, inicialización de la BD
│  ├─ Pages/                           Catálogo y formulario de Contacto (Razor)
│  ├─ Migrations/                      Migraciones de EF Core
│  └─ Dockerfile                       Imagen multi-stage
├─ tests/TiendaPromElec.UnitTests/         10 pruebas unitarias (xUnit + Moq)
├─ tests/TiendaPromElec.IntegrationTests/  20 pruebas de integración (WebApplicationFactory)
├─ docker-compose.yml                  API + SQL Server en contenedores (local)
├─ deploy/                             Despliegue en el servidor Linux (Nginx + 2 instancias)
└─ docs/                               Análisis de seguridad, pruebas, Docker, despliegue y evidencias
```

---

## Requisitos

| Herramienta | Para qué |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Compilar, ejecutar y probar. El `global.json` elige automáticamente un SDK 8.0.x |
| SQL Server (Express o Developer) | Base de datos para ejecutar la API en local. Las pruebas no lo necesitan |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Contenedores |
| Visual Studio Code + extensión C# Dev Kit | Editor |

---

## 1. Restaurar dependencias

Desde la carpeta raíz del repositorio:

```powershell
dotnet restore
dotnet tool restore
```

`dotnet restore` descarga los paquetes NuGet de los tres proyectos. `dotnet tool restore` instala la herramienta local `dotnet-ef` 8.0.34.

---

## 2. Configurar los secretos

La API **no guarda contraseñas ni llaves en `appsettings.json`**. En desarrollo se configuran con **User Secrets**, que se guardan fuera del proyecto, en el perfil del usuario:

```powershell
cd src/TiendaPromElec

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=PromElec;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "PEGA_AQUI_UNA_LLAVE_ALEATORIA_DE_32_O_MAS_CARACTERES"
dotnet user-secrets set "SeedAdmin:Password" "PEGA_AQUI_UNA_CONTRASEÑA_CON_MAYUSCULA_minuscula_NUMERO_Y_SIMBOLO"

cd ../..
```

Para generar una llave aleatoria en PowerShell:

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

- Ajusta `Server=` a tu instalación: `.\SQLEXPRESS`, `localhost` o `(localdb)\MSSQLLocalDB`.
- Si falta la cadena de conexión o la llave JWT, **la API no arranca** y el mensaje dice qué falta.
- Para ver tus secretos: `dotnet user-secrets list --project src/TiendaPromElec`.

---

## 3. Ejecutar la API

```powershell
dotnet dev-certs https --trust
dotnet run --project src/TiendaPromElec
```

- Swagger: **https://localhost:7131/swagger** · Catálogo: **https://localhost:7131/**
- Al arrancar, la API aplica las migraciones pendientes, crea los roles **Admin** y **Cliente**, y crea el usuario **`admin@promelec.local`** con la contraseña de `SeedAdmin:Password`.
- Los Clientes se registran con `POST /api/auth/register`.
- **Para usar un token en Swagger:** `POST /api/auth/login` → copiar el `token` → botón **Authorize** → pegarlo.
- El archivo [`src/TiendaPromElec/TiendaPromElec.http`](src/TiendaPromElec/TiendaPromElec.http) tiene ejemplos listos para la extensión REST Client de VS Code.

### Endpoints

| Método | Ruta | Quién puede usarlo |
|---|---|---|
| GET | `/api/Product`, `/api/Product/{id}` | Público |
| POST, PUT, DELETE | `/api/Product`, `/api/Product/{id}` | Admin. Borrar un producto con pedidos responde 409 |
| GET | `/api/Order`, `/api/Order/{id}` | Admin (todos) o Cliente (solo los suyos; un pedido ajeno responde 403) |
| POST | `/api/Order` | Cliente. El total lo calcula el servidor |
| PUT | `/api/Order/{id}/status` | Admin |
| DELETE | `/api/Order/{id}` | Admin |
| POST | `/api/auth/register`, `/api/auth/login` | Público, con límite de 10 peticiones por minuto por IP |
| GET | `/api/auth/me` | Cualquier usuario autenticado |
| GET | `/`, `/Contacto`, `/health` | Público |

---

## 4. Ejecutar las pruebas

Las pruebas **no necesitan SQL Server ni secretos**: usan mocks y una base SQLite en memoria.

```powershell
dotnet test --logger "console;verbosity=normal"
```

| Proyecto | Pruebas | Qué cubre |
|---|---|---|
| `tests/TiendaPromElec.UnitTests` | **10** (5 positivas, 5 negativas) | Controladores y servicios con dependencias simuladas con Moq |
| `tests/TiendaPromElec.IntegrationTests` | **20**: 10 de funcionalidad y 10 de seguridad, cada grupo con 5 positivas y 5 negativas | La API completa en memoria con `WebApplicationFactory<Program>`: JWT, roles, IDOR, CORS, HTTPS, CSRF |

Filtros útiles:

```powershell
dotnet test tests/TiendaPromElec.UnitTests --filter "Tipo=Negativa"
dotnet test tests/TiendaPromElec.IntegrationTests --filter "Categoria=Seguridad"
```

La descripción de cada prueba está en [docs/Pruebas.md](docs/Pruebas.md).

---

## 5. Docker

### Construir la imagen

```powershell
docker build -t promelec-api:1.1 src/TiendaPromElec
```

El [Dockerfile](src/TiendaPromElec/Dockerfile) es **multi-stage**: compila con `mcr.microsoft.com/dotnet/sdk:8.0` y ejecuta con `mcr.microsoft.com/dotnet/aspnet:8.0`. La imagen final pesa 345 MB, expone el **puerto 80**, corre con un **usuario sin privilegios** y **no contiene secretos**.

### Ejecutar con `docker compose` (API + SQL Server)

```powershell
Copy-Item .env.example .env      # luego edita .env y pon valores propios
docker compose up -d --build
docker compose ps
```

La API queda en **http://localhost:8080** y SQL Server 2022 en un contenedor sin puertos públicos. Para detenerla: `docker compose down`.

### Ejecutar con `docker run`

```powershell
Copy-Item api.env.example api.env   # usa los mismos valores que en .env
docker compose up -d sqlserver
docker run -d --name promelec-api-run --network promelec_default -p 8081:80 --env-file api.env -v promelec_promelec-keys:/app/keys promelec-api:1.1
```

### Usar la imagen publicada

```powershell
docker pull marianasilcam05/promelec-api:1.1
```

El proceso completo, incluida la subida a Docker Hub, está en [docs/Docker.md](docs/Docker.md).

---

## 6. Despliegue en servidor Linux

La API está desplegada en un VPS **Ubuntu 24.04**, junto a otros servicios en producción y sin afectarlos:

```
Internet ──► :8443 (HTTPS) ──► Nginx (TLS + cabeceras de seguridad + balanceo) ──► API 1 / API 2 ──► SQLite
Internet ──► :8088 (HTTP)  ──► Nginx ──► 301 a HTTPS
```

- **Nginx 1.30** como proxy inverso, con `upstream` en *round robin* y reintento automático si una instancia falla.
- Las instancias de la API **no publican puertos**; solo Nginx las alcanza.
- **SQLite** y límites de memoria: todo el despliegue usa unos 130 MB.

Guía paso a paso: [docs/Despliegue.md](docs/Despliegue.md). Capturas: [docs/evidencias](docs/evidencias/README.md).

---

## 7. Resumen de seguridad

| Requisito | Implementación |
|---|---|
| **Autenticación y autorización** | ASP.NET Core Identity + **JWT**; roles **Admin** y **Cliente**; política que **niega por defecto**; un Cliente solo ve sus pedidos |
| **Contraseñas fuera de la configuración** | User Secrets en desarrollo y variables de entorno o `.env` (fuera de git) en Docker; la API no arranca si falta un secreto |
| **Forzado de HTTPS** | Redirección a HTTPS, **HSTS**, cookies `Secure` y TLS 1.2/1.3 en Nginx |
| **CORS seguro** | Lista blanca de orígenes, métodos y cabeceras; nunca `AllowAnyOrigin` |
| **CSRF** | Tokens antiforgery en los formularios Razor, cookies `SameSite=Strict`, y la API usa JWT en la cabecera (no cookies) |
| Otros controles | DTOs contra *overposting*, validación de datos, cabeceras de seguridad, SRI, *rate limiting*, bloqueo tras 5 intentos fallidos, registro de eventos de seguridad, manejo global de errores, dependencias sin vulnerabilidades |

El análisis completo, con evidencia por archivo y línea y la tabla de antes y después, está en [docs/AnalisisSeguridad.md](docs/AnalisisSeguridad.md).

---

## 8. Mejoras futuras

- **Migrar a .NET 10 LTS:** el soporte oficial de .NET 8 terminó el 10 de noviembre de 2026.
- Publicar con un **dominio** y un certificado de **Let's Encrypt**.
- **CI/CD** con GitHub Actions: compilar, correr las 30 pruebas, escanear y publicar la imagen en cada cambio.
- Concurrencia optimista en las existencias (`rowversion`), para pedidos simultáneos del mismo producto.
- Usuario de SQL Server con permisos mínimos para la API, e imagen base *chiseled*.
- Gestor de secretos (Azure Key Vault o Docker secrets) en lugar de archivos `.env`.

---

## Documentación

| Documento | Contenido |
|---|---|
| [docs/AnalisisSeguridad.md](docs/AnalisisSeguridad.md) | Análisis OWASP Top 10: 16 hallazgos, correcciones y verificación |
| [docs/Pruebas.md](docs/Pruebas.md) | Las 30 pruebas: qué verifica cada una y cómo ejecutarlas |
| [docs/Docker.md](docs/Docker.md) | Dockerfile explicado, `docker compose`, `docker run` y Docker Hub |
| [docs/Despliegue.md](docs/Despliegue.md) | Despliegue en Linux con Nginx, HTTPS y balanceo de carga |
| [docs/evidencias/README.md](docs/evidencias/README.md) | Capturas del despliegue |
