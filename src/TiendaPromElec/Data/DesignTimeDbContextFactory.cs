using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProductApi.Models;

/// <summary>
/// Solo la usa la herramienta "dotnet ef migrations add" para generar migraciones.
/// Generar una migración no se conecta a la base de datos, así que aquí no hace falta
/// ninguna cadena de conexión (H-05: no se escribe ninguna en el código).
/// Las migraciones se aplican solas al arrancar la API (Infrastructure/DatabaseInitializer).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer()
            .Options;

        return new AppDbContext(options);
    }
}
