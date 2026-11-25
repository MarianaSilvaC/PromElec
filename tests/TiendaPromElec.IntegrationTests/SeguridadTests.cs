using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using TiendaPromElec.Dtos;
using TiendaPromElec.IntegrationTests.Infraestructura;

namespace TiendaPromElec.IntegrationTests;

/// <summary>
/// Pruebas de integración de SEGURIDAD (10):
/// 5 positivas (usuarios autenticados acceden a lo que SÍ tienen permitido)
/// y 5 negativas (se niega el acceso a usuarios no autenticados o sin permisos).
/// Cubren autorización por roles, propiedad de pedidos (IDOR), HTTPS, CORS y CSRF.
/// </summary>
[Trait("Categoria", "Seguridad")]
public partial class SeguridadTests : ApiTestBase
{
    public SeguridadTests(PromElecApiFactory factory) : base(factory) { }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    private static FormUrlEncodedContent FormularioContacto(string? tokenAntiforgery)
    {
        var campos = new Dictionary<string, string>
        {
            ["Input.Nombre"] = "Ana",
            ["Input.Correo"] = "ana@promelec.test",
            ["Input.Mensaje"] = "Quiero información sobre artículos promocionales."
        };
        if (tokenAntiforgery is not null)
        {
            campos["__RequestVerificationToken"] = tokenAntiforgery;
        }
        return new FormUrlEncodedContent(campos);
    }

    // ---------------------------------------------------------------- Positivas

    [Fact(DisplayName = "Seguridad Positiva 1 · El Admin autenticado accede a los pedidos de TODOS los clientes")]
    [Trait("Tipo", "Positiva")]
    public async Task Admin_AccedeATodosLosPedidos()
    {
        var respuesta = await CrearCliente(await LoginAdminAsync()).GetAsync("/api/Order");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pedidos = await respuesta.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.Contains(pedidos!, p => p.CustomerId == 1);
        Assert.Contains(pedidos!, p => p.CustomerId == 2);
    }

    [Fact(DisplayName = "Seguridad Positiva 2 · El Cliente autenticado accede a SU pedido y su lista solo trae los suyos")]
    [Trait("Tipo", "Positiva")]
    public async Task Cliente_AccedeASuPropioPedido()
    {
        var token = await RegistrarClienteAsync();
        var pedido = await CrearPedidoAsync(token);
        var cliente = CrearCliente(token);

        var detalle = await cliente.GetAsync($"/api/Order/{pedido.Id}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

        var lista = await cliente.GetFromJsonAsync<List<OrderDto>>("/api/Order");
        Assert.Single(lista!);
        Assert.All(lista!, p => Assert.Equal(pedido.CustomerId, p.CustomerId));
    }

    [Fact(DisplayName = "Seguridad Positiva 3 · El acceso autenticado es por HTTPS: http redirige (307) y https responde con cabeceras de seguridad")]
    [Trait("Tipo", "Positiva")]
    public async Task AccesoAutenticado_SoloPorHttps_ConCabecerasDeSeguridad()
    {
        var token = await RegistrarClienteAsync();

        // Por HTTP: la API no atiende, redirige a HTTPS (H-06).
        var porHttp = await CrearCliente(token, baseAddress: "http://localhost").GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, porHttp.StatusCode);
        Assert.Equal("https", porHttp.Headers.Location!.Scheme);

        // Por HTTPS: 200 con las cabeceras de seguridad y sin revelar el servidor (H-10).
        var porHttps = await CrearCliente(token).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, porHttps.StatusCode);
        Assert.Equal("nosniff", porHttps.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", porHttps.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(porHttps.Headers.Contains("Content-Security-Policy"));
        Assert.False(porHttps.Headers.Contains("Server"));
    }

