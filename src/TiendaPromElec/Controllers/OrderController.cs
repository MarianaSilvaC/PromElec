using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TiendaPromElec.Dtos;
using TiendaPromElec.Security;
using TiendaPromElec.Services;

namespace TiendaPromElec.Controllers
{
    /// <summary>
    /// Pedidos. Todo el controlador exige iniciar sesión (H-01).
    /// Un Cliente solo ve y crea SUS pedidos (H-02); el Admin ve todos, cambia estados y elimina.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        /// <summary>Lista pedidos: el Admin ve todos y el Cliente solo los suyos.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
        {
            if (!TryGetScope(out var customerId))
            {
                return Forbid();
            }

            return Ok(await _orderService.GetOrdersAsync(customerId));
        }

        /// <summary>Consulta un pedido. Un Cliente que pide un pedido ajeno recibe 403.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderDto>> GetOrder(long id)
        {
            if (!TryGetScope(out var customerId))
            {
                return Forbid();
            }

            var order = await _orderService.GetOrderAsync(id, customerId);
            return order is null ? NotFound() : Ok(order);
        }

        /// <summary>Crea un pedido para el Cliente que inició sesión. El total lo calcula el servidor.</summary>
        [HttpPost]
        [Authorize(Policy = Policies.SoloCliente)]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<OrderDto>> PostOrder(OrderCreateDto input)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customerId = GetCustomerId();
            if (customerId is null)
            {
                return Forbid();
            }

            var created = await _orderService.CreateOrderAsync(customerId.Value, input);
            _logger.LogInformation("Cliente {CustomerId} creó el pedido {OrderId} por {Total}.", customerId, created.Id, created.TotalAmount);
            return CreatedAtAction(nameof(GetOrder), new { id = created.Id }, created);
        }

        /// <summary>Cambia el estado de un pedido (solo Admin).</summary>
        [HttpPut("{id:long}/status")]
        [Authorize(Policy = Policies.SoloAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PutOrderStatus(long id, OrderStatusUpdateDto input)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _orderService.UpdateStatusAsync(id, input.Status))
            {
                return NotFound();
            }

            _logger.LogInformation("Admin {User} cambió el pedido {OrderId} a {Status}.", User.Identity?.Name, id, input.Status);
            return NoContent();
        }

        /// <summary>Elimina un pedido (solo Admin).</summary>
        [HttpDelete("{id:long}")]
        [Authorize(Policy = Policies.SoloAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteOrder(long id)
        {
            if (!await _orderService.DeleteOrderAsync(id))
            {
                return NotFound();
            }

            _logger.LogWarning("Admin {User} eliminó el pedido {OrderId}.", User.Identity?.Name, id);
            return NoContent();
        }

        /// <summary>
        /// Define qué pedidos puede ver el usuario: Admin = todos (null); Cliente = su id de cliente.
        /// Devuelve false si el usuario no es Admin y su token no trae id de cliente.
        /// </summary>
        private bool TryGetScope(out long? customerId)
        {
            if (User.IsInRole(Roles.Admin))
            {
                customerId = null;
                return true;
            }

            customerId = GetCustomerId();
            return customerId is not null;
        }

        /// <summary>H-02: el id de cliente SIEMPRE se toma del token firmado, nunca de la petición.</summary>
        private long? GetCustomerId()
        {
            var value = User.FindFirst(ClaimNames.CustomerId)?.Value;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }
}
