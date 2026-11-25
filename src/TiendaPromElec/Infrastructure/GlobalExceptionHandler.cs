using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TiendaPromElec.Exceptions;

namespace TiendaPromElec.Infrastructure;

/// <summary>
/// H-16: manejador global de excepciones.
/// Convierte las excepciones de negocio en respuestas ProblemDetails (RFC 9457) con el código HTTP correcto
/// y, ante un error inesperado, responde 500 SIN revelar detalles internos (trazas, SQL, rutas).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Solicitud no válida"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Acceso denegado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor")
        };

        var path = httpContext.Request.Path;
        var user = httpContext.User.Identity?.Name ?? "anónimo";

        // H-13: cada tipo de error se registra con su nivel adecuado.
        switch (status)
        {
            case StatusCodes.Status500InternalServerError:
                _logger.LogError(exception, "Error no controlado en {Method} {Path}.", httpContext.Request.Method, path);
                break;
            case StatusCodes.Status403Forbidden:
                _logger.LogWarning("Acceso denegado a {Path} para {User}: {Motivo}", path, user, exception.Message);
                break;
            default:
                _logger.LogInformation("Solicitud rechazada ({Status}) en {Path}: {Motivo}", status, path, exception.Message);
                break;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // En un 500 nunca se envía el mensaje real de la excepción al cliente.
            Detail = status == StatusCodes.Status500InternalServerError
                ? "Ocurrió un error inesperado. Intenta de nuevo más tarde."
                : exception.Message,
            Instance = path
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is BusinessRuleException { Errors: not null } businessError)
        {
            problem.Extensions["errors"] = businessError.Errors;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
