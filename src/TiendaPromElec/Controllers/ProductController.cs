using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TiendaPromElec.Dtos;
using TiendaPromElec.Security;
using TiendaPromElec.Services;

namespace TiendaPromElec.Controllers
{
    /// <summary>
    /// Catálogo de productos.
    /// Consultar es público; crear, modificar y borrar es solo para el rol Admin (H-01).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IProductService productService, ILogger<ProductController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        /// <summary>Lista todos los productos (público).</summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
        {
            return Ok(await _productService.GetAllAsync());
        }

        /// <summary>Consulta un producto por su id (público).</summary>
        [HttpGet("{id:long}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductDto>> GetProduct(long id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product is null)
            {
                _logger.LogInformation("Producto con ID {ProductoId} no encontrado.", id);
                return NotFound();
            }

            return Ok(product);
        }

        /// <summary>Crea un producto (solo Admin).</summary>
        [HttpPost]
        [Authorize(Policy = Policies.SoloAdmin)]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ProductDto>> PostProduct(ProductInputDto input)
        {
            // [ApiController] ya valida el modelo; esta comprobación también protege al controlador
            // cuando se usa fuera del pipeline de ASP.NET (por ejemplo, en pruebas unitarias).
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var created = await _productService.CreateAsync(input);

            // H-13: registro de acciones administrativas.
            _logger.LogInformation("Admin {User} creó el producto {ProductoId}.", User.Identity?.Name, created.Id);
            return CreatedAtAction(nameof(GetProduct), new { id = created.Id }, created);
        }

        /// <summary>Actualiza un producto (solo Admin).</summary>
        [HttpPut("{id:long}")]
        [Authorize(Policy = Policies.SoloAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PutProduct(long id, ProductInputDto input)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _productService.UpdateAsync(id, input))
            {
                return NotFound();
            }

            _logger.LogInformation("Admin {User} actualizó el producto {ProductoId}.", User.Identity?.Name, id);
            return NoContent();
        }

        /// <summary>Elimina un producto (solo Admin). Responde 409 si el producto tiene pedidos (H-15).</summary>
        [HttpDelete("{id:long}")]
        [Authorize(Policy = Policies.SoloAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteProduct(long id)
        {
            if (!await _productService.DeleteAsync(id))
            {
                return NotFound();
            }

            _logger.LogWarning("Admin {User} eliminó el producto {ProductoId}.", User.Identity?.Name, id);
            return NoContent();
        }
    }
}
