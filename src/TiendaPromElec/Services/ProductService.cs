using ProductApi.Models;
using TiendaPromElec.Dtos;
using TiendaPromElec.Exceptions;
using TiendaPromElec.Repositories;

namespace TiendaPromElec.Services;

public class ProductService : IProductService
{
    private const string DefaultImageUrl = "https://via.placeholder.com/300";

    private readonly IProductRepository _products;

    public ProductService(IProductRepository products)
    {
        _products = products;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync()
    {
        var products = await _products.GetAllAsync();
        return products.Select(ToDto).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(long id)
    {
        var product = await _products.GetByIdAsync(id);
        return product is null ? null : ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(ProductInputDto input)
    {
        await EnsureCategoryExistsAsync(input.CategoryId);

        // H-03: se construye la entidad SOLO con los campos permitidos del DTO.
        var product = new Product
        {
            Name = input.Name.Trim(),
            Description = input.Description.Trim(),
            Brand = input.Brand.Trim(),
            Price = input.Price,
            Stock = input.Stock,
            ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? DefaultImageUrl : input.ImageUrl.Trim(),
            CategoryId = input.CategoryId
        };

        await _products.AddAsync(product);
        return ToDto(product);
    }

    public async Task<bool> UpdateAsync(long id, ProductInputDto input)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null)
        {
            return false;
        }

        await EnsureCategoryExistsAsync(input.CategoryId);

        product.Name = input.Name.Trim();
        product.Description = input.Description.Trim();
        product.Brand = input.Brand.Trim();
        product.Price = input.Price;
        product.Stock = input.Stock;
        product.ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? product.ImageUrl : input.ImageUrl.Trim();
        product.CategoryId = input.CategoryId;

        await _products.UpdateAsync(product);
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null)
        {
            return false;
        }

        // H-15: si el producto aparece en pedidos, NO se borra (antes se borraba el historial en cascada).
        if (await _products.HasOrderDetailsAsync(id))
        {
            throw new ConflictException(
                $"No se puede eliminar el producto {id} porque aparece en pedidos registrados. " +
                "Cambia sus existencias a 0 para dejar de venderlo.");
        }

        await _products.DeleteAsync(product);
        return true;
    }

    private async Task EnsureCategoryExistsAsync(long categoryId)
    {
        if (!await _products.CategoryExistsAsync(categoryId))
        {
            throw new BusinessRuleException($"La categoría {categoryId} no existe.");
        }
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Brand, p.Price, p.Stock, p.ImageUrl, p.CategoryId, p.Category?.Name);
}
