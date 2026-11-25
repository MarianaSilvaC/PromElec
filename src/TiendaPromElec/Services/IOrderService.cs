using TiendaPromElec.Dtos;

namespace TiendaPromElec.Services;

/// <summary>Reglas de negocio de los pedidos.</summary>
public interface IOrderService
{
    /// <param name="customerId">
    /// Cliente que consulta. <c>null</c> significa Admin (ve todos los pedidos).
    /// </param>
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(long? customerId);

    /// <param name="id">Id del pedido.</param>
    /// <param name="customerId">Cliente que consulta; <c>null</c> = Admin.</param>
    /// <exception cref="Exceptions.ForbiddenAccessException">El pedido es de otro cliente (H-02).</exception>
    Task<OrderDto?> GetOrderAsync(long id, long? customerId);

    /// <exception cref="Exceptions.BusinessRuleException">Producto inexistente o sin existencias.</exception>
    Task<OrderDto> CreateOrderAsync(long customerId, OrderCreateDto input);

    Task<bool> UpdateStatusAsync(long id, string status);
    Task<bool> DeleteOrderAsync(long id);
}
