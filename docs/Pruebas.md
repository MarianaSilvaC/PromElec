# Pruebas automatizadas — TiendaPromElec

El proyecto tiene **30 pruebas automatizadas** y todas pasan: 10 unitarias y 20 de integración.

| Proyecto | Herramientas | Pruebas | Resultado |
|---|---|---|---|
| `tests/TiendaPromElec.UnitTests` | xUnit 2.9.3 + Moq 4.21.0 | 10 (5 positivas, 5 negativas) | 10 de 10 correctas |
| `tests/TiendaPromElec.IntegrationTests` | xUnit 2.9.3 + `WebApplicationFactory<Program>` + SQLite en memoria | 20: 10 de funcionalidad y 10 de seguridad, cada grupo con 5 positivas y 5 negativas | 20 de 20 correctas |

## Cómo ejecutarlas

Desde la carpeta raíz del repositorio (`PromElec/`). Las pruebas **no necesitan SQL Server** ni secretos: usan una base de datos en memoria y valores de prueba.

```powershell
# Las 30 pruebas, mostrando el nombre de cada una
dotnet test --logger "console;verbosity=normal"

# Solo un proyecto
dotnet test tests/TiendaPromElec.UnitTests --logger "console;verbosity=normal"
dotnet test tests/TiendaPromElec.IntegrationTests --logger "console;verbosity=normal"

# Por grupo (las pruebas tienen las etiquetas Tipo y Categoria)
dotnet test tests/TiendaPromElec.UnitTests --filter "Tipo=Negativa"
dotnet test tests/TiendaPromElec.IntegrationTests --filter "Categoria=Funcionalidad"
dotnet test tests/TiendaPromElec.IntegrationTests --filter "Categoria=Seguridad&Tipo=Negativa"
```

---

## 1. Pruebas unitarias

### Qué es una prueba unitaria y por qué usamos Moq

Una prueba unitaria revisa **una sola pieza** del código (un controlador o un servicio) sin base de datos ni servidor web. Para aislarla, sus dependencias se reemplazan por **objetos simulados (mocks)** creados con Moq:

- Al probar un **controlador** se simula el **servicio** (`Mock<IProductService>`, `Mock<IOrderService>`).
- Al probar un **servicio** se simulan los **repositorios** (`Mock<IProductRepository>`, `Mock<IOrderRepository>`) y el reloj (`Mock<TimeProvider>`), para que la fecha sea siempre la misma.

Esto es posible porque en la Fase 3 la API se separó en capas que dependen de **interfaces**. Antes, los controladores usaban el `DbContext` directamente y no se podían aislar.

Cada prueba sigue el patrón **Arrange, Act, Assert**:

1. **Arrange** (preparar): se configura qué devuelve el mock, por ejemplo `Setup(...).ReturnsAsync(...)`.
2. **Act** (actuar): se llama al método que se prueba.
3. **Assert** (comprobar): se verifica el resultado y, con `Verify(..., Times.Once)` o `Times.Never`, que el mock se llamó o no se llamó.

### Lista de pruebas

