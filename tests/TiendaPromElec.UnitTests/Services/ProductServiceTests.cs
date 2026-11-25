using Moq;
using ProductApi.Models;
using TiendaPromElec.Dtos;
using TiendaPromElec.Exceptions;
using TiendaPromElec.Repositories;
using TiendaPromElec.Services;

namespace TiendaPromElec.UnitTests.Services;

/// <summary>
/// Pruebas unitarias de ProductService.
/// El repositorio IProductRepository se SIMULA con Moq: no se toca la base de datos.
/// </summary>
public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repositorio = new();
    private readonly ProductService _servicio;

    public ProductServiceTests()
    {
        _servicio = new ProductService(_repositorio.Object);
    }

    [Fact(DisplayName = "Positiva 4 · CreateAsync guarda solo los campos del DTO y asigna una imagen por defecto")]
    [Trait("Tipo", "Positiva")]
    public async Task CreateAsync_CategoriaExistente_GuardaProductoConLosDatosDelDto()
    {
        // Arrange
        _repositorio.Setup(r => r.CategoryExistsAsync(3)).ReturnsAsync(true);
        Product? guardado = null;
        _repositorio.Setup(r => r.AddAsync(It.IsAny<Product>()))
            .Callback<Product>(p => { p.Id = 11; guardado = p; })   // el repositorio simulado "asigna" el id
            .Returns(Task.CompletedTask);

        var entrada = new ProductInputDto
        {
            Name = "  Audífonos PromElec  ",
            Description = "Audífonos inalámbricos",
            Brand = "PromElec",
            Price = 899.90m,
            Stock = 25,
            ImageUrl = null,
            CategoryId = 3
        };

        // Act
        var resultado = await _servicio.CreateAsync(entrada);

        // Assert
        Assert.NotNull(guardado);
        Assert.Equal("Audífonos PromElec", guardado!.Name);              // se quitan espacios sobrantes
        Assert.Equal(899.90m, guardado.Price);
        Assert.Equal(3, guardado.CategoryId);
        Assert.Equal("https://via.placeholder.com/300", guardado.ImageUrl); // imagen por defecto
        Assert.Equal(11, resultado.Id);
        _repositorio.Verify(r => r.AddAsync(It.IsAny<Product>()), Times.Once);
    }

    [Fact(DisplayName = "Negativa 3 · DeleteAsync de un producto con pedidos lanza ConflictException y no borra (H-15)")]
    [Trait("Tipo", "Negativa")]
    public async Task DeleteAsync_ProductoConPedidos_LanzaConflictExceptionYNoBorra()
    {
        // Arrange: el producto 2 existe y aparece en pedidos.
        var producto = new Product { Id = 2, Name = "iPhone 16 Pro", Description = "El flagship de Apple", Brand = "Apple", Price = 28999.50m, Stock = 40, CategoryId = 1 };
        _repositorio.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(producto);
        _repositorio.Setup(r => r.HasOrderDetailsAsync(2)).ReturnsAsync(true);

        // Act + Assert
        var error = await Assert.ThrowsAsync<ConflictException>(() => _servicio.DeleteAsync(2));
        Assert.Contains("aparece en pedidos", error.Message);
        _repositorio.Verify(r => r.DeleteAsync(It.IsAny<Product>()), Times.Never);
    }
}
