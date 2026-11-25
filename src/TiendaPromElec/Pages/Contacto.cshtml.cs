using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TiendaPromElec.Pages
{
    /// <summary>
    /// Formulario público de contacto. Sirve para demostrar la protección CSRF (H-09):
    /// Razor Pages valida el token antiforgery en cada POST de forma automática.
    /// </summary>
    [AllowAnonymous]
    public class ContactoModel : PageModel
    {
        private readonly ILogger<ContactoModel> _logger;

        public ContactoModel(ILogger<ContactoModel> logger)
        {
            _logger = logger;
        }

        [BindProperty]
        public ContactoInput Input { get; set; } = new();

        public bool Enviado { get; private set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // No se registra el contenido del mensaje ni el correo (datos personales).
            _logger.LogInformation("Mensaje de contacto recibido ({Caracteres} caracteres).", Input.Mensaje.Length);

            Enviado = true;
            ModelState.Clear();
            Input = new ContactoInput();
            return Page();
        }
    }

    public class ContactoInput
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El mensaje es obligatorio.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre 10 y 1000 caracteres.")]
        public string Mensaje { get; set; } = string.Empty;
    }
}
