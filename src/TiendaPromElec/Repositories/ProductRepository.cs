using Microsoft.EntityFrameworkCore;
using ProductApi.Models;

namespace TiendaPromElec.Repositories;

/// <summary>Implementación con Entity Framework Core (consultas parametrizadas, sin SQL crudo).</summary>
public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Id)
            .ToListAsync();

    public Task<Product?> GetByIdAsync(long id) =>
        _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<long> ids)
    {
        var idList = ids.Distinct().ToList();
        return await _context.Products.Where(p => idList.Contains(p.Id)).ToListAsync();
    }

    public Task<bool> CategoryExistsAsync(long categoryId) =>
        _context.Categories.AnyAsync(c => c.Id == categoryId);

    public Task<bool> HasOrderDetailsAsync(long productId) =>
        _context.OrderDetails.AnyAsync(od => od.ProductId == productId);

    public async Task AddAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        // Si el producto ya está rastreado (lo normal, porque se cargó con GetByIdAsync),
        // EF solo guarda las columnas que cambiaron.
        if (_context.Entry(product).State == EntityState.Detached)
        {
            _context.Products.Update(product);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Product product)
    {
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
    }
}
