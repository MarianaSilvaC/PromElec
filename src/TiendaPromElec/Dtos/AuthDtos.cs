using System.ComponentModel.DataAnnotations;

namespace TiendaPromElec.Dtos;

/// <summary>Datos para registrar una cuenta de Cliente.</summary>
public class RegisterDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(100, MinimumLength = 3)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    // La política completa (mayúscula, minúscula, número y símbolo) la aplica Identity (H-14).
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [Phone(ErrorMessage = "El teléfono no tiene un formato válido.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;
}

/// <summary>Credenciales para iniciar sesión.</summary>
public class LoginDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Respuesta de registro e inicio de sesión: el token JWT y su vencimiento.</summary>
public record AuthResponseDto(string Token, DateTime ExpiresAtUtc, string Email, string Role);

/// <summary>Datos del usuario autenticado, tomados de su token.</summary>
public record UserProfileDto(string UserId, string Email, string Role, long? CustomerId);
