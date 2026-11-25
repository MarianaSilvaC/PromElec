namespace ProductApi.Models
{
    public class Category
    {
        public long Id { get; set; }
        public required string Name { get; set; }

        // Se inicializa la colección para eliminar la advertencia CS8618 del código original.
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
