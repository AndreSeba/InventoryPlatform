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
| Auth | JWT bearer (bcrypt + `System.IdentityModel.Tokens.Jwt`) + autorización por permiso con policies dinámicas — ver "Autenticación y autorización" más abajo. |

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
Unidad              → CodigoUnidad (único entre activas, INMUTABLE), Nombre, Activo
                       # catálogo editable desde /unidades — reemplaza al CHECK fijo
                       # CK_Producto_Unidad IN ('UNI','CAJA','PQTS')
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
                       RegistradoPorId (FK a Usuario) + RegistradoPorNombre,
                       Motivo, FechaMovimiento
Solicitud            → NumeroSolicitud (generado post-insert: "SOL-{año}-
                       {id:D6}"), AreaId, Estado (Pendiente|Aprobada|
                       Rechazada|EntregadaParcial|Entregada|Cancelada|
                       Borrador —el flujo actual no usa Borrador, crea
                       directo en Pendiente), SolicitadoPorId (FK) +
                       SolicitadoPorNombre, AprobadoPorId (FK, null hasta
                       resolver) + AprobadoPorNombre,
                       FechaResolucion, MotivoRechazo (obligatorio si Rechazada)
SolicitudDetalle     → SolicitudId, ProductoId (único por Solicitud),
                       CantidadSolicitada, CantidadAprobada (null hasta
                       aprobar, <= Solicitada), CantidadEntregada (cache
                       acumulado desde los Movimiento de Salida ligados)
Conteo               → SesionConteo, ProductoId, UbicacionId, NumeroConteo
                       (permite reconteos 1,2,3…), CantidadContada,
                       ContadoPorId (FK a Usuario) + ContadoPorNombre, FechaConteo
                       # único por (SesionConteo, ProductoId, UbicacionId,
                       # NumeroConteo)
