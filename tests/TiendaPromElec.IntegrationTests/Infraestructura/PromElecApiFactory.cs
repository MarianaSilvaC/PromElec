using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductApi.Models;

namespace TiendaPromElec.IntegrationTests.Infraestructura;

/// <summary>
/// Levanta la API COMPLETA en memoria (WebApplicationFactory&lt;Program&gt;): el mismo Program.cs,
/// con autenticación JWT, políticas, CORS, antiforgery, cabeceras de seguridad, etc.
/// Solo cambian tres cosas para las pruebas:
///   1. La base de datos es SQLite en memoria (se crea desde cero con los datos semilla: 10 productos, 2 clientes, 5 pedidos).
///   2. Los secretos (llave JWT y contraseña del Admin) son valores de prueba en memoria.
///   3. El límite de peticiones de /api/auth se sube, para que las pruebas no reciban 429.
/// </summary>
public class PromElecApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@promelec.local";
    public const string AdminPassword = "Admin#Prueba2026";
    public const string JwtKey = "clave-solo-para-pruebas-de-integracion-promelec-2026";
    public const string OrigenPermitido = "https://tienda.promelec.local";

    // La base en memoria vive mientras la conexión esté abierta.
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public PromElecApiFactory()
    {
        _conexion.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Entorno "Testing": no se cargan los user-secrets ni la configuración de desarrollo.
        builder.UseEnvironment("Testing");

        // Puerto HTTPS conocido, para que UseHttpsRedirection pueda redirigir http -> https.
        builder.UseSetting("https_port", "443");

        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["SeedAdmin:Email"] = AdminEmail,
                ["SeedAdmin:Password"] = AdminPassword,
                ["RateLimiting:AuthPermitLimit"] = "1000"
            });
        });

        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureTestServices(services =>
        {
            // Se quita el registro de SQL Server y se usa SQLite en memoria.
            var registroSqlServer = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(registroSqlServer);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_conexion));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _conexion.Dispose();
        }
    }
}
