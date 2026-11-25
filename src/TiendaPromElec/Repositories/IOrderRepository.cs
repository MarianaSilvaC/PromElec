using ProductApi.Models;

namespace TiendaPromElec.Repositories;

/// <summary>Acceso a datos de pedidos (siempre incluye sus artículos y productos).</summary>
public interface IOrderRepository
{
    /// <param name="customerId">Si se indica, solo devuelve los pedidos de ese cliente.</param>
    Task<IReadOnlyList<Order>> GetAllAsync(long? customerId);
    Task<Order?> GetByIdAsync(long id);
    Task AddAsync(Order order);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Order order);
}
