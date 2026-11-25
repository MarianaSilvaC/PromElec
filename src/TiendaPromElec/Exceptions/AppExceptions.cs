namespace TiendaPromElec.Exceptions;

/// <summary>
/// Excepciones de negocio. Los servicios las lanzan y el manejador global
/// (Infrastructure/GlobalExceptionHandler) las convierte en respuestas HTTP claras (H-16).
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

/// <summary>Se traduce a 404 Not Found.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Se traduce a 409 Conflict (por ejemplo, borrar un producto que tiene pedidos).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Se traduce a 400 Bad Request (por ejemplo, existencias insuficientes).</summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, IDictionary<string, string[]>? errors = null) : base(message)
    {
        Errors = errors;
    }

    public IDictionary<string, string[]>? Errors { get; }
}

/// <summary>Se traduce a 403 Forbidden (por ejemplo, ver el pedido de otro cliente).</summary>
public sealed class ForbiddenAccessException : AppException
{
    public ForbiddenAccessException(string message) : base(message) { }
}
