using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TiendaPromElec.Controllers;
using TiendaPromElec.Dtos;
using TiendaPromElec.Services;
using TiendaPromElec.UnitTests.Helpers;

namespace TiendaPromElec.UnitTests.Controllers;

/// <summary>Pruebas unitarias de OrderController con el servicio de pedidos simulado.</summary>
public class OrderControllerTests
{
    [Fact(DisplayName = "Positiva 3 · GetOrders de un Cliente pide al servicio SOLO los pedidos de su id de cliente")]
    [Trait("Tipo", "Positiva")]
    public async Task GetOrders_ComoCliente_FiltraPorSuIdDeCliente()
    {
        // Arrange: el usuario es el Cliente 7 (el id viene de su token, no de la petición).
        var servicio = new Mock<IOrderService>();
        var pedidosDelCliente = new List<OrderDto>
        {
            new(40, new DateTime(2026, 11, 20), 2400m, "Pendiente", 7, Array.Empty<OrderItemDto>())
        };
        servicio.Setup(s => s.GetOrdersAsync(7)).ReturnsAsync(pedidosDelCliente);

        var controlador = new OrderController(servicio.Object, NullLogger<OrderController>.Instance)
        {
            ControllerContext = UsuarioDePrueba.Cliente(customerId: 7)
        };

        // Act
        var resultado = await controlador.GetOrders();

        // Assert: responde 200 y el servicio se llamó con el id 7, nunca con null ("todos los pedidos").
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var pedidos = Assert.IsAssignableFrom<IEnumerable<OrderDto>>(ok.Value);
        Assert.All(pedidos, p => Assert.Equal(7, p.CustomerId));
        servicio.Verify(s => s.GetOrdersAsync(7), Times.Once);
        servicio.Verify(s => s.GetOrdersAsync(null), Times.Never);
    }
}
