using ProductApi.Models;

namespace TiendaPromElec.Repositories;

/// <summary>
/// Acceso a datos de productos. Los servicios dependen de esta interfaz y no del DbContext,
/// así que en las pruebas unitarias se puede simular con Moq.
/// </summary>
public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(long id);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<long> ids);
    Task<bool> CategoryExistsAsync(long categoryId);
    Task<bool> HasOrderDetailsAsync(long productId);
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(Product product);
}
