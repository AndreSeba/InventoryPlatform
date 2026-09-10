# InventoryPlatform — CLAUDE.md

> Backend de la plataforma de Inventario de Material Promocional (Marketing,
> Nestlé Bolivia). Lee este archivo antes de tocar código. Si el código pide
> una decisión no documentada acá, preguntá antes de asumir.

## Propósito

Reemplaza el `SISTEMA DE INVENTARIO MARKETING.xlsm` (Excel con macros) por una
plataforma con trazabilidad real: catálogo de productos, movimientos de stock
por ubicación (entradas, salidas, ajustes, devoluciones de préstamo), un
flujo de solicitud/aprobación/entrega por área, y sesiones de conteo físico
reconciliables contra el sistema.

**Este repo es solo el backend.** El frontend vive en un repo hermano,
`../InventoryPlatform.Web` (Blazor Server) — ver la sección "Frontend" más
abajo antes de asumir que hay que tocar UI acá.

## Stack (cerrado — no reabrir sin aprobación explícita)

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 |
| API | ASP.NET Core Web API — **arquitectura MVC con Controllers**, no Minimal API |
| Acceso a datos | Entity Framework Core + SQL Server |
| Documentación de API | OpenAPI nativo (`AddOpenApi`) + Scalar en `/scalar/v1` (solo Development) |
| Auth | **No implementada todavía** — todo controller usa `User.Identity?.Name ?? "sistema"` como placeholder de usuario. No inventar un esquema de auth sin que el usuario lo pida. |

## Arquitectura en capas (estricta)

```
src/
├── Inventory.Domain/          # Entidades + enums. Sin dependencias de nada.
├── Inventory.Application/     # DTOs, interfaces de servicio, excepciones de
│                               # dominio (DominioException). Sin EF Core.
│                               # ⚠️ Es el único proyecto que referencia el
│                               # frontend (InventoryPlatform.Web) por path
│                               # relativo — mantenerlo libre de dependencias
│                               # pesadas (nada de EF Core acá).
├── Inventory.Infrastructure/   # DbContext, IEntityTypeConfiguration por
│                               # entidad, implementaciones de servicio.
└── Inventory.Api/              # Controllers MVC, middleware de excepciones,
                                 # Program.cs (DI, CORS, Scalar).
tests/Inventory.UnitTests/      # xUnit + SQLite en memoria (no el proveedor
                                 # InMemory de EF — los servicios usan
                                 # transacciones explícitas que InMemory no
                                 # soporta de verdad).
```

Nunca SQL en Controllers. Nunca lógica de negocio en Configurations. Los
servicios de `Inventory.Infrastructure/Services/` son el único lugar donde
vive la lógica de negocio real.

## Modelo de datos (v4 — adaptado de una guía SharePoint/Power Automate)

```
Categoria           → CodigoCategoria (único entre activas), Descripcion, Activo
Area                → CodigoArea (único entre activas), NombreArea, Activo
                       # quién puede solicitar material (Solicitud.AreaId)
Ubicacion           → TipoUbicacion (Rack|Mueble), Nro, Lado, Nivel (solo Rack),
                       CodigoUbicacion (generado en el servicio: Rack →
                       "{Lado}-{Nro}-{Nivel}", Mueble → "M{Nro}-{Lado}"), Activo
Producto            → ClaveProducto (Codigo+"-"+Unidad, único entre activos),
                       CodigoProducto, Nombre, CategoriaId, UnidadMedida
                       (UNI|CAJA|PQTS), CostoUnitario, StockMinimo, Detalle,
                       ImagenUrl, Activo
                       # NO tiene StockActual cacheado ni FechaVencimiento —
                       # ver "Decisiones cerradas" más abajo.
Movimiento           → NumeroMovimiento (generado post-insert: "MOV-{año}-
                       {id:D6}"), ProductoId, TipoMovimiento (Entrada|Salida|
                       AjustePositivo|AjusteNegativo), Cantidad,
                       CantidadEfectiva (con signo — SUM() da el stock),
                       UbicacionId (obligatoria en los 4 tipos), Retorna,
                       UbicacionExterna, FechaRetornoEsperada (solo si
                       TipoMovimiento=Salida), MovimientoOrigenId (self-FK,
                       la devolución apunta a la Salida que la originó),
                       SolicitudDetalleId (opcional, solo en Salida),
                       RegistradoPor, Motivo, FechaMovimiento
Solicitud            → NumeroSolicitud (generado post-insert: "SOL-{año}-
                       {id:D6}"), AreaId, Estado (Pendiente|Aprobada|
                       Rechazada|EntregadaParcial|Entregada|Cancelada|
                       Borrador —el flujo actual no usa Borrador, crea
                       directo en Pendiente), SolicitadoPor, AprobadoPor,
                       FechaResolucion, MotivoRechazo (obligatorio si Rechazada)
SolicitudDetalle     → SolicitudId, ProductoId (único por Solicitud),
                       CantidadSolicitada, CantidadAprobada (null hasta
                       aprobar, <= Solicitada), CantidadEntregada (cache
                       acumulado desde los Movimiento de Salida ligados)
Conteo               → SesionConteo, ProductoId, UbicacionId, NumeroConteo
                       (permite reconteos 1,2,3…), CantidadContada,
                       ContadoPor, FechaConteo
                       # único por (SesionConteo, ProductoId, UbicacionId,
                       # NumeroConteo)
Auditoria            → log genérico (UsuarioId, Entidad, EntidadId, Accion,
                       ValorAnterior, ValorNuevo, Motivo, CorrelationId) —
                       hoy solo lo escribe ProductoService (crear/actualizar/
                       desactivar). No está conectado a Movimiento/Solicitud
                       todavía.
```