    [Fact(DisplayName = "Seguridad Positiva 4 · Desde un origen permitido por CORS, el Cliente autenticado recibe permiso del navegador")]
    [Trait("Tipo", "Positiva")]
    public async Task OrigenPermitido_RecibeCabeceraCors()
    {
        var token = await RegistrarClienteAsync();
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/Order");
        peticion.Headers.Add("Origin", PromElecApiFactory.OrigenPermitido);

        var respuesta = await CrearCliente(token).SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(PromElecApiFactory.OrigenPermitido, respuesta.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact(DisplayName = "Seguridad Positiva 5 · Un formulario con token antiforgery válido se acepta (CSRF) y la cookie es SameSite=Strict")]
    [Trait("Tipo", "Positiva")]
    public async Task FormularioConTokenAntiforgery_SeAcepta()
    {
        var navegador = CrearCliente(); // guarda cookies, como un navegador

        var pagina = await navegador.GetAsync("/Contacto");
        var cookie = pagina.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".PromElec.Antiforgery"));
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);

        var token = TokenAntiforgery().Match(await pagina.Content.ReadAsStringAsync()).Groups[1].Value;
        var envio = await navegador.PostAsync("/Contacto", FormularioContacto(token));

        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        Assert.Contains("Recibimos tu mensaje", await envio.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------- Negativas

    [Fact(DisplayName = "Seguridad Negativa 1 · Un usuario NO autenticado no puede ver pedidos ni modificar productos -> 401")]
    [Trait("Tipo", "Negativa")]
    public async Task SinAutenticacion_Devuelve401()
    {
        var anonimo = CrearCliente();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/Order")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.DeleteAsync("/api/Product/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.PostAsJsonAsync("/api/Product", new { name = "Hack", price = 1 })).StatusCode);
    }

    [Fact(DisplayName = "Seguridad Negativa 2 · Un Cliente SIN permisos de Admin no puede crear, borrar productos ni cambiar estados -> 403")]
    [Trait("Tipo", "Negativa")]
    public async Task ClienteSinRolAdmin_Devuelve403()
    {
        var cliente = CrearCliente(await RegistrarClienteAsync());

        var crear = await cliente.PostAsJsonAsync("/api/Product", new ProductInputDto
        {
            Name = "Producto pirata", Description = "No debería crearse", Brand = "X", Price = 1m, Stock = 1, CategoryId = 1
        });
        var borrar = await cliente.DeleteAsync("/api/Product/1");
        var cambiarEstado = await cliente.PutAsJsonAsync("/api/Order/1/status", new OrderStatusUpdateDto { Status = "Cancelado" });

        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, borrar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cambiarEstado.StatusCode);
    }

    [Fact(DisplayName = "Seguridad Negativa 3 · Un Cliente no puede ver el pedido de OTRO cliente (IDOR) -> 403")]
    [Trait("Tipo", "Negativa")]
    public async Task ClienteNoVePedidoAjeno_Devuelve403()
    {
        var tokenA = await RegistrarClienteAsync();
        var pedidoDeA = await CrearPedidoAsync(tokenA);
        var clienteB = CrearCliente(await RegistrarClienteAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await clienteB.GetAsync($"/api/Order/{pedidoDeA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clienteB.GetAsync("/api/Order/1")).StatusCode);
    }

    [Fact(DisplayName = "Seguridad Negativa 4 · Un origen NO permitido por CORS no recibe permiso del navegador")]
    [Trait("Tipo", "Negativa")]
    public async Task OrigenNoPermitido_SinCabeceraCors()
    {
        // Petición previa (preflight) que hace el navegador antes de enviar un token desde otro sitio.
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/Order");
        preflight.Headers.Add("Origin", "https://evil.com");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");

        var respuesta = await CrearCliente().SendAsync(preflight);

        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Headers"));
    }

    [Fact(DisplayName = "Seguridad Negativa 5 · Un formulario enviado SIN token antiforgery se rechaza (CSRF) -> 400")]
    [Trait("Tipo", "Negativa")]
    public async Task FormularioSinTokenAntiforgery_Devuelve400()
    {
        // Simula un sitio malicioso que envía el formulario en nombre del usuario, sin el token.
        var respuesta = await CrearCliente().PostAsync("/Contacto", FormularioContacto(tokenAntiforgery: null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
