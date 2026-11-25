using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TiendaPromElec.Dtos;
using TiendaPromElec.IntegrationTests.Infraestructura;

namespace TiendaPromElec.IntegrationTests;

/// <summary>
/// Pruebas de integración de FUNCIONALIDAD (10): 5 positivas (flujos completos) y 5 negativas (errores).
/// Cada prueba hace peticiones HTTP reales a la API levantada en memoria.
/// </summary>
[Trait("Categoria", "Funcionalidad")]
public class FuncionalidadTests : ApiTestBase
{
    public FuncionalidadTests(PromElecApiFactory factory) : base(factory) { }

    // ---------------------------------------------------------------- Positivas

    [Fact(DisplayName = "Funcionalidad Positiva 1 · Registro -> login -> acceso a recurso protegido (/api/auth/me)")]
    [Trait("Tipo", "Positiva")]
    public async Task Registro_Login_AccesoARecursoProtegido()
    {
        var anonimo = CrearCliente();
        var correo = CorreoUnico();

        // 1. Registro
        var registro = await anonimo.PostAsJsonAsync("/api/auth/register", DatosRegistro(correo));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        // 2. Login con las mismas credenciales
        var login = await anonimo.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = correo, Password = PasswordCliente });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var sesion = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.False(string.IsNullOrWhiteSpace(sesion!.Token));
        Assert.Equal("Cliente", sesion.Role);

        // 3. Acceso a un recurso protegido con el token
        var perfil = await CrearCliente(sesion.Token).GetFromJsonAsync<UserProfileDto>("/api/auth/me");
        Assert.Equal(correo, perfil!.Email);
        Assert.Equal("Cliente", perfil.Role);
        Assert.NotNull(perfil.CustomerId);
    }

    [Fact(DisplayName = "Funcionalidad Positiva 2 · El catálogo es público: lista de productos y detalle por id")]
    [Trait("Tipo", "Positiva")]
    public async Task Catalogo_ListaYDetalle_SinIniciarSesion()
    {
        var anonimo = CrearCliente();

        var productos = await anonimo.GetFromJsonAsync<List<ProductDto>>("/api/Product");
        Assert.True(productos!.Count >= 10);
        Assert.Contains(productos, p => p.Name == "Galaxy S25");

        var iphone = await anonimo.GetFromJsonAsync<ProductDto>("/api/Product/2");
        Assert.Equal("iPhone 16 Pro", iphone!.Name);
        Assert.Equal(28999.50m, iphone.Price);
        Assert.Equal("Smartphones", iphone.CategoryName);
    }

    [Fact(DisplayName = "Funcionalidad Positiva 3 · Compra: el Cliente crea un pedido, el servidor calcula el total y descuenta existencias")]
    [Trait("Tipo", "Positiva")]
    public async Task Compra_PedidoConTotalCalculadoPorElServidor()
    {
        var token = await RegistrarClienteAsync();
        var cliente = CrearCliente(token);
        var existenciasAntes = (await cliente.GetFromJsonAsync<ProductDto>("/api/Product/9"))!.Stock;

        // El cliente intenta mandar un total de $1 y un estado "Entregado": la API los ignora.
        var respuesta = await cliente.PostAsJsonAsync("/api/Order", new
        {
            items = new[] { new { productId = 9, quantity = 2 } },
            totalAmount = 1,
            status = "Entregado"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var pedido = await respuesta.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(2400.00m, pedido!.TotalAmount);          // 2 x $1,200.00
        Assert.Equal("Pendiente", pedido.Status);
        Assert.Single(pedido.Items);

        var misPedidos = await cliente.GetFromJsonAsync<List<OrderDto>>("/api/Order");
        Assert.Contains(misPedidos!, p => p.Id == pedido.Id);

        var existenciasDespues = (await cliente.GetFromJsonAsync<ProductDto>("/api/Product/9"))!.Stock;
        Assert.Equal(existenciasAntes - 2, existenciasDespues);
    }

    [Fact(DisplayName = "Funcionalidad Positiva 4 · El Admin crea y actualiza un producto, y el catálogo refleja los cambios")]
    [Trait("Tipo", "Positiva")]
    public async Task Admin_CreaYActualizaProducto()
    {
        var admin = CrearCliente(await LoginAdminAsync());

        var creacion = await admin.PostAsJsonAsync("/api/Product", new ProductInputDto
        {
            Name = "Bocina PromElec",
            Description = "Bocina Bluetooth promocional",
            Brand = "PromElec",
            Price = 650m,
            Stock = 30,
            CategoryId = 3
        });
        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        var creado = await creacion.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal($"/api/Product/{creado!.Id}", creacion.Headers.Location!.AbsolutePath);

        var actualizacion = await admin.PutAsJsonAsync($"/api/Product/{creado.Id}", new ProductInputDto
        {
            Name = "Bocina PromElec Pro",
            Description = "Bocina Bluetooth promocional",
            Brand = "PromElec",
            Price = 799m,
            Stock = 30,
            CategoryId = 3
        });
        Assert.Equal(HttpStatusCode.NoContent, actualizacion.StatusCode);

        var consultado = await CrearCliente().GetFromJsonAsync<ProductDto>($"/api/Product/{creado.Id}");
        Assert.Equal("Bocina PromElec Pro", consultado!.Name);
        Assert.Equal(799m, consultado.Price);
    }

    [Fact(DisplayName = "Funcionalidad Positiva 5 · El Admin cambia el estado de un pedido a Enviado")]
    [Trait("Tipo", "Positiva")]
    public async Task Admin_CambiaEstadoDePedido()
    {
        var admin = CrearCliente(await LoginAdminAsync());

        var respuesta = await admin.PutAsJsonAsync("/api/Order/5/status", new OrderStatusUpdateDto { Status = "Enviado" });
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var pedido = await admin.GetFromJsonAsync<OrderDto>("/api/Order/5");
        Assert.Equal("Enviado", pedido!.Status);
    }

    // ---------------------------------------------------------------- Negativas

    [Fact(DisplayName = "Funcionalidad Negativa 1 · Token inválido o falsificado -> 401 Unauthorized")]
    [Trait("Tipo", "Negativa")]
    public async Task TokenInvalidoOFalsificado_Devuelve401()
    {
        // a) Un texto cualquiera en lugar de un token
        var conBasura = await CrearCliente("esto.no.es.un.token").GetAsync("/api/Order");
        Assert.Equal(HttpStatusCode.Unauthorized, conBasura.StatusCode);

        // b) Un token "Admin" bien formado, pero firmado con una llave que NO es la del servidor
        var llaveFalsa = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("llave-falsa-de-un-atacante-0123456789-abcdef"));
        var tokenFalso = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", "atacante"),
                new Claim("email", "atacante@evil.com"),
                new Claim("role", "Admin")
            }),
            Issuer = "PromElec.TiendaApi",
            Audience = "PromElec.Clientes",
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(llaveFalsa, SecurityAlgorithms.HmacSha256)
        });

        var conTokenFalso = await CrearCliente(tokenFalso).DeleteAsync("/api/Product/1");
        Assert.Equal(HttpStatusCode.Unauthorized, conTokenFalso.StatusCode);
    }

    [Fact(DisplayName = "Funcionalidad Negativa 2 · Recurso no encontrado -> 404 (producto y pedido inexistentes)")]
    [Trait("Tipo", "Negativa")]
    public async Task RecursoInexistente_Devuelve404()
    {
        var producto = await CrearCliente().GetAsync("/api/Product/999");
        Assert.Equal(HttpStatusCode.NotFound, producto.StatusCode);

        var pedido = await CrearCliente(await LoginAdminAsync()).GetAsync("/api/Order/9999");
        Assert.Equal(HttpStatusCode.NotFound, pedido.StatusCode);
    }

    [Fact(DisplayName = "Funcionalidad Negativa 3 · Contraseña incorrecta -> 401, y tras 5 intentos la cuenta se bloquea")]
    [Trait("Tipo", "Negativa")]
    public async Task LoginIncorrecto_Devuelve401YBloqueaLaCuenta()
    {
        var correo = CorreoUnico();
        await RegistrarClienteAsync(correo);
        var anonimo = CrearCliente();

        for (var intento = 1; intento <= 5; intento++)
        {
            var fallido = await anonimo.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = correo, Password = "Incorrecta#123" });
            Assert.Equal(HttpStatusCode.Unauthorized, fallido.StatusCode);
            var problema = await fallido.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("Credenciales inválidas", problema!.Title);
        }

        // Aun con la contraseña CORRECTA, la cuenta está bloqueada 15 minutos (H-14).
        var conPasswordCorrecto = await anonimo.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = correo, Password = PasswordCliente });
        Assert.Equal(HttpStatusCode.Unauthorized, conPasswordCorrecto.StatusCode);
    }

    [Fact(DisplayName = "Funcionalidad Negativa 4 · Producto con datos inválidos -> 400 con el error de cada campo")]
    [Trait("Tipo", "Negativa")]
    public async Task ProductoInvalido_Devuelve400ConErroresPorCampo()
    {
        var admin = CrearCliente(await LoginAdminAsync());

        var respuesta = await admin.PostAsJsonAsync("/api/Product", new
        {
            name = "",
            description = "Sin nombre y con precio negativo",
            brand = "PromElec",
            price = -100,
            stock = 5,
            imageUrl = "javascript:alert(1)",
            categoryId = 3
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Name", problema!.Errors.Keys);
        Assert.Contains("Price", problema.Errors.Keys);
        Assert.Contains("ImageUrl", problema.Errors.Keys);
    }

    [Fact(DisplayName = "Funcionalidad Negativa 5 · Eliminar un producto con pedidos -> 409 y el historial se conserva")]
    [Trait("Tipo", "Negativa")]
    public async Task EliminarProductoConPedidos_Devuelve409YConservaHistorial()
    {
        var admin = CrearCliente(await LoginAdminAsync());

        // El iPhone (producto 2) aparece en el pedido 1.
        var respuesta = await admin.DeleteAsync("/api/Product/2");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("aparece en pedidos", problema!.Detail);

        var producto = await admin.GetAsync("/api/Product/2");
        Assert.Equal(HttpStatusCode.OK, producto.StatusCode);
        var pedido = await admin.GetFromJsonAsync<OrderDto>("/api/Order/1");
        Assert.Contains(pedido!.Items, i => i.ProductId == 2);
    }
}
