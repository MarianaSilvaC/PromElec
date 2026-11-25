using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TiendaPromElec.Controllers;
using TiendaPromElec.Dtos;
using TiendaPromElec.Services;
using TiendaPromElec.UnitTests.Helpers;

namespace TiendaPromElec.UnitTests.Controllers;

/// <summary>
/// Pruebas unitarias de ProductController.
/// El servicio IProductService se SIMULA con Moq: así se prueba solo la lógica del controlador,
/// sin base de datos ni servidor web.
/// </summary>
public class ProductControllerTests
{
    private readonly Mock<IProductService> _servicio = new();
    private readonly ProductController _controlador;

    public ProductControllerTests()
    {
        _controlador = new ProductController(_servicio.Object, NullLogger<ProductController>.Instance)
        {
            ControllerContext = UsuarioDePrueba.Admin()
        };
    }

    private static ProductDto IPhone() =>
        new(2, "iPhone 16 Pro", "El flagship de Apple", "Apple", 28999.50m, 40, "https://via.placeholder.com/300", 1, "Smartphones");

    private static ProductInputDto ProductoNuevoValido() => new()
    {
        Name = "Audífonos PromElec",
        Description = "Audífonos inalámbricos",
        Brand = "PromElec",
        Price = 899.90m,
        Stock = 25,
        CategoryId = 3
    };

    [Fact(DisplayName = "Positiva 1 · GetProduct con un id existente responde 200 con el producto")]
    [Trait("Tipo", "Positiva")]
    public async Task GetProduct_IdExistente_Devuelve200ConElProducto()
    {
        // Arrange: el servicio simulado "encuentra" el producto 2.
        _servicio.Setup(s => s.GetByIdAsync(2)).ReturnsAsync(IPhone());

        // Act
        var resultado = await _controlador.GetProduct(2);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var producto = Assert.IsType<ProductDto>(ok.Value);
        Assert.Equal("iPhone 16 Pro", producto.Name);
        Assert.Equal(28999.50m, producto.Price);
    }

    [Fact(DisplayName = "Positiva 2 · PostProduct con datos válidos responde 201 y apunta al nuevo producto")]
    [Trait("Tipo", "Positiva")]
    public async Task PostProduct_DatosValidos_Devuelve201()
    {
        var entrada = ProductoNuevoValido();
        var creado = new ProductDto(11, entrada.Name, entrada.Description, entrada.Brand, entrada.Price, entrada.Stock,
            "https://via.placeholder.com/300", entrada.CategoryId, "Accesorios");
        _servicio.Setup(s => s.CreateAsync(entrada)).ReturnsAsync(creado);

        var resultado = await _controlador.PostProduct(entrada);

        var created = Assert.IsType<CreatedAtActionResult>(resultado.Result);
        Assert.Equal(nameof(ProductController.GetProduct), created.ActionName);
        Assert.Equal(11L, created.RouteValues!["id"]);
        Assert.Same(creado, created.Value);
        _servicio.Verify(s => s.CreateAsync(entrada), Times.Once);
    }

    [Fact(DisplayName = "Negativa 1 · GetProduct con un id inexistente responde 404")]
    [Trait("Tipo", "Negativa")]
    public async Task GetProduct_IdInexistente_Devuelve404()
    {
        // El servicio simulado no encuentra el producto (devuelve null).
        _servicio.Setup(s => s.GetByIdAsync(999)).ReturnsAsync((ProductDto?)null);

        var resultado = await _controlador.GetProduct(999);

        Assert.IsType<NotFoundResult>(resultado.Result);
    }

    [Fact(DisplayName = "Negativa 2 · PostProduct con un modelo inválido responde 400 y no llama al servicio")]
    [Trait("Tipo", "Negativa")]
    public async Task PostProduct_ModeloInvalido_Devuelve400YNoGuarda()
    {
        var entrada = ProductoNuevoValido();
        entrada.Price = -100;
        // En una prueba unitaria la validación automática de ASP.NET no corre,
        // así que se simula el error que habría producido el atributo [Range].
        _controlador.ModelState.AddModelError(nameof(ProductInputDto.Price), "El precio debe estar entre 0.01 y 1,000,000.");

        var resultado = await _controlador.PostProduct(entrada);

        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
        var errores = Assert.IsType<SerializableError>(badRequest.Value);
        Assert.True(errores.ContainsKey(nameof(ProductInputDto.Price)));
        _servicio.Verify(s => s.CreateAsync(It.IsAny<ProductInputDto>()), Times.Never);
    }
}
