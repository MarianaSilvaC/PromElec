using System.ComponentModel.DataAnnotations;

namespace TiendaPromElec.Dtos;

/// <summary>
/// Datos que el Admin envía para crear o actualizar un producto.
/// H-03: solo contiene los campos que el cliente PUEDE controlar (no hay Id ni navegación).
/// H-07: cada campo tiene reglas de validación; si no se cumplen, la API responde 400.
/// </summary>
public class ProductInputDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(500, ErrorMessage = "La descripción no puede pasar de 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [StringLength(50, ErrorMessage = "La marca no puede pasar de 50 caracteres.")]
    public string Brand { get; set; } = string.Empty;

    [Range(0.01, 1_000_000, ErrorMessage = "El precio debe estar entre 0.01 y 1,000,000.")]
    public decimal Price { get; set; }

    [Range(0, 100_000, ErrorMessage = "Las existencias deben estar entre 0 y 100,000.")]
    public int Stock { get; set; }

    // [Url] solo acepta direcciones http, https o ftp (rechaza, por ejemplo, "javascript:").
    [Url(ErrorMessage = "La imagen debe ser una URL válida.")]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public long CategoryId { get; set; }
}

/// <summary>Producto tal como lo devuelve la API.</summary>
public record ProductDto(
    long Id,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    int Stock,
    string ImageUrl,
    long CategoryId,
    string? CategoryName);
