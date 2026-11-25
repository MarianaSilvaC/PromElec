using System.ComponentModel.DataAnnotations;

namespace TiendaPromElec.Security;

/// <summary>
/// Configuración del token JWT (sección "Jwt").
/// H-05: <see cref="Key"/> es un SECRETO. No va en appsettings.json: se configura con
/// dotnet user-secrets (desarrollo) o con la variable de entorno Jwt__Key (Docker).
/// Si falta o es muy corta, la aplicación no arranca (ValidateOnStart en Program.cs).
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Falta Jwt:Issuer.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta Jwt:Audience.")]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta el secreto Jwt:Key (configúralo con dotnet user-secrets o con la variable Jwt__Key).")]
    [MinLength(32, ErrorMessage = "Jwt:Key debe tener al menos 32 caracteres (256 bits).")]
    public string Key { get; set; } = string.Empty;

    [Range(5, 1440, ErrorMessage = "Jwt:ExpiresMinutes debe estar entre 5 y 1440.")]
    public int ExpiresMinutes { get; set; } = 60;
}
