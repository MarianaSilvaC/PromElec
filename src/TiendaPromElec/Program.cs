using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductApi.Models;
using TiendaPromElec.Infrastructure;
using TiendaPromElec.Repositories;
using TiendaPromElec.Security;
using TiendaPromElec.Services;

var builder = WebApplication.CreateBuilder(args);

// H-10: Kestrel ya no envía la cabecera "Server: Kestrel".
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// =====================================================================================
// Base de datos
// H-05: la cadena de conexión NO está en appsettings.json. Se lee de dotnet user-secrets
// (desarrollo) o de la variable de entorno ConnectionStrings__DefaultConnection (Docker).
// =====================================================================================
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'. " +
            "Configúrala con 'dotnet user-secrets set' (desarrollo) o con la variable de entorno " +
            "ConnectionStrings__DefaultConnection (Docker).");
    }

    options.UseSqlServer(connectionString);
});

// =====================================================================================
// H-01 y H-14: identidad de usuarios con ASP.NET Core Identity
// =====================================================================================
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // H-14: política de contraseñas.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // H-14: bloqueo de la cuenta tras 5 intentos fallidos, durante 15 minutos.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// =====================================================================================
// H-01: autenticación con JWT
// H-05: la llave de firma (Jwt:Key) es un secreto y la API no arranca si falta (ValidateOnStart).
// =====================================================================================
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration(JwtSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimNames.Email,
            RoleClaimType = ClaimNames.Role
        };

        // H-13: registrar los accesos rechazados (401 y 403).
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>().CreateLogger("PromElec.Seguridad");
                logger.LogWarning("401 No autenticado: {Method} {Path} desde {Ip}. Motivo: {Error}",
                    context.Request.Method, context.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress,
                    string.IsNullOrEmpty(context.Error) ? "sin token" : context.ErrorDescription ?? context.Error);
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>().CreateLogger("PromElec.Seguridad");
                logger.LogWarning("403 Sin permiso: {User} intentó {Method} {Path}.",
                    context.HttpContext.User.Identity?.Name, context.Request.Method, context.Request.Path);
                return Task.CompletedTask;
            }
        };
    });

// =====================================================================================
// H-01: políticas de autorización
// =====================================================================================
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.SoloAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin))
    .AddPolicy(Policies.SoloCliente, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Cliente))
    // Denegar por defecto: todo endpoint SIN [AllowAnonymous] exige un usuario autenticado.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// =====================================================================================
// H-14: límite de peticiones a /api/auth (contra fuerza bruta)
// =====================================================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
    {
        var permitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:AuthPermitLimit", 10);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// =====================================================================================
// H-08: CORS seguro con lista blanca de orígenes (configuración "Cors:AllowedOrigins")
// =====================================================================================
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        options.AddPolicy(CorsPolicies.PromElec, policy => policy
            .WithOrigins(allowedOrigins)                      // nunca AllowAnyOrigin()
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
    });

// =====================================================================================
// H-09: protección CSRF para las páginas Razor y cookies SameSite
// =====================================================================================
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".PromElec.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
});

// Llaves de Data Protection (firman los tokens antiforgery). En Docker se guardan en un volumen
// (DataProtection:KeysPath) para que sobrevivan a reinicios y las compartan varias instancias.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("PromElec.TiendaApi");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// =====================================================================================
// H-06: HSTS (el navegador recordará usar solo HTTPS durante un año)
// =====================================================================================
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// =====================================================================================
// H-16: manejo global de errores con ProblemDetails
// =====================================================================================
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// =====================================================================================
// Capas de la aplicación (inyección de dependencias)
// =====================================================================================
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

// Swagger con botón "Authorize" para probar los endpoints protegidos con un token JWT.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PromElec Tienda API",
        Version = "v1",
        Description = "API de artículos electrónicos promocionales de PromElec (versión segura)."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega el token que devuelve /api/auth/login (sin la palabra Bearer)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

var app = builder.Build();

// =====================================================================================
// Pipeline de peticiones (el orden importa)
// =====================================================================================
app.UseExceptionHandler();                       // H-16

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();                               // H-06
}

app.UseHttpsRedirection();                       // H-06: http -> https
app.UseMiddleware<SecurityHeadersMiddleware>();  // H-10

if (app.Environment.IsDevelopment())
{
    // Swagger solo en desarrollo (control que ya existía en el código original).
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseCookiePolicy();                           // H-09
app.UseRouting();
app.UseCors(CorsPolicies.PromElec);              // H-08
app.UseRateLimiter();                            // H-14
app.UseAuthentication();                         // H-01
app.UseAuthorization();                          // H-01

app.MapControllers();
app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();

await app.InitializeDatabaseAsync();

app.Run();

/// <summary>Necesario para que las pruebas de integración usen WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program { }
