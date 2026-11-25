namespace TiendaPromElec.Infrastructure;

/// <summary>
/// H-10: agrega cabeceras de seguridad a TODAS las respuestas.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    // Política de contenido: solo se cargan recursos del propio sitio, más la hoja de estilos
    // de Bootstrap desde jsDelivr (verificada con SRI, H-12) e imágenes por https.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "img-src 'self' https: data:; " +
        "style-src 'self' https://cdn.jsdelivr.net; " +
        "script-src 'self'; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Evita que el navegador "adivine" el tipo de contenido (MIME sniffing).
            headers["X-Content-Type-Options"] = "nosniff";
            // Impide incrustar el sitio en un iframe ajeno (clickjacking).
            headers["X-Frame-Options"] = "DENY";
            // No envía la URL de origen a otros sitios.
            headers["Referrer-Policy"] = "no-referrer";
            // Desactiva APIs del navegador que la tienda no usa.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // Swagger UI (solo en desarrollo) usa scripts en línea, así que se excluye de la CSP.
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers["Content-Security-Policy"] = ContentSecurityPolicy;
            }

            // Defensa adicional: no revelar la tecnología del servidor (también se desactiva en Kestrel).
            headers.Remove("Server");
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
