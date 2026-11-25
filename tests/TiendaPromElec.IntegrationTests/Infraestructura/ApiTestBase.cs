using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TiendaPromElec.Dtos;

namespace TiendaPromElec.IntegrationTests.Infraestructura;

/// <summary>
/// Operaciones comunes de las pruebas: crear clientes HTTP, iniciar sesión y registrar Clientes.
/// IClassFixture hace que todas las pruebas de una clase compartan la misma API en memoria
/// (y su base de datos), así que cada prueba usa correos únicos para no chocar con las demás.
/// </summary>
public abstract class ApiTestBase : IClassFixture<PromElecApiFactory>
{
    protected const string PasswordCliente = "Cliente#2026x";

    protected ApiTestBase(PromElecApiFactory factory)
    {
        Factory = factory;
    }

    protected PromElecApiFactory Factory { get; }

    /// <summary>Cliente HTTP que habla con la API por HTTPS y no sigue redirecciones automáticamente.</summary>
    protected HttpClient CrearCliente(string? token = null, string baseAddress = "https://localhost")
    {
        var cliente = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(baseAddress),
            AllowAutoRedirect = false
        });

        if (token is not null)
        {
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return cliente;
    }

    protected async Task<string> LoginAdminAsync()
    {
        var respuesta = await CrearCliente().PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = PromElecApiFactory.AdminEmail, Password = PromElecApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token;
    }

    protected static string CorreoUnico() => $"cliente.{Guid.NewGuid():N}@promelec.test";

    protected static RegisterDto DatosRegistro(string correo, string password = PasswordCliente) => new()
    {
        FullName = "Cliente de Prueba",
        Email = correo,
        Password = password,
        Phone = "5512345678",
        Address = "Av. Siempre Viva 742, CDMX"
    };

    /// <summary>Registra un Cliente nuevo y devuelve su token.</summary>
    protected async Task<string> RegistrarClienteAsync(string? correo = null)
    {
        var respuesta = await CrearCliente().PostAsJsonAsync("/api/auth/register", DatosRegistro(correo ?? CorreoUnico()));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token;
    }

    /// <summary>El Cliente compra 1 pieza del producto indicado y devuelve el pedido creado.</summary>
    protected async Task<OrderDto> CrearPedidoAsync(string tokenCliente, long productId = 10, int cantidad = 1)
    {
        var respuesta = await CrearCliente(tokenCliente).PostAsJsonAsync("/api/Order",
            new { items = new[] { new { productId, quantity = cantidad } } });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
