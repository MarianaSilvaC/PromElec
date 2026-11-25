using ProductApi.Models;
using TiendaPromElec.Dtos;
using TiendaPromElec.Exceptions;
using TiendaPromElec.Repositories;

namespace TiendaPromElec.Services;

/// <summary>Estados válidos de un pedido.</summary>
public static class OrderStatuses
{
    public const string Pendiente = "Pendiente";

    public static readonly string[] All = { "Pendiente", "Pagado", "Enviado", "Entregado", "Cancelado" };
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly TimeProvider _time;

    public OrderService(IOrderRepository orders, IProductRepository products, TimeProvider time)
    {
        _orders = orders;
        _products = products;
        _time = time;
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(long? customerId)
    {
        // H-02: un Cliente solo recibe SUS pedidos; el filtro usa el id que viene del token.
        var orders = await _orders.GetAllAsync(customerId);
        return orders.Select(ToDto).ToList();
    }

    public async Task<OrderDto?> GetOrderAsync(long id, long? customerId)
    {
        var order = await _orders.GetByIdAsync(id);
        if (order is null)
        {
            return null;
        }

        // H-02: si un Cliente pide el pedido de otro cliente, se niega el acceso (403).
        if (customerId.HasValue && order.CustomerId != customerId.Value)
        {
            throw new ForbiddenAccessException($"El pedido {id} no pertenece al cliente que lo solicita.");
        }

        return ToDto(order);
    }

    public async Task<OrderDto> CreateOrderAsync(long customerId, OrderCreateDto input)
    {
        if (input.Items is null || input.Items.Count == 0)
        {
            throw new BusinessRuleException("El pedido debe tener al menos un artículo.");
        }

        // Si el mismo producto viene dos veces, se suman las cantidades.
        var requested = input.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        var products = await _products.GetByIdsAsync(requested.Select(r => r.ProductId));

        // H-03: el estado inicial, la fecha y el cliente los decide el servidor, no el cliente.
        var order = new Order
        {
            OrderDate = _time.GetUtcNow().UtcDateTime,
            Status = OrderStatuses.Pendiente,
            CustomerId = customerId
        };

        foreach (var item in requested)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new BusinessRuleException($"El producto {item.ProductId} no existe.");

            if (product.Stock < item.Quantity)
            {
                throw new BusinessRuleException(
                    $"No hay existencias suficientes de '{product.Name}'. Disponibles: {product.Stock}, solicitadas: {item.Quantity}.");
            }

            product.Stock -= item.Quantity;

            // H-03: el precio unitario sale de la base de datos, nunca de la petición.
            order.Items.Add(new OrderDetail
            {
                ProductId = product.Id,
                Product = product,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
        }

        // H-03: el total lo calcula el servidor.
        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        await _orders.AddAsync(order);
        return ToDto(order);
    }

    public async Task<bool> UpdateStatusAsync(long id, string status)
    {
        if (!OrderStatuses.All.Contains(status))
        {
            throw new BusinessRuleException($"El estado '{status}' no es válido.");
        }

        var order = await _orders.GetByIdAsync(id);
        if (order is null)
        {
            return false;
        }

        order.Status = status;
        await _orders.UpdateAsync(order);
        return true;
    }

    public async Task<bool> DeleteOrderAsync(long id)
    {
        var order = await _orders.GetByIdAsync(id);
        if (order is null)
        {
            return false;
        }

        await _orders.DeleteAsync(order);
        return true;
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.OrderDate,
        o.TotalAmount,
        o.Status,
        o.CustomerId,
        o.Items
            .Select(i => new OrderItemDto(
                i.ProductId,
                i.Product?.Name ?? $"Producto {i.ProductId}",
                i.Quantity,
                i.UnitPrice,
                i.UnitPrice * i.Quantity))
            .ToList());
}
