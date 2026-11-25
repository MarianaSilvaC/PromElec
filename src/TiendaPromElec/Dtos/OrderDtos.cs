using System.ComponentModel.DataAnnotations;

namespace TiendaPromElec.Dtos;

/// <summary>
/// Un artículo del pedido: el cliente SOLO indica qué producto y cuántas piezas quiere.
/// H-03: el precio unitario y el total los calcula el servidor con los precios de la base de datos.
/// </summary>
public class OrderItemInputDto
{
    [Range(1, long.MaxValue, ErrorMessage = "El producto es obligatorio.")]
    public long ProductId { get; set; }

    [Range(1, 100, ErrorMessage = "La cantidad debe estar entre 1 y 100.")]
    public int Quantity { get; set; }
}

/// <summary>
/// Pedido que crea un Cliente. No incluye total, estado ni cliente:
/// el total lo calcula el servidor, el estado inicial es "Pendiente" y el cliente sale del token.
/// </summary>
public class OrderCreateDto
{
    [Required(ErrorMessage = "El pedido debe tener artículos.")]
    [MinLength(1, ErrorMessage = "El pedido debe tener al menos un artículo.")]
    [MaxLength(20, ErrorMessage = "El pedido no puede tener más de 20 artículos.")]
    public List<OrderItemInputDto> Items { get; set; } = new();
}

/// <summary>Cambio de estado de un pedido (solo Admin).</summary>
public class OrderStatusUpdateDto
{
    [Required(ErrorMessage = "El estado es obligatorio.")]
    [RegularExpression("^(Pendiente|Pagado|Enviado|Entregado|Cancelado)$",
        ErrorMessage = "Estado no válido. Usa: Pendiente, Pagado, Enviado, Entregado o Cancelado.")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>Línea de un pedido tal como la devuelve la API.</summary>
public record OrderItemDto(long ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);

/// <summary>Pedido tal como lo devuelve la API, con sus artículos.</summary>
public record OrderDto(
    long Id,
    DateTime OrderDate,
    decimal TotalAmount,
    string Status,
    long CustomerId,
    IReadOnlyList<OrderItemDto> Items);
