using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TiendaPromElec.Security;

namespace TiendaPromElec.UnitTests.Helpers;

/// <summary>
/// Crea el contexto HTTP de un controlador con un usuario "simulado".
/// En una prueba unitaria no hay token real: se construyen directamente los mismos claims
/// que pondría el token JWT (correo, rol y, si es Cliente, su id de cliente).
/// </summary>
public static class UsuarioDePrueba
{
    public static ControllerContext Admin() =>
        Contexto(new Claim(ClaimNames.Email, "admin@promelec.local"), new Claim(ClaimNames.Role, Roles.Admin));

    public static ControllerContext Cliente(long customerId) =>
        Contexto(
            new Claim(ClaimNames.Email, $"cliente{customerId}@promelec.test"),
            new Claim(ClaimNames.Role, Roles.Cliente),
            new Claim(ClaimNames.CustomerId, customerId.ToString()));

    private static ControllerContext Contexto(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "Prueba", nameType: ClaimNames.Email, roleType: ClaimNames.Role);
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }
}