| # | Tipo | Clase · método de prueba | Qué verifica | Qué se simula | Hallazgo |
|---|---|---|---|---|---|
| 1 | Positiva | `ProductControllerTests.GetProduct_IdExistente_Devuelve200ConElProducto` | Con un id existente, el controlador responde **200** con el producto | `IProductService` | — |
| 2 | Positiva | `ProductControllerTests.PostProduct_DatosValidos_Devuelve201` | Con datos válidos responde **201 Created**, apunta a `GetProduct` con el nuevo id y llama al servicio **una vez** | `IProductService` | H-03 |
| 3 | Positiva | `OrderControllerTests.GetOrders_ComoCliente_FiltraPorSuIdDeCliente` | Un Cliente recibe **solo sus pedidos**: el servicio se llama con el id de cliente **del token** y **nunca** con `null` (todos) | `IOrderService` + usuario simulado | H-02 |
| 4 | Positiva | `ProductServiceTests.CreateAsync_CategoriaExistente_GuardaProductoConLosDatosDelDto` | El servicio guarda solo los campos del DTO, quita espacios sobrantes y asigna una imagen por defecto | `IProductRepository` | H-03, H-07 |
| 5 | Positiva | `OrderServiceTests.CreateOrderAsync_CalculaTotalEnServidorYDescuentaExistencias` | El total se **calcula en el servidor** (2 × 1200 + 1 × 2400 = **4800**), el estado inicial es Pendiente y se descuentan las existencias | `IOrderRepository`, `IProductRepository`, `TimeProvider` | H-03 |
| 6 | Negativa | `ProductControllerTests.GetProduct_IdInexistente_Devuelve404` | Con un id inexistente responde **404** | `IProductService` | — |
| 7 | Negativa | `ProductControllerTests.PostProduct_ModeloInvalido_Devuelve400YNoGuarda` | Con un **modelo inválido** (precio negativo) responde **400** con el error del campo y **no** llama al servicio | `IProductService` | H-07 |
| 8 | Negativa | `ProductServiceTests.DeleteAsync_ProductoConPedidos_LanzaConflictExceptionYNoBorra` | Borrar un producto que tiene pedidos lanza **ConflictException** (409) y **no** borra nada | `IProductRepository` | H-15 |
| 9 | Negativa | `OrderServiceTests.CreateOrderAsync_ExistenciasInsuficientes_LanzaExcepcionYNoGuarda` | Pedir más piezas de las disponibles lanza **BusinessRuleException** (400), no cambia las existencias y **no** guarda el pedido | `IOrderRepository`, `IProductRepository`, `TimeProvider` | H-03 |
| 10 | Negativa | `OrderServiceTests.GetOrderAsync_PedidoDeOtroCliente_LanzaForbiddenAccessException` | Un Cliente que pide el pedido de **otro** cliente recibe **ForbiddenAccessException** (403) | `IOrderRepository` | H-02 |

Cobertura por componente: 2 controladores (`ProductController`, `OrderController`) y 2 servicios (`ProductService`, `OrderService`). Las negativas cubren un recurso inexistente (404), un modelo inválido (400) y las tres excepciones de negocio (409, 400 y 403).

### Resultado (25/11/2026)

```
Correctas Positiva 1 · GetProduct con un id existente responde 200 con el producto
Correctas Positiva 2 · PostProduct con datos válidos responde 201 y apunta al nuevo producto
Correctas Positiva 3 · GetOrders de un Cliente pide al servicio SOLO los pedidos de su id de cliente
Correctas Positiva 4 · CreateAsync guarda solo los campos del DTO y asigna una imagen por defecto
Correctas Positiva 5 · CreateOrderAsync calcula el total en el servidor y descuenta existencias (H-03)
Correctas Negativa 1 · GetProduct con un id inexistente responde 404
Correctas Negativa 2 · PostProduct con un modelo inválido responde 400 y no llama al servicio
Correctas Negativa 3 · DeleteAsync de un producto con pedidos lanza ConflictException y no borra (H-15)
Correctas Negativa 4 · CreateOrderAsync sin existencias suficientes lanza BusinessRuleException y no guarda
Correctas Negativa 5 · GetOrderAsync del pedido de otro cliente lanza ForbiddenAccessException (H-02)
Pruebas totales: 10
Correcto: 10
```

*(Las pruebas corren en paralelo, así que en la consola aparecen en otro orden.)*

---

## 2. Pruebas de integración

### Qué es una prueba de integración y cómo se levanta la API

Una prueba de integración revisa que **todas las piezas funcionen juntas**. Cada prueba hace peticiones HTTP reales (`GET`, `POST`, etc.) a la API completa: controladores, servicios, repositorios, Entity Framework y todo el pipeline de seguridad (JWT, políticas, CORS, antiforgery, cabeceras, redirección a HTTPS).

La API se levanta **en memoria** con `WebApplicationFactory<Program>`, sin abrir puertos. La clase [`PromElecApiFactory`](../tests/TiendaPromElec.IntegrationTests/Infraestructura/PromElecApiFactory.cs) usa el mismo `Program.cs` de producción y solo cambia tres cosas:

