namespace ProductApi.Models
{
    public class Order
    {
        public long Id { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public required string Status { get; set; }
        public long CustomerId { get; set; }

        // Las propiedades de navegación ahora aceptan null. En el original eran no nulas,
        // y por eso ASP.NET las volvía obligatorias y POST/PUT siempre respondían 400 (H-03).
        public Customer? Customer { get; set; }
        public ICollection<OrderDetail> Items { get; set; } = new List<OrderDetail>();
    }
}
