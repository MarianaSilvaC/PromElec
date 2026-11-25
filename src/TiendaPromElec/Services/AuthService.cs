using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductApi.Models;
using TiendaPromElec.Dtos;
using TiendaPromElec.Exceptions;
using TiendaPromElec.Security;

namespace TiendaPromElec.Services;

/// <summary>Registro e inicio de sesión.</summary>
public interface IAuthService
{
    /// <exception cref="ConflictException">El correo ya está registrado.</exception>
    /// <exception cref="BusinessRuleException">La contraseña no cumple la política.</exception>
    Task<AuthResponseDto> RegisterAsync(RegisterDto input);

    /// <returns><c>null</c> si las credenciales no son válidas o la cuenta está bloqueada.</returns>
    Task<AuthResponseDto?> LoginAsync(LoginDto input);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITokenService tokens,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto input)
    {
        var email = input.Email.Trim().ToLowerInvariant();

        if (await _userManager.FindByEmailAsync(email) is not null
            || await _db.Customers.AnyAsync(c => c.Email == email))
        {
            throw new ConflictException("El correo ya está registrado.");
        }

        // El cliente y su usuario se crean en una sola transacción: o se guardan ambos o ninguno.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var customer = new Customer
        {
            FullName = input.FullName.Trim(),
            Email = email,
            Phone = input.Phone.Trim(),
            Address = input.Address.Trim()
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser { UserName = email, Email = email, CustomerId = customer.Id };

        // UserManager guarda la contraseña como hash y aplica la política de contraseñas (H-14).
        var result = await _userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            var errors = new Dictionary<string, string[]>
            {
                ["Password"] = result.Errors.Select(e => e.Description).ToArray()
            };
            _logger.LogInformation("Registro rechazado por la política de contraseñas.");
            throw new BusinessRuleException("La contraseña no cumple la política de seguridad.", errors);
        }

        await _userManager.AddToRoleAsync(user, Roles.Cliente);
        await transaction.CommitAsync();

        // H-13: evento de seguridad (sin datos sensibles: nunca la contraseña).
        _logger.LogInformation("Nuevo Cliente registrado. UserId={UserId}, CustomerId={CustomerId}", user.Id, customer.Id);

        return _tokens.CreateToken(user, Roles.Cliente);
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto input)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);

        // H-14: el mensaje al usuario es siempre el mismo ("credenciales inválidas") para no revelar
        // si el correo existe. En el log sí se distingue el motivo.
        if (user is null)
        {
            _logger.LogWarning("Inicio de sesión fallido: correo no registrado ({Email}).", MaskEmail(email));
            return null;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Inicio de sesión bloqueado: la cuenta {UserId} está bloqueada temporalmente.", user.Id);
            return null;
        }

        if (!await _userManager.CheckPasswordAsync(user, input.Password))
        {
            // Suma un intento fallido; al llegar a 5, Identity bloquea la cuenta 15 minutos.
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning("Inicio de sesión fallido: contraseña incorrecta para {UserId}.", user.Id);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Cliente;
        _logger.LogInformation("Inicio de sesión exitoso. UserId={UserId}, Rol={Role}", user.Id, role);

        return _tokens.CreateToken(user, role);
    }

    /// <summary>Oculta parte del correo para no guardar datos personales completos en los logs.</summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