1. **Base de datos SQLite en memoria** en lugar de SQL Server. Se crea desde cero en cada ejecución con los datos semilla (10 productos, 2 clientes, 5 pedidos), así que las pruebas no tocan la base real y siempre empiezan igual.
2. **Secretos de prueba** (llave JWT y contraseña del Admin) en memoria. El entorno es `Testing`, así que no se cargan los user-secrets de la persona que corre las pruebas.
3. **Límite de peticiones más alto** en `/api/auth`, para que las pruebas no reciban 429.

Cada clase de pruebas tiene su propia API en memoria (`IClassFixture`). Dentro de una clase, cada prueba usa correos únicos para no depender de las demás.

### Pruebas de funcionalidad (10)

| # | Tipo | Prueba | Flujo / caso | Resultado esperado |
|---|---|---|---|---|
| 1 | Positiva | `Registro_Login_AccesoARecursoProtegido` | Registro → login → `GET /api/auth/me` con el token | 201 → 200 con token → 200 con el correo y rol Cliente |
| 2 | Positiva | `Catalogo_ListaYDetalle_SinIniciarSesion` | Catálogo sin iniciar sesión: lista y detalle del producto 2 | 200 con los productos; el 2 es el iPhone 16 Pro |
| 3 | Positiva | `Compra_PedidoConTotalCalculadoPorElServidor` | Un Cliente compra 2 cargadores mandando `totalAmount: 1` y `status: Entregado` | 201 con total **2400.00** y estado Pendiente; el pedido aparece en "mis pedidos"; las existencias bajan 2 |
| 4 | Positiva | `Admin_CreaYActualizaProducto` | El Admin crea un producto, lo actualiza y lo consulta | 201 (con `Location`) → 204 → el catálogo muestra el nombre y el precio nuevos |
| 5 | Positiva | `Admin_CambiaEstadoDePedido` | El Admin cambia el pedido 5 a "Enviado" | 204 → el pedido aparece como Enviado |
| 6 | Negativa | `TokenInvalidoOFalsificado_Devuelve401` | Token basura, y token de "Admin" **firmado con otra llave** | 401 en ambos casos |
| 7 | Negativa | `RecursoInexistente_Devuelve404` | Producto 999 y pedido 9999 | 404 |
| 8 | Negativa | `LoginIncorrecto_Devuelve401YBloqueaLaCuenta` | 5 intentos con contraseña incorrecta y luego la correcta | 401 con "Credenciales inválidas"; la cuenta queda **bloqueada** (H-14) |
| 9 | Negativa | `ProductoInvalido_Devuelve400ConErroresPorCampo` | Producto sin nombre, precio −100 e imagen `javascript:alert(1)` | 400 con errores en `Name`, `Price` e `ImageUrl` (H-07) |
| 10 | Negativa | `EliminarProductoConPedidos_Devuelve409YConservaHistorial` | El Admin intenta borrar el iPhone, que está en el pedido 1 | 409; el producto y la línea del pedido siguen existiendo (H-15) |

### Pruebas de seguridad (10)

| # | Tipo | Prueba | Caso | Resultado esperado | Control |
|---|---|---|---|---|---|
| 1 | Positiva | `Admin_AccedeATodosLosPedidos` | El Admin consulta los pedidos | 200 con pedidos de los clientes 1 **y** 2 | Política por rol |
| 2 | Positiva | `Cliente_AccedeASuPropioPedido` | Un Cliente consulta su pedido y su lista | 200; la lista solo contiene su pedido | Propiedad (H-02) |
| 3 | Positiva | `AccesoAutenticado_SoloPorHttps_ConCabecerasDeSeguridad` | Un Cliente autenticado entra por `http://` y luego por `https://` | `http` → **307** a https; `https` → 200 con `X-Content-Type-Options`, `X-Frame-Options`, CSP y **sin** `Server` | HTTPS (H-06), cabeceras (H-10) |
| 4 | Positiva | `OrigenPermitido_RecibeCabeceraCors` | Un Cliente autenticado llama desde `https://tienda.promelec.local` | 200 con `Access-Control-Allow-Origin` de ese origen | CORS (H-08) |
| 5 | Positiva | `FormularioConTokenAntiforgery_SeAcepta` | Se abre `/Contacto` y se envía con su token | Cookie `SameSite=Strict`, `Secure`, `HttpOnly`; envío aceptado (200) | CSRF (H-09) |
| 6 | Negativa | `SinAutenticacion_Devuelve401` | Sin token: ver pedidos, borrar y crear productos | **401** en los tres casos | Autenticación (H-01) |
| 7 | Negativa | `ClienteSinRolAdmin_Devuelve403` | Un Cliente intenta crear o borrar productos y cambiar un estado | **403** en los tres casos | Política `SoloAdmin` (H-01) |
| 8 | Negativa | `ClienteNoVePedidoAjeno_Devuelve403` | El Cliente B pide el pedido del Cliente A y el pedido 1 | **403** | IDOR (H-02) |
| 9 | Negativa | `OrigenNoPermitido_SinCabeceraCors` | *Preflight* desde `https://evil.com` | Sin `Access-Control-Allow-Origin`: el navegador bloquea la llamada | CORS (H-08) |
| 10 | Negativa | `FormularioSinTokenAntiforgery_Devuelve400` | Envío del formulario sin token, como lo haría un sitio malicioso | **400** | CSRF (H-09) |