### Reglas de negocio críticas (todas con CHECK constraint de respaldo en la BD, vía `HasCheckConstraint` en las Configurations — no solo validadas en C#)

- **`CantidadEfectiva` con signo obligatorio**: Entrada/AjustePositivo → `+Cantidad`; Salida/AjusteNegativo → `-Cantidad`. `CK_Movimiento_Signo`.
- **Stock se calcula, nunca se cachea**: `SUM(CantidadEfectiva)` por producto (global) o por (producto, ubicación) — ver `MovimientoService.CalcularExistencia*Async`. Evita a propósito la clase de bug de caché desincronizado.
- **Stock verificado por ubicación específica**, no solo global — una Salida no puede sacar más de lo que hay en ESA ubicación.
- **Retorna/UbicacionExterna/FechaRetornoEsperada solo en Salida** — `CK_Movimiento_RetornaSoloSalida`.
- **Una devolución (Entrada con `MovimientoOrigenId`) solo puede apuntar a una Salida con `Retorna=true`**, y la suma de lo ya devuelto + esta devolución no puede superar la cantidad original — validado en `MovimientoService.RegistrarDevolucionAsync`, no solo por CHECK (el CHECK solo cubre `CK_Movimiento_OrigenSoloEntrada`, que la devolución sea de tipo Entrada).
- **Una Salida ligada a una solicitud** (`SolicitudDetalleId`) exige que la línea ya esté aprobada y no puede superar `CantidadAprobada - CantidadEntregada`.
- **Solicitud rechazada exige `MotivoRechazo`** — `CK_Solicitud_MotivoRechazo`.
- **Numeración correlativa en dos pasos**: se inserta con un placeholder, se obtiene el `Id` autogenerado, se actualiza `NumeroMovimiento`/`NumeroSolicitud` con ese Id, segundo `SaveChanges`. No hay tabla de secuencia aparte.
- **Nunca se borra un movimiento confirmado** — todas las FK hacia `Movimiento` son `DeleteBehavior.Restrict`.
- **Errores de negocio esperables** (stock insuficiente, ubicación no encontrada, etc.) son subclases de `DominioException` con `.Status` propio — el `ManejadorGlobalDeExcepciones` las traduce a un `ProblemDetails` con ese status y el mensaje real. Cualquier otra excepción se colapsa a 500 "Error interno" genérico, nunca expone `.Message` crudo.

### Operaciones que el backend **no** expone todavía (a propósito, no un olvido)

- `ICategoriaService`, `IAreaService`, `IUbicacionService`: solo `ListarAsync` + `CrearAsync`. **No hay Actualizar ni Eliminar.** El frontend lo sabe y no muestra esos botones — si se agrega la operación acá, avisar para que el frontend la use.
- No hay campo de imagen múltiple por producto (`ImagenUrl` es una sola URL en `Producto`).
- No hay `FechaVencimiento` en `Producto` — si hace falta control de vencimiento, es una decisión de producto a tomar explícitamente (una guía SharePoint de referencia lo tenía, se descartó al adaptar el modelo real).

## Frontend

