using Microsoft.AspNetCore.Identity;

namespace ProductApi.Models
{
    /// <summary>
    /// Usuario del sistema (H-01). Hereda de IdentityUser, que guarda la contraseña
    /// como hash PBKDF2 con sal; nunca en texto plano.
    /// Los usuarios con rol Cliente están ligados a un registro de Customer.
    /// El Admin no tiene cliente asociado.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        public long? CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }
}