### Resultado (25/11/2026)

```
Correctas Funcionalidad Positiva 1 · Registro -> login -> acceso a recurso protegido (/api/auth/me)
Correctas Funcionalidad Positiva 2 · El catálogo es público: lista de productos y detalle por id
Correctas Funcionalidad Positiva 3 · Compra: el Cliente crea un pedido, el servidor calcula el total y descuenta existencias
Correctas Funcionalidad Positiva 4 · El Admin crea y actualiza un producto, y el catálogo refleja los cambios
Correctas Funcionalidad Positiva 5 · El Admin cambia el estado de un pedido a Enviado
Correctas Funcionalidad Negativa 1 · Token inválido o falsificado -> 401 Unauthorized
Correctas Funcionalidad Negativa 2 · Recurso no encontrado -> 404 (producto y pedido inexistentes)
Correctas Funcionalidad Negativa 3 · Contraseña incorrecta -> 401, y tras 5 intentos la cuenta se bloquea
Correctas Funcionalidad Negativa 4 · Producto con datos inválidos -> 400 con el error de cada campo
Correctas Funcionalidad Negativa 5 · Eliminar un producto con pedidos -> 409 y el historial se conserva
Correctas Seguridad Positiva 1 · El Admin autenticado accede a los pedidos de TODOS los clientes
Correctas Seguridad Positiva 2 · El Cliente autenticado accede a SU pedido y su lista solo trae los suyos
Correctas Seguridad Positiva 3 · El acceso autenticado es por HTTPS: http redirige (307) y https responde con cabeceras de seguridad
Correctas Seguridad Positiva 4 · Desde un origen permitido por CORS, el Cliente autenticado recibe permiso del navegador
Correctas Seguridad Positiva 5 · Un formulario con token antiforgery válido se acepta (CSRF) y la cookie es SameSite=Strict
Correctas Seguridad Negativa 1 · Un usuario NO autenticado no puede ver pedidos ni modificar productos -> 401
Correctas Seguridad Negativa 2 · Un Cliente SIN permisos de Admin no puede crear, borrar productos ni cambiar estados -> 403
Correctas Seguridad Negativa 3 · Un Cliente no puede ver el pedido de OTRO cliente (IDOR) -> 403
Correctas Seguridad Negativa 4 · Un origen NO permitido por CORS no recibe permiso del navegador
Correctas Seguridad Negativa 5 · Un formulario enviado SIN token antiforgery se rechaza (CSRF) -> 400
Pruebas totales: 20
Correcto: 20
```

---

## 3. Las pruebas detectan fallas reales

Para comprobar que las pruebas no pasan "siempre", se desactivó a propósito la protección contra ver pedidos ajenos (H-02) en `OrderService.GetOrderAsync` y se volvieron a ejecutar:

| Ejecución | Unitarias | Integración |
|---|---|---|
| Con la protección desactivada | 9 de 10: falla **Negativa 5** (pedido de otro cliente) | 19 de 20: falla **Seguridad Negativa 3** (IDOR) |
| Con la protección restaurada | 10 de 10 | 20 de 20 |

Fallaron **exactamente** las dos pruebas que vigilan ese control. Si alguien elimina una protección por error en el futuro, las pruebas lo detectan antes de publicar el cambio.
