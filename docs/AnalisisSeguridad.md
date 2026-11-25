# Análisis de seguridad — TiendaPromElec

**Proyecto:** Escudo PromElec: Seguridad, Pruebas y Despliegue
**Sistema analizado:** Web API `TiendaPromElec` (ASP.NET Core 8, Entity Framework Core 8, SQL Server Express), versión original entregada por PromElec
**Fecha del análisis:** 24 de noviembre de 2026
**Referencia principal:** [OWASP Top 10:2025](https://owasp.org/Top10/2025/), con su equivalente en [OWASP Top 10:2021](https://owasp.org/Top10/)
**Estado:** las 16 correcciones se implementaron el 25 de noviembre de 2026 (Fase 3). La verificación está en la sección 10.

---

## 1. Resumen ejecutivo

Se encontraron **16 hallazgos de seguridad**: 1 crítico, 4 altos, 7 medios y 4 bajos.

El problema más grave es que **la API no tiene autenticación ni autorización**. Cualquier persona puede ver los pedidos de todos los clientes y crear, modificar o borrar productos y pedidos. Este problema se agrava por otros tres:

- Los controladores reciben las entidades de la base de datos directamente, así que el cliente decide campos como el total a pagar o el estado del pedido (*overposting*).
- Borrar un producto borra en cascada las líneas de los pedidos históricos que lo contienen.
- El proyecto arrastra **12 dependencias con vulnerabilidades conocidas**, una de ellas crítica.

La sección 6 relaciona cada hallazgo con su corrección y con los cuatro controles que exige la rúbrica del reto: políticas de autorización, CORS seguro, HTTPS forzado y contraseñas fuera de los archivos de configuración.

| Severidad | Cantidad | Hallazgos |
|---|---|---|
| Crítica | 1 | H-01 |
| Alta | 4 | H-02, H-03, H-04, H-15 |
| Media | 7 | H-05, H-06, H-07, H-09, H-10, H-13, H-14 |
| Baja | 4 | H-08, H-11, H-12, H-16 |

---

## 2. Alcance y metodología

**Alcance:** todo el código fuente del proyecto `TiendaPromElec`. Eso incluye `Program.cs`, los controladores `ProductController`, `OrderController` y `WeatherForecastController`, los modelos, el `AppDbContext`, la migración `InitialCreate`, la página Razor `Index`, `appsettings*.json`, `launchSettings.json` y el archivo `.csproj`.

**Técnicas usadas:**

1. **Revisión manual del código** contra las categorías del OWASP Top 10 y las guías de seguridad de ASP.NET Core.
2. **Pruebas dinámicas** contra la API en ejecución (`dotnet run`, `http://localhost:5152`) con `curl` y Swagger. Los resultados están en la sección 7.
3. **Análisis de dependencias** con `dotnet list package --vulnerable --include-transitive`, que consulta la base de avisos de GitHub (GHSA).
4. **Advertencias del compilador** (`dotnet build --no-incremental`): 0 errores y 9 advertencias CS8618.

**Escala de severidad usada:**

| Severidad | Criterio |
|---|---|
| Crítica | Explotable sin autenticación y compromete todos los datos o todas las operaciones. |
| Alta | Permite leer o modificar datos de otros, o es una vulnerabilidad pública conocida de severidad alta o crítica. |
| Media | Debilita una defensa importante o facilita otro ataque, pero no basta por sí sola. |
| Baja | Endurecimiento recomendado o superficie innecesaria con riesgo limitado. |

---

## 3. Controles que ya estaban presentes

No todo estaba mal. Estos controles del código original son correctos y se conservan:

| Control | Evidencia | Comentario |
|---|---|---|
| Swagger solo en desarrollo | `Program.cs:22-26` | En producción no se expone la documentación interactiva. |
| Redirección a HTTPS | `Program.cs:28` | Existe, pero sin HSTS y con el perfil `http` por defecto (ver H-06). |
| Consultas parametrizadas | Todo el acceso a datos usa EF Core con LINQ | No hay `FromSqlRaw`, `ExecuteSqlRaw` ni SQL concatenado, así que no se encontró inyección SQL. |
| Codificación de salida HTML | `Pages/Index.cshtml` usa `@item.Name` y similares | Razor codifica la salida y no se usa `Html.Raw`, así que no se encontró XSS. |
| Validación automática del modelo | `[ApiController]` en los controladores | Devuelve 400 con *ProblemDetails* cuando el modelo es inválido, aunque los modelos no definen reglas (ver H-07). |

---

## 4. Tabla de hallazgos

| ID | Hallazgo | OWASP 2025 | OWASP 2021 | Severidad | Estado |
|---|---|---|---|---|---|
| H-01 | La API no tiene autenticación ni autorización | A01, A07 | A01, A07 | Crítica | Corregido |
| H-02 | Cualquiera puede ver los pedidos de cualquier cliente (IDOR) | A01 | A01 | Alta | Corregido |
| H-03 | *Overposting*: el cliente controla el total, el estado y el cliente del pedido | A06, A08 | A04, A08 | Alta | Corregido |
| H-04 | 12 dependencias con vulnerabilidades conocidas (1 crítica) | A03 | A06 | Alta | Corregido |
| H-05 | Cadena de conexión en `appsettings.json` y certificado de SQL sin validar | A02, A04 | A05, A02 | Media | Corregido |
| H-06 | HTTPS no forzado: sin HSTS y con perfil `http` por defecto | A04 | A02 | Media | Corregido |
| H-07 | Sin validación de datos de entrada | A05 | A03 | Media | Corregido |
| H-08 | Sin política CORS definida | A02 | A05 | Baja | Corregido |
| H-09 | Protección CSRF y cookies SameSite sin configurar | A01 | A01 | Media | Corregido |
| H-10 | Faltan cabeceras de seguridad y se expone `Server: Kestrel` | A02 | A05 | Media | Corregido |
| H-11 | Superficie innecesaria: endpoint de plantilla, `AllowedHosts: *` y paquetes de *scaffolding* | A02 | A05 | Baja | Corregido |
| H-12 | Bootstrap se carga de un CDN sin verificación de integridad (SRI) | A08 | A08 | Baja | Corregido |
| H-13 | No se registran eventos de seguridad | A09 | A09 | Media | Corregido |
| H-14 | Sin límite de intentos ni política de contraseñas | A07 | A07 | Media | Corregido |
| H-15 | Borrar un producto borra en cascada el historial de pedidos | A06 | A04 | Alta | Corregido |
| H-16 | Manejo de excepciones incompleto | A10 | A05 | Baja | Corregido |

---

## 5. Detalle de cada hallazgo

### H-01 · La API no tiene autenticación ni autorización

- **Severidad:** Crítica
- **OWASP:** A01:2025 Broken Access Control y A07:2025 Authentication Failures (A01 y A07 en 2021)
- **CWE:** CWE-306 (falta de autenticación en una función crítica), CWE-862 (falta de autorización)
- **Evidencia:**
  - `Program.cs:30` llama a `app.UseAuthorization()`, pero nunca se registra un esquema con `AddAuthentication()`.
  - Ningún controlador ni acción tiene el atributo `[Authorize]`.
  - Prueba dinámica: `curl.exe -i -X DELETE http://localhost:5152/api/Product/999` responde **404 Not Found**. La API procesó el borrado sin pedir credenciales; lo correcto sería **401 Unauthorized**.
- **Impacto:** cualquier persona con acceso a la red puede crear, modificar y borrar productos y pedidos, y leer la información de todos los clientes.
- **Corrección:**
  - ASP.NET Core Identity para gestionar usuarios. Las contraseñas se guardan con hash PBKDF2 y sal, nunca en texto plano.
  - Autenticación con **JWT Bearer**. Se validan emisor, audiencia, firma y expiración, y el token dura 60 minutos.
  - Dos roles: **Admin** (administra el catálogo y todos los pedidos) y **Cliente** (consulta el catálogo y gestiona sus propios pedidos).
  - Endpoints `POST /api/auth/register` y `POST /api/auth/login`.
  - **Denegar por defecto:** una política *fallback* exige estar autenticado en todo endpoint. Solo se abren con `[AllowAnonymous]` la consulta del catálogo, el registro, el login y la página del catálogo.
  - Políticas con nombre (`SoloAdmin`, `SoloCliente`) en las acciones que modifican datos.
- **Verificación (Fase 5):** un usuario anónimo recibe 401 al escribir; un Cliente recibe 403 en acciones de administrador; un Admin recibe 2xx.

### H-02 · Cualquiera puede ver los pedidos de cualquier cliente (IDOR)

- **Severidad:** Alta
- **OWASP:** A01:2025 Broken Access Control (A01:2021)
- **CWE:** CWE-639 (saltar la autorización mediante una llave controlada por el usuario)
- **Evidencia:**
  - `OrderController.cs:28` (`GetOrders`) devuelve **todos** los pedidos.
  - `OrderController.cs:35` (`GetOrder`) devuelve cualquier pedido con solo conocer su `id`, que es un número consecutivo fácil de adivinar.
  - Prueba dinámica: `curl.exe http://localhost:5152/api/Order` responde 200 con los 5 pedidos de los 2 clientes, sin iniciar sesión.
- **Impacto:** fuga de información comercial y personal (montos, fechas y estados de compra de cada cliente).
- **Corrección:** el `CustomerId` se toma del token y no de la petición. Un Cliente solo ve sus pedidos; si pide el de otro cliente recibe 403. El Admin ve todos.
- **Verificación (Fase 5):** un Cliente que pide un pedido ajeno recibe 403, y su lista de pedidos solo contiene los suyos.

### H-03 · *Overposting*: el cliente controla el total, el estado y el cliente del pedido

- **Severidad:** Alta
- **OWASP:** A06:2025 Insecure Design y A08:2025 Software or Data Integrity Failures (A04 y A08 en 2021)
- **CWE:** CWE-915 (asignación masiva de atributos), CWE-602 (confiar en el cliente para un control del servidor)
- **Evidencia:**
  - `ProductController.cs:85` y `OrderController.cs:81` reciben las entidades `Product` y `Order` completas.
  - `ProductController.cs:61` y `OrderController.cs:57` marcan la entidad recibida como modificada (`EntityState.Modified`), así que el cliente puede sobrescribir **cualquier** columna.
  - `Models/Order.cs:10-11`: el cliente envía `TotalAmount` y `Status`. Nada impide registrar un pedido de $38,000.75 con un total de $1, o marcarlo como "Entregado".
  - Las propias plantillas lo advierten en `ProductController.cs:83` y `OrderController.cs:79`: *"To protect from overposting attacks..."*.
  - **Efecto funcional:** como las propiedades de navegación (`Category`, `Customer`, `Items`) son no nulas, ASP.NET las vuelve obligatorias. Por eso `POST /api/Product` y `POST /api/Order` **siempre responden 400** y no se pueden crear productos ni pedidos. Es la causa de las 9 advertencias CS8618.
- **Impacto:** fraude en precios y montos, manipulación de estados y asignación de pedidos a otros clientes.
- **Corrección:**
  - **DTOs** (objetos de entrada y salida) con solo los campos permitidos, separados de las entidades.
  - El servidor calcula `UnitPrice` con el precio vigente en la base de datos, y `TotalAmount` como la suma de cantidad × precio.
  - El estado inicial siempre es "Pendiente" y el cliente se toma del token.
  - Solo el Admin cambia el estado de un pedido.
  - Se valida que haya existencias suficientes y se descuentan al crear el pedido.
- **Verificación (Fases 4 y 5):** pruebas unitarias del cálculo del total y del rechazo por existencias insuficientes; prueba de integración que manda un total falso y comprueba que se ignora.

### H-04 · 12 dependencias con vulnerabilidades conocidas (1 crítica)

- **Severidad:** Alta
- **OWASP:** A03:2025 Software Supply Chain Failures (A06:2021 Vulnerable and Outdated Components)
- **CWE:** CWE-1395 (dependencia de un componente de terceros vulnerable)
- **Evidencia:**
  - `TiendaPromElec.csproj:10,14,15,19` declara versiones flotantes (`Version="8"`), que se resuelven a la 8.0.0 de noviembre de 2023, sin parches.
  - `TiendaPromElec.csproj:19` incluye `Microsoft.VisualStudio.Web.CodeGeneration.Design`, una herramienta de *scaffolding* del IDE que mete Roslyn y las librerías de NuGet en la salida publicada.
  - `dotnet list package --vulnerable --include-transitive` reporta:

| Paquete (transitivo) | Versión | Severidad | Aviso |
|---|---|---|---|
| NuGet.Packaging | 6.3.1 | **Crítica** | [GHSA-68w7-72jg-6qpp](https://github.com/advisories/GHSA-68w7-72jg-6qpp) |
| NuGet.Common | 6.3.1 | Alta | [GHSA-6qmf-mmc7-6c2p](https://github.com/advisories/GHSA-6qmf-mmc7-6c2p) |
| NuGet.Protocol | 6.3.1 | Alta | [GHSA-6qmf-mmc7-6c2p](https://github.com/advisories/GHSA-6qmf-mmc7-6c2p) |
| Microsoft.Data.SqlClient | 5.1.1 | Alta | [GHSA-98g6-xh36-x2p7](https://github.com/advisories/GHSA-98g6-xh36-x2p7) |
| Microsoft.Extensions.Caching.Memory | 8.0.0 | Alta | [GHSA-qj66-m88j-hmgj](https://github.com/advisories/GHSA-qj66-m88j-hmgj) |
| System.Text.Json | 8.0.0 | Alta | [GHSA-hh2w-p6rv-4g7w](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w), [GHSA-8g4q-xg66-9fp4](https://github.com/advisories/GHSA-8g4q-xg66-9fp4) |
| System.Formats.Asn1 | 5.0.0 | Alta | [GHSA-447r-wph3-92pm](https://github.com/advisories/GHSA-447r-wph3-92pm) |
| Azure.Identity | 1.7.0 | Alta / Moderada | [GHSA-5mfx-4wcx-rv27](https://github.com/advisories/GHSA-5mfx-4wcx-rv27), [GHSA-m5vv-6r4h-3vj9](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9), [GHSA-wvxc-855f-jvrv](https://github.com/advisories/GHSA-wvxc-855f-jvrv) |
| Microsoft.IdentityModel.JsonWebTokens | 6.24.0 | Moderada | [GHSA-59j7-ghrg-fj52](https://github.com/advisories/GHSA-59j7-ghrg-fj52) |
| System.IdentityModel.Tokens.Jwt | 6.24.0 | Moderada | [GHSA-59j7-ghrg-fj52](https://github.com/advisories/GHSA-59j7-ghrg-fj52) |

- **Impacto:** el sistema hereda fallas públicas y documentadas, para las que ya existen herramientas de ataque.
- **Corrección:** actualizar los paquetes a la última versión de parche de la línea 8.0 (**8.0.34**) con versiones exactas; quitar `CodeGeneration.Design` y `EntityFrameworkCore.Tools`; reemplazar el manifiesto de herramientas por uno con solo `dotnet-ef` 8.0.34, en la raíz del repositorio. Si algún paquete transitivo siguiera vulnerable, se fijaría su versión corregida de forma explícita (no fue necesario).
- **Verificación:** `dotnet list package --vulnerable --include-transitive` debe reportar **cero** paquetes vulnerables.

### H-05 · Cadena de conexión en `appsettings.json` y certificado de SQL sin validar

- **Severidad:** Media
- **OWASP:** A02:2025 Security Misconfiguration y A04:2025 Cryptographic Failures (A05 y A02 en 2021)
- **CWE:** CWE-260 (contraseña en un archivo de configuración), CWE-295 (validación incorrecta de certificados)
- **Evidencia:**
  - `appsettings.json:10` guarda la cadena de conexión, y este archivo se sube al repositorio.
  - Hoy usa autenticación de Windows, pero al desplegar en un servidor o contenedor con usuario y contraseña de SQL, la contraseña quedaría en texto plano en el código fuente.
  - La misma línea usa `TrustServerCertificate=True`, que desactiva la validación del certificado TLS de SQL Server.
- **Impacto:** quien tenga acceso al repositorio, o a una imagen Docker publicada, obtiene las credenciales de la base de datos.
- **Corrección:**
  - El repositorio solo guarda valores de ejemplo, nunca secretos reales.
  - En desarrollo, los secretos van en **User Secrets** (`dotnet user-secrets`), que se guardan fuera del proyecto en el perfil del usuario.
  - En Docker van en **variables de entorno** (`ConnectionStrings__DefaultConnection`, `Jwt__Key`), leídas de un archivo `.env` que está en `.gitignore`.
  - La llave de firma JWT y la contraseña inicial del Admin siguen la misma regla.
  - La aplicación se niega a arrancar si falta algún secreto obligatorio.
  - `TrustServerCertificate=True` se limita al entorno de desarrollo local.
- **Verificación:** revisar que `appsettings.json` no contenga secretos (`git grep` sin coincidencias) y que la API arranque en Docker solo con variables de entorno.

### H-06 · HTTPS no forzado: sin HSTS y con perfil `http` por defecto

- **Severidad:** Media
- **OWASP:** A04:2025 Cryptographic Failures (A02:2021)
- **CWE:** CWE-319 (transmisión de información sensible en texto claro), CWE-523 (transporte de credenciales sin protección)
- **Evidencia:**
  - `Program.cs:28` usa `UseHttpsRedirection()`, pero no hay `UseHsts()`.
  - `launchSettings.json:17`: el primer perfil, que es el que usa `dotnet run` por defecto, es `http` y solo escucha en `http://localhost:5152`. Sin un puerto HTTPS, la redirección no tiene a dónde enviar.
- **Impacto:** cuando existan login y tokens (H-01), las contraseñas y los JWT podrían viajar sin cifrar y ser interceptados.
- **Corrección:**
  - Perfil `https` por defecto en desarrollo.
  - `UseHsts()` fuera de desarrollo, con `Strict-Transport-Security` de 1 año.
  - Las cookies (antiforgery) solo viajan por HTTPS (`Secure`, ver H-09).
  - En el despliegue, **Nginx** recibe el tráfico en 443 con TLS, redirige el puerto 80 a 443 y reenvía al contenedor. La API lee `X-Forwarded-Proto` (*Forwarded Headers*) para saber que la conexión original era HTTPS.
- **Verificación:** una petición HTTP recibe una redirección 307/308 a HTTPS, y la respuesta HTTPS incluye `Strict-Transport-Security`.

### H-07 · Sin validación de datos de entrada

- **Severidad:** Media
- **OWASP:** A05:2025 Injection (A03:2021)
- **CWE:** CWE-20 (validación de entrada incorrecta)
- **Evidencia:**
  - `Models/Product.cs:10-11`: `Price` y `Stock` no tienen restricciones, y ningún texto tiene longitud máxima.
  - En la prueba dinámica se envió `price: -100` y ninguna regla lo rechazó; el 400 vino solo por la categoría faltante (H-03).
- **Impacto:** datos corruptos (precios o existencias negativos), textos enormes que degradan la base de datos y la página, y más superficie para otros ataques.
- **Corrección:** reglas *DataAnnotations* en los DTOs: `[Required]`, `[StringLength]`, `[Range]` (precio mayor que 0, existencias de 0 en adelante, cantidad de 1 a 100) y `[Url]` para `ImageUrl`. `[ApiController]` responde 400 de forma automática cuando no se cumplen.
- **Verificación (Fases 4 y 5):** crear un producto con precio negativo responde 400, en prueba unitaria y de integración.

### H-08 · Sin política CORS definida

- **Severidad:** Baja
- **OWASP:** A02:2025 Security Misconfiguration (A05:2021)
- **CWE:** CWE-942 (política permisiva entre dominios)
- **Evidencia:** `Program.cs` no registra CORS. Hoy el navegador bloquea por defecto las lecturas desde otros orígenes, lo cual es seguro. Pero cuando PromElec tenga un frontend en otro dominio, la salida rápida suele ser `AllowAnyOrigin()`, que abre la API a cualquier sitio.
- **Corrección:** una política con nombre (`PromElecCors`) con una **lista blanca** de orígenes leída de la configuración (`Cors:AllowedOrigins`). Solo permite los métodos `GET`, `POST`, `PUT` y `DELETE` y las cabeceras `Authorization` y `Content-Type`; nunca `AllowAnyOrigin`.
- **Verificación (Fase 5):** un origen permitido recibe `Access-Control-Allow-Origin`; un origen desconocido (por ejemplo `https://sitio-malicioso.com`) no lo recibe.

### H-09 · Protección CSRF y cookies SameSite sin configurar

- **Severidad:** Media
- **OWASP:** A01:2025 Broken Access Control (A01:2021)
- **CWE:** CWE-352 (falsificación de peticiones entre sitios), CWE-1275 (cookie sensible con atributo SameSite incorrecto)
- **Evidencia:** la aplicación combina una API con páginas Razor, y no hay ninguna configuración de *antiforgery* ni de política de cookies (`SameSite`, `Secure`, `HttpOnly`).
- **Análisis:** el CSRF afecta a las sesiones que el navegador envía **solo**, es decir, las cookies. La API usará JWT en la cabecera `Authorization`, que el navegador **no** agrega por su cuenta, así que es inmune a CSRF por diseño. El riesgo queda en las páginas Razor con formularios y en cualquier cookie que se emita.
- **Corrección:**
  - Los formularios Razor usan **tokens antiforgery**. Razor Pages los genera y los valida en cada `POST`, y un `POST` sin token recibe 400.
  - La cookie antiforgery se configura con `SameSite=Strict`, `HttpOnly` y `Secure`.
  - Una política global de cookies con `MinimumSameSitePolicy = Strict` y `Secure = Always`.
  - La autenticación de la API no usa cookies.
- **Verificación (Fase 5):** un `POST` al formulario sin token antiforgery responde 400, y las cookies emitidas llevan `samesite=strict`.

### H-10 · Faltan cabeceras de seguridad y se expone `Server: Kestrel`

- **Severidad:** Media
- **OWASP:** A02:2025 Security Misconfiguration (A05:2021)
- **CWE:** CWE-693 (falla de un mecanismo de protección), CWE-1021 (*clickjacking*), CWE-200 (exposición de información)
- **Evidencia:** `curl.exe -I http://localhost:5152/` solo devuelve `Content-Type`, `Date`, `Server: Kestrel` y `Transfer-Encoding`. No hay `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Referrer-Policy` ni `Strict-Transport-Security`.
- **Impacto:** la página puede incrustarse en un *iframe* ajeno (*clickjacking*), el navegador puede adivinar tipos de contenido, y el encabezado `Server` le dice al atacante qué tecnología se usa.
- **Corrección:** un middleware que agrega `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy` y `Permissions-Policy`, y `AddServerHeader = false` en Kestrel. Nginx también las aplica en el despliegue.
- **Verificación (Fase 5):** las respuestas incluyen las cabeceras y no incluyen `Server: Kestrel`.

### H-11 · Superficie innecesaria: endpoint de plantilla, `AllowedHosts: *` y paquetes de *scaffolding*

- **Severidad:** Baja
- **OWASP:** A02:2025 Security Misconfiguration (A05:2021)
- **Evidencia:**
  - `Controllers/WeatherForecastController.cs:7`: el endpoint de ejemplo `/WeatherForecast` sigue publicado y responde 200.
  - `appsettings.json:8`: `AllowedHosts: "*"` acepta cualquier encabezado `Host`.
  - `TiendaPromElec.csproj:19` y `.config/dotnet-tools.json` contienen herramientas de generación de código que no deben ir a producción.
- **Corrección:** eliminar `WeatherForecast`; limitar `AllowedHosts` a los dominios reales en la configuración de producción; quitar las herramientas de *scaffolding*.
- **Verificación:** `GET /WeatherForecast` con token responde 404 (el endpoint ya no existe). Sin token responde 401: con la política de denegar por defecto, la API no le dice a un usuario anónimo si una ruta existe o no.

### H-12 · Bootstrap se carga de un CDN sin verificación de integridad (SRI)

- **Severidad:** Baja
- **OWASP:** A08:2025 Software or Data Integrity Failures (A08:2021)
- **CWE:** CWE-829 (inclusión de funcionalidad desde una fuente no controlada), CWE-353 (falta de verificación de integridad)
- **Evidencia:** `Pages/Index.cshtml:13` carga `bootstrap.min.css` desde `cdn.jsdelivr.net` sin los atributos `integrity` ni `crossorigin`.
- **Impacto:** si el CDN fuera comprometido, el navegador cargaría el archivo alterado sin notarlo.
- **Corrección:** agregar `integrity="sha384-…"` con el hash oficial y `crossorigin="anonymous"`, y permitir ese origen en la `Content-Security-Policy`.

### H-13 · No se registran eventos de seguridad

- **Severidad:** Media
- **OWASP:** A09:2025 Security Logging and Alerting Failures (A09:2021)
- **CWE:** CWE-778 (registro insuficiente)
- **Evidencia:** `OrderController.cs:17` declara `_logger` pero nunca lo usa. `ProductController` solo registra búsquedas, y no hay registro de cambios, borrados ni accesos.
- **Impacto:** un ataque (borrado masivo, fuerza bruta) no dejaría rastro para detectarlo ni investigarlo.
- **Corrección:** registrar inicios de sesión exitosos y fallidos, registros de usuarios, accesos denegados (401 y 403) y las altas, cambios y bajas hechas por el Admin, con usuario, IP y recurso. **Nunca** se registran contraseñas ni tokens.

### H-14 · Sin límite de intentos ni política de contraseñas

- **Severidad:** Media (aplica en cuanto exista el login de H-01)
- **OWASP:** A07:2025 Authentication Failures (A07:2021)
- **CWE:** CWE-307 (intentos de autenticación sin límite), CWE-521 (requisitos de contraseña débiles)
- **Corrección:**
  - Política de contraseñas: mínimo 8 caracteres, con mayúscula, minúscula, número y símbolo.
  - Bloqueo de la cuenta 15 minutos tras 5 intentos fallidos (*lockout* de Identity).
  - **Rate limiting** de .NET 8 en `/api/auth/*`, que responde 429 Too Many Requests al pasar el límite.
- **Verificación (Fase 5):** el login con contraseña incorrecta responde 401, y el registro con una contraseña débil responde 400.

### H-15 · Borrar un producto borra en cascada el historial de pedidos

- **Severidad:** Alta
- **OWASP:** A06:2025 Insecure Design (A04:2021)
- **Evidencia:** `Migrations/20250616051432_InitialCreate.cs:112-117` define la llave foránea `FK_OrderDetails_Products_ProductId` con `onDelete: ReferentialAction.Cascade`. Al borrar un producto, SQL Server borra también todas las líneas (`OrderDetails`) de los pedidos que lo incluyen. Junto con H-01, cualquier persona anónima podía borrar el historial de ventas con una sola petición.
- **Impacto:** pérdida irreversible de información contable. Además, los pedidos quedan con un `TotalAmount` que ya no coincide con sus líneas.
- **Corrección:** cambiar la regla a `Restrict` con una migración nueva. Si el producto tiene pedidos, la API responde **409 Conflict** con un mensaje claro en lugar de borrar el historial. Además, borrar queda reservado al Admin (H-01).
- **Verificación (Fase 5):** borrar un producto que aparece en un pedido responde 409, y el pedido conserva sus líneas.

### H-16 · Manejo de excepciones incompleto

- **Severidad:** Baja
- **OWASP:** A10:2025 Mishandling of Exceptional Conditions (A05:2021)
- **CWE:** CWE-755 (manejo incorrecto de condiciones excepcionales), CWE-209 (mensajes de error con información sensible)
- **Evidencia:** no hay un manejador global de excepciones. `ProductController.cs:75` y `OrderController.cs:71` vuelven a lanzar (`throw`) las excepciones de concurrencia, y los errores de base de datos (por ejemplo, violaciones de llave foránea) terminan en un 500 sin un formato consistente. En desarrollo se muestra la página de excepciones con la traza completa.
- **Corrección:** `UseExceptionHandler` con respuestas *ProblemDetails* (RFC 9457) que no revelan detalles internos en producción, y traducción de los errores de datos esperados a códigos claros (409 Conflict, 404 Not Found).

---

## 6. Plan de corrección y relación con la rúbrica

Todas las correcciones se implementan en la **Fase 3** dentro de `src/TiendaPromElec` y se comprueban con las pruebas automatizadas de las **Fases 4 y 5**.

| Requisito de la rúbrica / guía | Hallazgos | Corrección |
|---|---|---|
| **Políticas de autorización** | H-01, H-02, H-15 | Identity + JWT, roles Admin y Cliente, política *fallback* que deniega por defecto, políticas `SoloAdmin` y `SoloCliente`, y control de propiedad de los pedidos. |
| **CORS seguro** | H-08 | Política `PromElecCors` con lista blanca de orígenes, métodos y cabeceras. |
| **Forzado de HTTPS** | H-06 | Perfil https por defecto, `UseHttpsRedirection`, `UseHsts`, cookies `Secure` y TLS en Nginx. |
| **Contraseñas fuera de archivos de configuración** | H-05 | User Secrets en desarrollo y variables de entorno en Docker; `.env` fuera de git; la API no arranca si falta un secreto. |
| **Autenticación** (guía) | H-01, H-14 | Registro y login con JWT, política de contraseñas, *lockout* y *rate limiting*. |
| **CSRF: tokens antiforgery y cookies SameSite** (guía) | H-09 | Antiforgery en formularios Razor, cookies `SameSite=Strict` + `Secure` + `HttpOnly`, y API con JWT en cabecera. |
| Endurecimiento adicional | H-03, H-04, H-07, H-10 a H-13, H-16 | DTOs, paquetes actualizados, validación, cabeceras de seguridad, SRI, registro de eventos y manejo de errores. |

---

## 7. Evidencia de las pruebas dinámicas (código original)

Pruebas hechas el 24/11/2026 contra `http://localhost:5152`, con la API original sin modificar.

| # | Petición | Resultado obtenido | Resultado esperado en un sistema seguro | Hallazgo |
|---|---|---|---|---|
| 1 | `GET /api/Product` | 200 con los 10 productos | 200 (catálogo público) | — |
| 2 | `GET /api/Product/999` | 404 | 404 | — |
| 3 | `GET /api/Order` sin credenciales | **200 con los 5 pedidos de todos los clientes** | 401 | H-01, H-02 |
| 4 | `DELETE /api/Product/999` sin credenciales | **404**: se procesó sin autenticación | 401 | H-01 |
| 5 | `POST /api/Product` con `price: -100` | **400: The Category field is required** | 400 por el precio inválido, o 201 si los datos son válidos | H-03, H-07 |
| 6 | `POST /api/Order` con `totalAmount: 1` | **400: The Items/Customer field is required** | El total lo calcula el servidor | H-03 |
| 7 | `GET /` (cabeceras) | Sin cabeceras de seguridad; `Server: Kestrel` | Cabeceras de seguridad presentes, sin `Server` | H-10 |
| 8 | `GET /api/Product` con `Origin: https://evil.com` | Sin cabeceras CORS | Sin cabeceras CORS (se mantiene tras la corrección) | H-08 |
| 9 | `GET /WeatherForecast` | **200** | 404 con token / 401 sin token (endpoint eliminado) | H-11 |
| 10 | `dotnet list package --vulnerable` | **12 paquetes vulnerables** | 0 | H-04 |
| 11 | `dotnet build --no-incremental` | 0 errores, 9 advertencias CS8618 | 0 errores, 0 advertencias | H-03 |

### Cómo reproducir

Con la API original en ejecución (`dotnet run --launch-profile http`), en otra terminal:

```powershell
curl.exe http://localhost:5152/api/Order
curl.exe -i -X DELETE http://localhost:5152/api/Product/999
curl.exe -I http://localhost:5152/
curl.exe -s -D - -o NUL -H "Origin: https://evil.com" http://localhost:5152/api/Product
curl.exe http://localhost:5152/WeatherForecast
dotnet list package --vulnerable --include-transitive
```

---

## 8. Categorías del OWASP Top 10:2025 revisadas

| Categoría 2025 | Equivalente 2021 | ¿Aplica? | Hallazgos |
|---|---|---|---|
| A01 Broken Access Control | A01 (+ A10 SSRF) | Sí | H-01, H-02, H-09. SSRF no aplica: la API no hace peticiones salientes. |
| A02 Security Misconfiguration | A05 | Sí | H-05, H-08, H-10, H-11 |
| A03 Software Supply Chain Failures | A06 | Sí | H-04 |
| A04 Cryptographic Failures | A02 | Sí | H-05, H-06 |
| A05 Injection | A03 | Parcial | H-07. No se encontró inyección SQL ni XSS (sección 3). |
| A06 Insecure Design | A04 | Sí | H-03, H-15 |
| A07 Authentication Failures | A07 | Sí | H-01, H-14 |
| A08 Software or Data Integrity Failures | A08 | Sí | H-03, H-12 |
| A09 Security Logging and Alerting Failures | A09 | Sí | H-13 |
| A10 Mishandling of Exceptional Conditions | (nueva) | Sí | H-16 |

---

## 9. Dónde quedó cada corrección

Todas las rutas son relativas a `src/TiendaPromElec/`.

| Hallazgo | Archivos |
|---|---|
| H-01 Autenticación y autorización | `Program.cs` (Identity, JWT, políticas y *fallback*), `Controllers/AuthController.cs`, `Services/AuthService.cs`, `Services/TokenService.cs`, `[Authorize]` y `[AllowAnonymous]` en controladores y páginas |
| H-02 IDOR | `Controllers/OrderController.cs` (el cliente sale del token), `Services/OrderService.cs` (`GetOrderAsync` responde 403) |
| H-03 *Overposting* | `Dtos/ProductDtos.cs`, `Dtos/OrderDtos.cs`, `Services/ProductService.cs`, `Services/OrderService.cs` (`CreateOrderAsync` calcula el total) |
| H-04 Dependencias | `TiendaPromElec.csproj` (8.0.34), `../../.config/dotnet-tools.json` |
| H-05 Secretos | `appsettings.json` (sin secretos), `<UserSecretsId>` en el `.csproj`, `Security/JwtSettings.cs` (`ValidateOnStart`), `Program.cs` (error si falta la cadena de conexión), `Data/DesignTimeDbContextFactory.cs` |
| H-06 HTTPS | `Program.cs` (`AddHsts`, `UseHsts`, `UseHttpsRedirection`), `Properties/launchSettings.json` (perfil https primero) |
| H-07 Validación | Atributos `[Required]`, `[StringLength]`, `[Range]` y `[Url]` en `Dtos/` |
| H-08 CORS | `Program.cs` (política `PromElecCors`), `Cors:AllowedOrigins` en `appsettings*.json` |
| H-09 CSRF | `Program.cs` (`AddAntiforgery`, `CookiePolicyOptions`, `UseCookiePolicy`), `Pages/Contacto.cshtml` |
| H-10 Cabeceras | `Infrastructure/SecurityHeadersMiddleware.cs`, `Program.cs` (`AddServerHeader = false`) |
| H-11 Superficie | `WeatherForecast` eliminado, `AllowedHosts` en `appsettings.json`, paquetes de *scaffolding* eliminados |
| H-12 SRI | `Pages/Shared/_Layout.cshtml` (`integrity`), `wwwroot/css/site.css` (sin estilos en línea, por la CSP) |
| H-13 Registro de eventos | `Program.cs` (`JwtBearerEvents` para 401 y 403), `Services/AuthService.cs`, controladores, `Infrastructure/GlobalExceptionHandler.cs` |
| H-14 Fuerza bruta | `Program.cs` (contraseñas, *lockout*, `AddRateLimiter`), `[EnableRateLimiting]` en `AuthController` |
| H-15 Historial | `Data/AppDbContext.cs` (`OnDelete(Restrict)`), migración `AgregarIdentityYProteccionHistorial`, `ProductService.DeleteAsync` (409) |
| H-16 Excepciones | `Infrastructure/GlobalExceptionHandler.cs`, `Exceptions/AppExceptions.cs`, `Program.cs` (`AddExceptionHandler`, `UseExceptionHandler`) |

---

## 10. Verificación de las correcciones (antes y después)

Pruebas manuales hechas el 25/11/2026 contra la API corregida (`https://localhost:7299`, entorno Development) con `curl`, más una revisión en el navegador. Las pruebas automatizadas de las Fases 4 y 5 repiten estos casos.

| Prueba | Antes (original) | Después (corregido) | Hallazgo |
|---|---|---|---|
| Petición por `http://` | Se atendía por HTTP | **307** redirección a HTTPS | H-06 |
| `GET /api/Product` sin token | 200 | 200 (el catálogo sigue siendo público) | — |
| `GET /api/Order` sin token | **200 con todos los pedidos** | **401** | H-01, H-02 |
| `DELETE /api/Product/999` sin token | **404** (sin pedir credenciales) | **401** | H-01 |
| Token inválido | (no había tokens) | **401** | H-01 |
| Login del Admin / contraseña incorrecta | (no había login) | 200 con token / **401** | H-01, H-14 |
| `POST /api/Product` con precio negativo (Admin) | 400 por Category | **400** por el precio | H-07 |
| `POST` y `PUT` de producto válidos (Admin) | **400 siempre** | 201 / 204 | H-03 |
| `DELETE` de un producto con pedidos (Admin) | Borraba el historial en cascada | **409 Conflict** | H-15 |
| Registro con contraseña débil / correo repetido | (no había registro) | **400** / **409** | H-14 |
| Cliente crea un pedido con `totalAmount: 1` | 400 siempre | 201 con el total **calculado por el servidor** (2400.00) y estado Pendiente | H-03 |
| Pedido con más piezas que las existencias | (no se validaba) | **400** | H-03 |
| Cliente intenta crear o borrar productos | Cualquiera podía | **403** | H-01 |
| Cliente pide un pedido de otro cliente | Cualquiera podía | **403** | H-02 |
| Cabeceras de seguridad | Ninguna; `Server: Kestrel` | `X-Content-Type-Options`, `X-Frame-Options`, CSP, `Referrer-Policy`, `Permissions-Policy`; sin `Server` | H-10 |
| `GET /WeatherForecast` | 200 | 404 con token / 401 sin token | H-11 |
| CORS desde `https://evil.com` | Sin cabeceras CORS | Sin cabeceras CORS | H-08 |
| CORS desde un origen permitido | (sin política) | `Access-Control-Allow-Origin` presente | H-08 |
| `POST /Contacto` sin / con token antiforgery | (no existía) | **400** / 200; cookie con `SameSite=Strict` | H-09 |
| Bootstrap con SRI y CSP en el navegador | Sin SRI ni CSP | Carga correcta y sin errores de CSP en la consola | H-10, H-12 |
| `dotnet list package --vulnerable` | **12 paquetes vulnerables** | **0** | H-04 |
| `dotnet build` | 9 advertencias CS8618 | **0 advertencias** | H-03 |
| Log al rechazar una petición | Sin registros de seguridad | Registra cada 401, 403, login y cambio del Admin, sin contraseñas ni tokens | H-13 |
| Error de negocio (409) | 500 sin formato | ProblemDetails con el código correcto, sin trazas | H-16 |

---

## 11. Referencias

- [OWASP Top 10:2025](https://owasp.org/Top10/2025/)
- [OWASP Top 10:2021](https://owasp.org/Top10/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [Documentación de seguridad de ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/aspnet/core/security/)
