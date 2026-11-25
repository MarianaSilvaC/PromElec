using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductApi.Models;
using TiendaPromElec.Security;

namespace TiendaPromElec.Infrastructure;

/// <summary>
/// Prepara la base de datos al arrancar:
/// 1. Aplica las migraciones pendientes (SQL Server) o crea el esquema con los datos semilla
///    (SQLite: pruebas de integración y servidores con poca memoria).
/// 2. Crea los roles Admin y Cliente.
/// 3. Crea el usuario Admin inicial con la contraseña del secreto SeedAdmin:Password (H-05).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PromElec.BaseDeDatos");
        var configuration = services.GetRequiredService<IConfiguration>();

        // Si falta la cadena de conexión, aquí se lanza el error y la API no arranca (H-05).
        var db = services.GetRequiredService<AppDbContext>();

        if (db.Database.IsSqlServer())
        {
            if (configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Migraciones aplicadas.");
            }
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "No se creó el usuario Admin inicial: falta SeedAdmin:Email o el secreto SeedAdmin:Password.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo crear el usuario Admin inicial: " +
                string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Usuario Admin inicial creado ({Email}).", adminEmail);
    }
}