Vive en `../InventoryPlatform.Web`, un repo Git **separado** (decisión
explícita del usuario: "quiero hacer el front... en otro proyecto para que
no sea muy pesado"). Blazor Server que:

- Referencia `Inventory.Application.csproj` de este repo por path relativo
  para compartir los DTOs reales — **nunca duplicar los DTOs a mano en el
  frontend**, si un contrato cambia acá, el frontend lo ve directo.
- Consume la API vía `HttpClient` (nunca toca la base de datos ni referencia
  `Inventory.Infrastructure`).
- Su config (`appsettings.json` → `ApiBaseUrl`) apunta al puerto HTTPS de
  este backend (`https://localhost:7272/` en dev).

**Si cambian los puertos de `InventoryPlatform.Web`** (nuevo
`dotnet new`, máquina distinta, etc.), hay que actualizar
`src/Inventory.Api/appsettings.json` → `CorsOrigenesPermitidos` con los
puertos nuevos (http y https) — si no, el navegador bloquea las respuestas
aunque el request llegue bien.

## Base de datos — SQL Server, sin configurar todavía

`appsettings.json` trae una cadena de conexión a LocalDB
(`(localdb)\mssqllocaldb`), pero **LocalDB no está instalado en la máquina
de desarrollo actual** — no se corrió ninguna migración real todavía. Antes
de poder guardar datos de verdad hace falta:

1. Instalar SQL Server Express LocalDB (o levantar un contenedor Docker, o
   apuntar a una instancia ya existente — a decidir con el usuario).
2. `dotnet ef migrations add InicialV4 --project src/Inventory.Infrastructure --startup-project src/Inventory.Api` (todavía no se generó ninguna migración).
3. `dotnet ef database update` (mismo `--project`/`--startup-project`).

Verificado sin base de datos real: la API arranca igual, y cualquier
endpoint que toque el `DbContext` responde el 500 genérico controlado (no
crashea) — el pipeline de errores ya se probó de punta a punta así.

## Comandos

```bash
dotnet build                                          # toda la solución
dotnet test tests/Inventory.UnitTests                 # 8 tests, SQLite en memoria
dotnet run --project src/Inventory.Api                # API en https://localhost:7272
```

Con el SDK recién instalado en Windows, `dotnet` puede no estar en el PATH
de una sesión de shell nueva — probar con la ruta completa
(`"/c/Program Files/dotnet/dotnet.exe"` en Git Bash) si `dotnet` no se
reconoce.

## Decisiones cerradas (no reabrir sin aprobación explícita)

| # | Decisión | Motivo |
|---|---|---|
| D1 | MVC Controllers, no Minimal API | Pedido explícito del usuario ("arquitectura MVC bajo todos los estándares de desarrollo corporativo") |
| D2 | `StockActual` se calcula, nunca se cachea en `Producto` | La guía SharePoint de referencia SÍ cachea el stock (porque SharePoint no delega bien `Sum()` sobre listas grandes) — en SQL Server no hace falta, y cachear introduce una clase entera de bugs de sincronización que así se evita de raíz |
| D3 | `Ubicacion` obligatoria en los 4 tipos de movimiento, incluidos los ajustes | La guía SharePoint original (v3) solo la exigía en Entrada/Salida; la v4 la generalizó a los 4 tipos — se siguió la v4 |
| D4 | Sin `CargaRechazos` ni `ImagenesProductos` como entidades propias | Eran artefactos específicos de SharePoint (listas de log de importación e imágenes múltiples) sin equivalente real necesario en una base de datos relacional — `Producto.ImagenUrl` alcanza |
| D5 | Frontend en repo separado (`InventoryPlatform.Web`) | Pedido explícito del usuario, para no sumar peso al backend |
| D6 | `CategoriaService`/`AreaService`/`UbicacionService` sin editar/eliminar | Alcance del MVP — agregar solo si se pide explícitamente, y avisar al frontend cuando se agregue |

## Lo que NO hacer

- ❌ No dupliques los DTOs de `Inventory.Application` en el frontend — se
  comparten por referencia de proyecto.
- ❌ No agregues lógica de negocio en los Controllers ni en las
  `IEntityTypeConfiguration` — va en `Inventory.Infrastructure/Services/`.
- ❌ No caches `StockActual`/existencia en `Producto` — se calcula siempre
  desde `Movimiento` (ver D2).
- ❌ No dejes una excepción sin tipar filtrarse al cliente con su `.Message`
  crudo — o es una `DominioException` con mensaje pensado para mostrarse, o
  cae al 500 genérico.
- ❌ No agregues Editar/Eliminar a Categoría/Área/Ubicación sin que te lo
  pidan explícitamente (ver D6) — y si lo agregás, avisar que el frontend
  necesita actualizarse para usarlo.
