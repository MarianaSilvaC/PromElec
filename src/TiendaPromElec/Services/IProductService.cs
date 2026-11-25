using TiendaPromElec.Dtos;

namespace TiendaPromElec.Services;

/// <summary>Reglas de negocio del catálogo. Los controladores dependen de esta interfaz.</summary>
public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(long id);
    Task<ProductDto> CreateAsync(ProductInputDto input);

    /// <returns><c>false</c> si el producto no existe.</returns>
    Task<bool> UpdateAsync(long id, ProductInputDto input);

    /// <returns><c>false</c> si el producto no existe.</returns>
    Task<bool> DeleteAsync(long id);
}
