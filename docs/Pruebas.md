# Pruebas automatizadas — TiendaPromElec

El proyecto tiene **10 pruebas unitarias** (5 positivas y 5 negativas), hechas con xUnit y Moq. Todas pasan.

| Proyecto | Herramientas | Pruebas | Resultado |
|---|---|---|---|
| `tests/TiendaPromElec.UnitTests` | xUnit 2.9.3 + Moq 4.21.0 | 10 (5 positivas, 5 negativas) | 10 de 10 correctas |

## Cómo ejecutarlas

Desde la carpeta raíz del repositorio (`PromElec/`):

```powershell
# Todas, mostrando el nombre de cada prueba
dotnet test tests/TiendaPromElec.UnitTests --logger "console;verbosity=normal"

# Solo las positivas o solo las negativas
dotnet test tests/TiendaPromElec.UnitTests --filter "Tipo=Positiva"
dotnet test tests/TiendaPromElec.UnitTests --filter "Tipo=Negativa"
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
