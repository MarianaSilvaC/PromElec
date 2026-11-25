using Moq;
using ProductApi.Models;
using TiendaPromElec.Dtos;
using TiendaPromElec.Exceptions;
using TiendaPromElec.Repositories;
using TiendaPromElec.Services;

namespace TiendaPromElec.UnitTests.Services;

/// <summary>
/// Pruebas unitarias de OrderService.
/// Se simulan con Moq los dos repositorios (pedidos y productos) y también el reloj (TimeProvider),
/// para que la fecha del pedido sea siempre la misma y la prueba sea repetible.
/// </summary>
public class OrderServiceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IOrderRepository> _pedidos = new();
    private readonly Mock<IProductRepository> _productos = new();
    private readonly OrderService _servicio;

    public OrderServiceTests()
    {
        var reloj = new Mock<TimeProvider>();
        reloj.Setup(t => t.GetUtcNow()).Returns(Ahora);
        _servicio = new OrderService(_pedidos.Object, _productos.Object, reloj.Object);
    }

    private static Product Cargador() =>
        new() { Id = 9, Name = "Cargador Anker 100W", Description = "Carga rápida", Brand = "Anker", Price = 1200.00m, Stock = 200, CategoryId = 3 };

    private static Product Mouse() =>
        new() { Id = 10, Name = "Mouse Logitech MX Master 4", Description = "Productividad", Brand = "Logitech", Price = 2400.00m, Stock = 80, CategoryId = 3 };

    [Fact(DisplayName = "Positiva 5 · CreateOrderAsync calcula el total en el servidor y descuenta existencias (H-03)")]
    [Trait("Tipo", "Positiva")]
    public async Task CreateOrderAsync_CalculaTotalEnServidorYDescuentaExistencias()
    {
        // Arrange
        var cargador = Cargador();
        var mouse = Mouse();
        _productos.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<long>>()))
            .ReturnsAsync(new List<Product> { cargador, mouse });

        var entrada = new OrderCreateDto
        {
            Items =
            {
                new OrderItemInputDto { ProductId = 9, Quantity = 2 },
                new OrderItemInputDto { ProductId = 10, Quantity = 1 }
            }
        };

        // Act
        var pedido = await _servicio.CreateOrderAsync(customerId: 7, entrada);

        // Assert: 2 x 1200 + 1 x 2400 = 4800, con precios tomados del "repositorio", no de la petición.
        Assert.Equal(4800.00m, pedido.TotalAmount);
        Assert.Equal("Pendiente", pedido.Status);
        Assert.Equal(7, pedido.CustomerId);
        Assert.Equal(Ahora.UtcDateTime, pedido.OrderDate);
        Assert.Equal(198, cargador.Stock);
        Assert.Equal(79, mouse.Stock);
        _pedidos.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact(DisplayName = "Negativa 4 · CreateOrderAsync sin existencias suficientes lanza BusinessRuleException y no guarda")]
    [Trait("Tipo", "Negativa")]
    public async Task CreateOrderAsync_ExistenciasInsuficientes_LanzaExcepcionYNoGuarda()
    {
        var cargador = Cargador();
        cargador.Stock = 1;
        _productos.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<long>>()))
            .ReturnsAsync(new List<Product> { cargador });

        var entrada = new OrderCreateDto { Items = { new OrderItemInputDto { ProductId = 9, Quantity = 5 } } };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _servicio.CreateOrderAsync(7, entrada));

        Assert.Contains("No hay existencias suficientes", error.Message);
        Assert.Equal(1, cargador.Stock);                                  // las existencias no cambian
        _pedidos.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never); // el pedido no se guarda
    }

    [Fact(DisplayName = "Negativa 5 · GetOrderAsync del pedido de otro cliente lanza ForbiddenAccessException (H-02)")]
    [Trait("Tipo", "Negativa")]
    public async Task GetOrderAsync_PedidoDeOtroCliente_LanzaForbiddenAccessException()
    {
        // Arrange: el pedido 1 pertenece al cliente 1.
        _pedidos.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new Order { Id = 1, CustomerId = 1, Status = "Entregado", TotalAmount = 28999.50m });

        // Act + Assert: el cliente 7 intenta verlo.
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _servicio.GetOrderAsync(1, customerId: 7));
    }
}
