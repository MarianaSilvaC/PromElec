using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TiendaPromElec.Dtos;
using TiendaPromElec.Security;
using TiendaPromElec.Services;

namespace TiendaPromElec.Controllers
{
    /// <summary>
    /// Registro, inicio de sesión y perfil (H-01).
    /// H-14: tiene límite de peticiones por IP para frenar ataques de fuerza bruta (429 Too Many Requests).
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>Crea una cuenta con rol Cliente y devuelve su token.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto input)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.RegisterAsync(input);
            return Created("/api/auth/me", response);
        }

        /// <summary>Inicia sesión y devuelve un token JWT válido por 60 minutos.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto input)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.LoginAsync(input);
            if (response is null)
            {
                _logger.LogWarning("Credenciales inválidas desde la IP {Ip}.", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Credenciales inválidas",
                    Detail = "El correo o la contraseña no son correctos, o la cuenta está bloqueada temporalmente."
                });
            }

            return Ok(response);
        }

        /// <summary>Devuelve los datos del usuario autenticado (recurso protegido).</summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<UserProfileDto> Me()
        {
            var customerClaim = User.FindFirst(ClaimNames.CustomerId)?.Value;
            long? customerId = long.TryParse(customerClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

            return Ok(new UserProfileDto(
                User.FindFirst(ClaimNames.Subject)?.Value ?? string.Empty,
                User.FindFirst(ClaimNames.Email)?.Value ?? string.Empty,
                User.FindFirst(ClaimNames.Role)?.Value ?? string.Empty,
                customerId));
        }
    }
}