Auditoria            → log genérico (UsuarioId (FK a Usuario) + UsuarioNombre,
                       Entidad, EntidadId, Accion,
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

- `IUbicacionService`: solo `ListarAsync` + `CrearAsync`. **No hay Actualizar ni Eliminar.**
  El frontend lo sabe y no muestra esos botones — si se agrega la operación acá, avisar
  para que el frontend la use. (`ICategoriaService`/`IAreaService` SÍ tienen `ActualizarAsync`
  desde 2026-09-15 — ver más abajo, D6 quedó parcialmente reabierta.)
- No hay campo de imagen múltiple por producto (`ImagenUrl` es una sola URL en `Producto`).
- No hay `FechaVencimiento` en `Producto` — si hace falta control de vencimiento, es una decisión de producto a tomar explícitamente (una guía SharePoint de referencia lo tenía, se descartó al adaptar el modelo real).

### Editar/eliminar Categoría y Área (agregado 2026-09-15, D6 reabierta parcialmente)

`CategoriaService`/`AreaService` ahora tienen `ActualizarAsync(id, dto, ct)` — `dto` lleva
`Activo`, así que **"eliminar" es este mismo endpoint con `Activo: false`**, no un DELETE
real ni un endpoint aparte (a diferencia de `Producto`, que sí tiene un `DesactivarAsync`
propio porque su form de edición no expone `Activo`). Nunca se borra la fila — todas las FK
del proyecto son `DeleteBehavior.Restrict`, así que una categoría/área con productos o
solicitudes históricas tiene que poder seguir existiendo aunque esté inactiva.
Permisos nuevos: `Permisos.CategoriasEditar`/`AreasEditar`, agregados **al final** del
`Catalogo` (Id 25/26) — a propósito, insertarlos entre los `Ver`/`Crear` existentes habría
corrido el Id de todo lo que viene después y roto el seed contra una base ya migrada (ver
el comentario en `Permisos.cs`). Si se agrega un permiso nuevo alguna vez, agregarlo
siempre al final del array, nunca intercalado.
`IUbicacionService` sigue sin Editar/Eliminar — no se pidió, y su regla de "tipo" (Rack
exige Nivel, Mueble no) es más compleja que un simple toggle de `Activo` (ver "Más tipos de
Ubicación" abajo).

### Unidades de medida como catálogo (agregado 2026-09-16, pedido explícito del usuario)

Antes las unidades eran 3 valores fijos (`UNI`/`CAJA`/`PQTS`) clavados en un CHECK de la
base (`CK_Producto_Unidad`) y en un `<select>` hardcodeado del frontend. Agregar una
unidad nueva exigía tocar código y migrar. Ahora hay una tabla `Unidad` editable desde
`/unidades`, con el mismo patrón de `Categoria`/`Area`: `ListarAsync` + `CrearAsync` +
`ActualizarAsync`, y "eliminar" es `Activo = false`, nunca un DELETE.

**`Producto.UnidadMedida` sigue siendo el código en texto, NO un FK — a propósito.**
Ese mismo código va embebido en `ClaveProducto` (`"{CodigoProducto}-{CodigoUnidad}"`,
regla de la guía v4), así que ya es una clave natural denormalizada por diseño. Un FK
habría obligado a agregar `.ThenInclude(p => p.Unidad)` en cada consulta de
`SolicitudService`/`ConteoService` que hoy lee `Producto.UnidadMedida` desde entidades
ya cargadas — y olvidarse uno **no rompe la compilación**, deja la unidad en blanco en
pantalla en silencio. La integridad la garantizan dos cosas en su lugar:

- `ProductoService.ResolverUnidadAsync` exige que el código exista entre las unidades
  activas antes de crear o actualizar un producto (`UnidadNoEncontradaException`, 404).
- **`CodigoUnidad` es inmutable**: `ActualizarUnidadDto` solo lleva `Nombre` y `Activo`.
  Si se pudiera renombrar el código, los productos ya creados quedarían apuntando a uno
  que no existe. Para corregir un código: desactivar la unidad y crear otra.

Si algún día hace falta convertirlo en FK, es un cambio contenido, pero hay que revisar
esas consultas una por una.

**Seed**: `UnidadConfiguration` siembra con `HasData` las 3 unidades que antes estaban
en el CHECK (Ids 1/2/3), así los productos existentes siguen siendo válidos apenas se
aplica la migración, sin conversión de datos.

**Permisos nuevos**: `unidades.ver`/`unidades.crear`/`unidades.editar`, agregados **al
final** del `Catalogo` (Ids 27/28/29) por el mismo motivo de siempre — el Id del seed es
la posición en el array. `Operador` y `Consulta` reciben `unidades.ver` porque lo
necesitan para el formulario de producto.

**Bug arreglado de paso**: `ProductoService.ActualizarAsync` cambiaba `UnidadMedida`
pero nunca regeneraba `ClaveProducto`, así que un producto editado de UNI a CAJA quedaba
con la clave `...-UNI` mintiendo. Ahora la regenera y revalida que no choque con otro
producto activo.

### Hoja de conteo: selección explícita de productos, sin filtro de ubicación (2026-09-15/16)

`GenerarHojaConteoDto` es solo `List<int> ProductoIds` — **obligatorio, no puede venir
vacío** (`SeleccionDeProductosVaciaException`, 400). Antes tenía además `UbicacionId`
(obligar a elegir una sola ubicación por hoja) y `CategoriaId` (para generar por
categoría entera, o el catálogo completo si no venía nada); los dos se sacaron por
pedido explícito del usuario, en dos pasos:

- **Sin `UbicacionId`** (2026-09-15): el operario elige PRODUCTOS, no ubicaciones. El
  servicio arma una fila por cada `(producto, ubicación)` donde ese producto tiene stock
  ahora mismo — un producto guardado en 3 racks genera 3 filas. Mismo criterio que
  `ProductoService.ListarUbicacionesConStockAsync`, pero agregado para todos los
  productos elegidos de una sola consulta.
- **Sin `CategoriaId`, `ProductoIds` obligatorio** (2026-09-16): generar con el catálogo
  completo o una categoría entera es inmanejable en papel — un conteo físico se hace
  siempre sobre un conjunto acotado. La categoría quedó como **filtro de la lista** en el
  frontend (acota qué se ve para tildar), nunca como criterio de generación. Si alguno de
  los ids pedidos no existe o está inactivo, el servicio falla en vez de generar en
  silencio una hoja más corta que lo elegido.

**Formato del Excel:** pensado para imprimir y llenar a mano.
- Sin cuadrícula, ni en pantalla ni al imprimir (`SheetView.ShowGridLines` y
  `PageSetup.ShowGridlines` en `false`). **No volver a poner recuadros por fila** — solo
  una regla bajo el encabezado y un renglón fino (`Hair`) bajo cada fila, para escribir.
- Filas de 22 de alto y columna "Cantidad Contada" (la última, con fondo tenue) de ancho
  fijo, para que se vea dónde escribir.
- El encabezado se repite en cada página (`SetRowsToRepeatAtTop`) y hay pie con "Página X
  de Y" — sin eso, de la hoja 2 en adelante no se sabe qué columna es cuál.

⚠️ **El layout de celdas es un contrato con el importador**: `ImportarHojaConteoAsync`
lee la sesión en `B2`, el `ProductoId` en la columna 1 y el `UbicacionId` en la columna 2
(ambas ocultas, no se imprimen — la ubicación viaja por FILA, no en el encabezado, porque
un mismo producto puede aparecer en varias filas con ubicaciones distintas) y la cantidad
contada en la última columna (`ColumnasExcel` = 9), desde `FilaEncabezado + 1`. Mover una
celda rompe la importación sin error de compilación — si hay que reacomodar, tocar las
dos puntas juntas.

### Quién hizo cada cosa: FK + snapshot del nombre (2026-09-16)

Hasta esta fecha, `Movimiento.RegistradoPor`, `Solicitud.SolicitadoPor`/`AprobadoPor`,
`Conteo.ContadoPor` y `Auditoria.UsuarioId` guardaban **el nombre completo en texto
libre**, no una referencia al usuario. Salían de `UsuarioActual()` en los controllers,
que devolvía `User.Identity?.Name` con fallback al string literal `"sistema"` — un
usuario que no existe. `Auditoria.UsuarioId` era `string` y guardaba un nombre, pese a
llamarse así.

No era estrictamente una violación de 3FN (la tabla guardaba *solo* el nombre, sin el
id, así que no había dependencia transitiva dentro de la relación), pero sí **falta de
integridad referencial**: nada garantizaba que el valor fuera un usuario real, un cambio
de `NombreCompleto` partía el historial en dos sin forma de saber que era la misma
persona, dos homónimos eran indistinguibles, y "todo lo que hizo Fulano" era un match por
string en lugar de un join.

**Ahora cada una de esas tablas guarda las dos cosas:**

- `...Id` → **FK real a `Usuario`**, con `DeleteBehavior.Restrict` como el resto del
  proyecto (nunca se borra un usuario que tiene historial).
- `...Nombre` → **snapshot del nombre al momento de la operación. Es denormalización
  DELIBERADA, no un descuido: no la "arregles".** El FK dice quién fue y sigue siendo
  correcto aunque la persona cambie de nombre; el snapshot dice con qué nombre se firmó
  entonces, que es lo que una auditoría posterior necesita ver. Guardar solo el nombre
  (como antes) era lo peor de los dos mundos.

Los servicios reciben `UsuarioActuante(int Id, string Nombre)` en lugar del viejo
`string usuarioId`, y los DTOs conservan sus propiedades de nombre (`RegistradoPor`,
`SolicitadoPor`, …) alimentadas desde el snapshot, más los `...Id` nuevos — por eso el
frontend no necesitó ningún cambio.

**⚠️ Trampa del claim, documentada para no repetirla:** el JWT se emite con `sub` =
`usuario.Id` (`JwtTokenService`), pero `Program.cs` **no** configura
`MapInboundClaims = false` en `AddJwtBearer`, así que ASP.NET aplica su mapeo por defecto
y `sub` llega renombrado a `ClaimTypes.NameIdentifier`. Buscar `"sub"` devuelve `null`.
`ClaimsPrincipalExtensions.ObtenerUsuarioActuante()` lee `ClaimTypes.NameIdentifier` por
eso. **No lo "arregles" poniendo `MapInboundClaims = false`**: eso también desactiva el
mapeo de `ClaimTypes.Name` y `ClaimTypes.Role`, y rompe en silencio `User.Identity.Name`.

Si el claim falta o no parsea, la extensión **lanza excepción** en vez de caer a un
usuario inventado — el fallback `"sistema"` era parte del problema.

### Más tipos de Ubicación además de Rack/Mueble (consultado 2026-09-14, no implementado)

> El usuario preguntó si valía la pena poder agregar más tipos de ubicación a futuro.
> **Recomendación dada: no, no de forma especulativa** — sin un tercer tipo concreto no hay
> nada que implementar. Se le explicó por qué no es un cambio chico, por si lo retoma.

`TipoUbicacion` está hardcodeado con lógica propia por tipo en 3 lugares: el CHECK
`CK_Ubicacion_Nivel` (Rack exige `Nivel`, Mueble no lo lleva), `UbicacionService.CrearAsync`
(formato de `CodigoUbicacion` distinto por tipo: `Lado-Nro-Nivel` vs `M{Nro}-{Lado}`), y el
toggle de 2 opciones del frontend. Agregar un tipo nuevo no es "sumar un valor al enum" —
cada tipo necesita su propia regla de negocio (¿exige Nivel? ¿qué otro dato lleva? ¿cuál es
su formato de código?), que solo el usuario puede definir. Si en algún momento aparece un
tipo concreto, el camino correcto es convertir `TipoUbicacion` en una tabla configurable
(mismo patrón que `Categoria`/`Area`, que dejaron de ser catálogos fijos) — no agregarlo al
enum con un `if` más.

## Autenticación y autorización (agregado 2026-09-13, pedido explícito del usuario)

```
Usuario     → Id, Email (único entre activos), NombreCompleto, PasswordHash (BCrypt,
              workFactor 12), RolId, Activo, CreadoEn, UltimoLoginEn
Rol         → Id, Nombre (único entre activos), Descripcion, Activo
Permiso     → Id, Codigo ("modulo.accion", ej. "productos.crear"), Modulo, Descripcion
              # catálogo único en Inventory.Domain.Security.Permisos — agregar un
              # permiso nuevo es agregarlo ahí + nada más (ver más abajo)
RolPermiso  → RolId, PermisoId (M:N)
```

- **Login**: `POST /api/auth/login` (email+password) → `AuthService` valida con
  `BCrypt.Net.BCrypt.Verify`, arma un JWT (`JwtTokenService`) con claims `sub`, `email`,
  `name` (→ `User.Identity.Name`), `role`, y un claim `"permiso"` repetido por cada código de permiso
  del rol. Expira en `Jwt:ExpiracionMinutos` (480 = 8h, un turno laboral).
- **Autorización por permiso, no por rol**: cada acción de cada Controller lleva
  `[Authorize(Policy = Permisos.XxxYyy)]`, usando directo el código de permiso como
  nombre de policy. Funciona sin registrar ~20 `AddPolicy` en `Program.cs` gracias a
  `Inventory.Api.Security.PermissionPolicyProvider` (`IAuthorizationPolicyProvider`
  custom): cualquier nombre de policy que no exista ya se resuelve al vuelo como un
  `PermisoRequirement` que chequea `User.HasClaim("permiso", policyName)`.
  **Agregar un permiso nuevo = agregarlo a `Permisos.Catalogo` (Domain) + usar
  `[Authorize(Policy = Permisos.ElNuevo)]` donde corresponda — nunca hace falta tocar
  `Program.cs`.**
- **3 roles seedeados** (`RolPermisoConfiguration`, vía `HasData` — Ids 1/2/3 fijos):
  - **Administrador**: los ~20 permisos del catálogo completo.
  - **Operador**: ver+operar el día a día (productos.ver, movimientos.*, solicitudes.ver/
    crear/entregar, conteos.*, categorias/areas/ubicaciones.ver) — **sin** aprobar/
    rechazar solicitudes, sin gestionar catálogos ni usuarios.
  - **Consulta**: solo los `.ver` de todos los módulos.
  - RRHH puede crear roles nuevos con cualquier combinación desde `/roles` en el
    frontend (`RolesController`, requiere `roles.gestionar`) — los 3 de arriba son el
    punto de partida, no un techo.
- **Usuario admin inicial**: `Inventory.Infrastructure.Seed.DatabaseSeeder.SeedAdminInicialAsync`,
  llamado una vez al arrancar la API (`Program.cs`, con `try/catch` — si no hay base de
  datos todavía, solo loguea un warning y la API sigue arrancando igual). Se salta si
  `Usuarios` ya tiene alguna fila. Credenciales de arranque:
  `admin@inventario.local` / `Cambiar123!` — **cambiarla en cuanto haya un despliegue
  real**, es una contraseña de desarrollo a propósito.
- **`Jwt:SecretKey` en `appsettings.json` es un secreto de desarrollo** (commiteado a
  propósito, no hay gestor de secretos configurado todavía para este proyecto chico) —
  rotarlo antes de cualquier despliegue con datos reales, igual que la contraseña de
  arriba.
- **`SolicitudesEntregar` es un permiso "conceptual" del lado del frontend**: la acción
  de "Entregar" en `/solicitudes/{id}` en realidad llama a
  `POST /api/movimientos/salidas` (con `SolicitudDetalleId`), no a un endpoint propio de
  Solicitudes — el backend ya exige `movimientos.salida` para esa llamada. El frontend
  gatea el botón con `solicitudes.entregar` además, para que un rol pueda ver/aprobar
  solicitudes sin poder despachar stock si no tiene también permiso de movimientos. No es
  un hueco de seguridad (el backend igual exige `movimientos.salida`), pero si se le da
  `solicitudes.entregar` a un rol sin `movimientos.salida`, el botón se ve pero el POST
  real falla con 403 — tenerlo en cuenta al armar roles custom desde `/roles`.
- **Sin implementar todavía**: refresh token (el JWT expira a las 8h y no hay forma de
  renovarlo sin volver a loguearse — aceptable para un turno de trabajo, no para dejar la
  pestaña abierta de un día para el otro), cambio de contraseña propio (solo se puede
  crear el usuario con una contraseña inicial desde `/usuarios`, no hay endpoint de
  "cambiar mi contraseña" ni de reseteo), y 2FA. Ninguno se pidió explícitamente — no
  construir sin que el usuario lo pida.

## Pendiente: notificaciones por correo en Solicitudes (pedido 2026-09-14, sin implementar)

> El usuario pidió que el módulo de Solicitudes mande correos en 3 momentos. Se le explicó
> el diseño en el chat, pero **no se escribió ningún código todavía** — quedan varias
> decisiones suyas abiertas antes de poder implementarlo. No asumir un proveedor de correo
> ni un destinatario por defecto sin que las responda.

**Los 3 disparadores** (se enganchan en `SolicitudService`, justo después de que cada
operación se confirma en la base):
- `CrearAsync` → correo de "nueva solicitud" a quien tiene que aprobar.
- `AprobarAsync` → correo al solicitante con el formato de la solicitud (la misma tabla
  Código/Descripción/Cantidad/UME/Costo del formulario imprimible del frontend) + la fecha
  en que se van a enviar los productos.
- `RechazarAsync` → correo al solicitante con el motivo de rechazo.

**Falta un campo en el modelo:** `Solicitud` no tiene ninguna fecha de envío/entrega
estimada hoy (`FechaResolucion` es cuándo se aprobó/rechazó, no cuándo llega la
mercadería). Hace falta agregar algo como `Solicitud.FechaEnvioEstimada` (date, nullable),
cargado en el momento de aprobar — el formulario de "Aprobar solicitud" del frontend
necesita un campo de fecha nuevo al lado de las cantidades, que viaje en
`AprobarSolicitudDto`. Sin este campo no hay ninguna fecha real que mandar en el correo.

**Decisiones del usuario, todavía sin responder:**
1. **Proveedor de correo** — SMTP (Gmail/Outlook con contraseña de aplicación) vs. una API
   transaccional (Resend/Brevo/Mailgun/SendGrid). Para .NET, la librería a usar es
   **MailKit** (no `System.Net.Mail`, obsoleta), funciona con SMTP genérico o el modo SMTP
   de cualquiera de las APIs. Las credenciales van en `appsettings` + `dotnet user-secrets`
   en dev, nunca hardcodeadas ni commiteadas — mismo patrón que `Jwt:SecretKey`.
2. **Destinatario del correo de "nueva solicitud"** — hoy no hay ningún concepto de "a
   quién le llega esto". Opciones: un email fijo en configuración, o el email de todos los
   `Usuario` con el permiso `SolicitudesAprobar` (ya existen en la base, no hace falta
   agregar nada para esa segunda opción).
3. **Formato del correo de aprobación** — la tabla en el cuerpo del correo en HTML (simple,
   reusa el mismo layout del formulario imprimible) vs. un PDF real adjunto (más prolijo,
   pero suma una librería de generación de PDF, ej. QuestPDF — más trabajo).

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

## Base de datos — SQL Server, sin aplicar todavía

`appsettings.json` trae una cadena de conexión a LocalDB
(`(localdb)\mssqllocaldb`), pero **LocalDB no está instalado en la máquina
de desarrollo actual** — ninguna migración se aplicó contra una base real
todavía. Antes de poder guardar datos de verdad hace falta:

1. Instalar SQL Server Express LocalDB (o levantar un contenedor Docker, o
   apuntar a una instancia ya existente — a decidir con el usuario).
2. `dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api`.

Verificado sin base de datos real: la API arranca igual, y cualquier
endpoint que toque el `DbContext` responde el 500 genérico controlado (no
crashea) — el pipeline de errores ya se probó de punta a punta así.

⚠️ **Migración pendiente de generar (unificación de sesiones, 2026-09-16)**: ya existen
4 migraciones (`InicialRbac` … `AgregarRolSolicitanteYPermisoInicio`), pero ninguna
cubre el catálogo `Unidad` ni las columnas `...Id`/`...Nombre` (FK + snapshot) agregadas
en "Unidades de medida como catálogo" y "Quién hizo cada cosa" más arriba — esos cambios
de modelo se escribieron en una sesión que nunca corrió `dotnet ef migrations add` (sin
SDK de .NET en este entorno tampoco se pudo generar acá). Antes de `database update`,
generar la migración que falta:
`dotnet ef migrations add AgregarUnidadYUsuarioActuante --project src/Inventory.Infrastructure --startup-project src/Inventory.Api`,
revisar el `.cs` generado (altas de la tabla `Unidad` + columnas nuevas con sus FK en
`Movimiento`/`Solicitud`/`Conteo`/`Auditoria`) antes de aplicarla.

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
| D6 | `CategoriaService`/`AreaService`/`UbicacionService` sin editar/eliminar | Alcance del MVP — agregar solo si se pide explícitamente, y avisar al frontend cuando se agregue. **Reabierta parcialmente 2026-09-15**: se pidió explícitamente para Categoría y Área, ya implementado (ver "Editar/eliminar Categoría y Área" arriba) — `UbicacionService` sigue sin pedirse, sigue sin Editar/Eliminar |
| D7 | Autorización por permiso (policy dinámica = código de permiso), no solo por rol fijo | Pedido explícito del usuario ("usuario con roles y permisos") — con solo 2-3 roles hardcodeados (como el RBAC de controAsistencia) no se puede armar una combinación custom sin tocar código; con permisos + roles editables desde `/roles`, sí |
| D8 | JWT en vez de cookie de sesión | El frontend y el backend son procesos/orígenes distintos (Blazor Server llama a la API por HTTP, no comparten el mismo dominio) — un JWT que el frontend guarda del lado del servidor (nunca en el navegador) y manda como Bearer es más simple acá que una cookie cross-origin |

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
- ❌ No agregues un endpoint nuevo sin `[Authorize]` — todos los Controllers son
  `[Authorize]` a nivel clase, con `[Authorize(Policy = Permisos.Xxx)]` por acción.
  Si un endpoint necesita ser público, usar `[AllowAnonymous]` explícito (como
  `AuthController.Login`), nunca "olvidar" el atributo.
- ❌ No registres una policy nueva a mano en `Program.cs` para un permiso nuevo — agregalo
  a `Inventory.Domain.Security.Permisos.Catalogo` y listo, `PermissionPolicyProvider` la
  resuelve sola.
- ❌ No commitees `Jwt:SecretKey` real ni cambies la contraseña admin de dev en el
  `appsettings.json` — son valores de desarrollo a propósito, documentados como "cambiar
  antes de un despliegue real", no secretos de producción.
