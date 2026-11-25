namespace TiendaPromElec.Security;

/// <summary>Roles del sistema (H-01).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Cliente = "Cliente";

    public static readonly string[] All = { Admin, Cliente };
}

/// <summary>Políticas de autorización con nombre (H-01).</summary>
public static class Policies
{
    public const string SoloAdmin = "SoloAdmin";
    public const string SoloCliente = "SoloCliente";
}

/// <summary>Nombres de los datos (claims) que lleva el token JWT.</summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string CustomerId = "customer_id";
}

/// <summary>Nombre de la política CORS (H-08).</summary>
public static class CorsPolicies
{
    public const string PromElec = "PromElecCors";
}

/// <summary>Nombre de la política de límite de peticiones (H-14).</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
