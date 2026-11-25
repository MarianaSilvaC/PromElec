using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TiendaPromElec.Dtos;
using TiendaPromElec.Services;

namespace TiendaPromElec.Pages
{
    /// <summary>Catálogo público. [AllowAnonymous] es necesario por la política "denegar por defecto" (H-01).</summary>
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly IProductService _productService;

        public IndexModel(IProductService productService)
        {
            _productService = productService;
        }

        public IReadOnlyList<ProductDto> Products { get; private set; } = Array.Empty<ProductDto>();

        public async Task OnGetAsync()
        {
            Products = await _productService.GetAllAsync();
        }
    }
}
