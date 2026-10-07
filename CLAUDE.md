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
SesionConteo         → Codigo (generado: "CONT-{año}-{id:D6}", provisional único
                       "TMP-{guid}" hasta tener Id), PaisId, Nombre, Notas,
                       Estado (EnCurso|Cerrado|Cancelado), CreadoPorId + Nombre,
                       CerradoPorId + Nombre + FechaCierre (también al cancelar),
                       MotivoCancelacion, ConteoOrigenId (reconteo)
SesionConteoLinea    → (SesionConteoId, ProductoId, UbicacionId) único,
                       ExistenciaSistema (FOTO FIJA al crear), CantidadContada
                       (null = sin contar; 0 = se contó y no había nada),
                       ContadoPorId + Nombre, FechaConteo
SesionConteoEvidencia→ NombreArchivo, ContentType, TamanoBytes, Datos (varbinary,
                       tabla aparte a propósito), SubidoPorId + Nombre
Auditoria            → log genérico (UsuarioId (FK a Usuario) + UsuarioNombre,
                       PaisId (FK a Pais, país de la SESIÓN del actor — ver
                       "Auditoría completa" más abajo), Entidad, EntidadId,
                       Accion, ValorAnterior, ValorNuevo, Motivo, CorrelationId)
                       — escrito por TODOS los servicios que mutan datos, vía
                       IAuditoriaService (ver sección "Auditoría completa").
```

### Reglas de negocio críticas (todas con CHECK constraint de respaldo en la BD, vía `HasCheckConstraint` en las Configurations — no solo validadas en C#)

- **`CantidadEfectiva` con signo obligatorio**: Entrada/AjustePositivo → `+Cantidad`; Salida/AjusteNegativo → `-Cantidad`. `CK_Movimiento_Signo`.
- **Stock se calcula, nunca se cachea**: `SUM(CantidadEfectiva)` por producto (global) o por (producto, ubicación) — ver `MovimientoService.CalcularExistencia*Async`. Evita a propósito la clase de bug de caché desincronizado.
- **Stock verificado por ubicación específica**, no solo global — una Salida no puede sacar más de lo que hay en ESA ubicación.
- **Retorna/UbicacionExterna/FechaRetornoEsperada solo en Salida** — `CK_Movimiento_RetornaSoloSalida`.
- **Una devolución (Entrada con `MovimientoOrigenId`) solo puede apuntar a una Salida con `Retorna=true`**, y la suma de lo ya devuelto + esta devolución no puede superar la cantidad original — validado en `MovimientoService.RegistrarDevolucionAsync`, no solo por CHECK (el CHECK solo cubre `CK_Movimiento_OrigenSoloEntrada`, que la devolución sea de tipo Entrada).
- **Una Salida ligada a una solicitud** (`SolicitudDetalleId`) exige que la línea ya esté aprobada y no puede superar `CantidadAprobada - CantidadEntregada`.
- **Solicitud rechazada exige `MotivoRechazo`** — `CK_Solicitud_MotivoRechazo`.
- **Numeración correlativa en dos pasos**: se inserta con un placeholder, se obtiene el `Id` autogenerado, se actualiza `NumeroMovimiento`/`NumeroSolicitud`/`Codigo` con ese Id, segundo `SaveChanges`. No hay tabla de secuencia aparte. **El placeholder es ÚNICO por alta (`"TMP-" + Guid`), nunca una constante**: con `"PENDIENTE"` fijo, dos altas simultáneas chocaban contra el índice único (1 de cada 2 solicitudes daba 500 con 40 usuarios).
- **Nunca se borra un movimiento confirmado** — todas las FK hacia `Movimiento` son `DeleteBehavior.Restrict`.
- **Errores de negocio esperables** (stock insuficiente, ubicación no encontrada, etc.) son subclases de `DominioException` con `.Status` propio — el `ManejadorGlobalDeExcepciones` las traduce a un `ProblemDetails` con ese status y el mensaje real. Cualquier otra excepción se colapsa a 500 "Error interno" genérico, nunca expone `.Message` crudo.

### Operaciones que el backend **no** expone todavía (a propósito, no un olvido)

- `IUbicacionService`: solo `ListarAsync` + `CrearAsync`. **No hay Actualizar ni Eliminar.**
  El frontend lo sabe y no muestra esos botones — si se agrega la operación acá, avisar
  para que el frontend la use. (`ICategoriaService`/`IAreaService` SÍ tienen `ActualizarAsync`
  desde 2026-09-15 — ver más abajo, D6 quedó parcialmente reabierta.)
- No hay campo de imagen múltiple por producto (`ImagenUrl` es una sola URL en `Producto`).
- No hay `FechaVencimiento` en `Producto` — si hace falta control de vencimiento, es una decisión de producto a tomar explícitamente (una guía SharePoint de referencia lo tenía, se descartó al adaptar el modelo real).

### La foto del producto vive en su propia tabla (2026-10-02)

`ProductoImagen` (PK = `ProductoId`, `Datos`, `ContentType`) reemplaza a las columnas
`Producto.ImagenData`/`ImagenContentType`. **No las vuelvas a meter en `Producto`**: casi
toda consulta que carga un `Producto` (Movimientos, Solicitudes, Conteos, listados) lo hace
por nombre/código, y con la foto adentro cada una arrastraba ~250 KB por producto desde SQL
(con ~500 productos, ~125 MB por listado → pantallas de "Cargando…" de varios segundos).
Se midió: `/api/productos` pasó de 0,85 s a ~0,02 s con 82 productos, misma respuesta.

- `Producto.TieneImagen` (bool persistido) dice si hay foto sin leerla; lo mantiene
  `ProductoService` al crear/actualizar. `ProductoDto.ImagenUrl` sale de ahí.
- Los bytes solo se leen en `ObtenerImagenAsync` (`GET /api/productos/{id}/imagen`) y se
  escriben en `CrearAsync`/`ActualizarAsync`. Nunca hagas `Include(p => p.Imagen)` en un listado.
- Migración `SepararImagenDeProducto`: **copia** las fotos a la tabla nueva antes de borrar
  las columnas (EF la generó como DropColumn + CreateTable, que las perdía — se reescribió).
  Mueve todas las fotos de la base, así que conviene tener un backup antes de aplicarla.

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

### Conteo físico: sesión con trazabilidad y evidencia (2026-10-02)

Antes un "conteo" era solo un texto `CONTEO-fecha` que nacía al bajar el Excel: sin estado,
sin responsable, sin respaldo. Ahora es una entidad (`SesionConteo`) con ciclo de vida:

- **Crear** (`POST /api/conteos`, `CrearConteoDto(Nombre, Notas, ProductoIds)`): los productos
  se eligen **uno por uno y nunca vacío** (`SeleccionDeProductosVaciaException`) — jamás el
  catálogo ni una categoría entera, un conteo físico es sobre un conjunto acotado (hasta
  1000). El servicio arma una línea por cada `(producto, ubicación)` con stock > 0 y guarda
  su existencia como **foto fija** (`ExistenciaSistema`): la diferencia se calcula contra ella,
  no contra el stock "vivo", para que movimientos posteriores no la distorsionen.
- **Cargar cantidades**: grilla (`PUT /{id}/cantidades`) o Excel (`GET /{id}/hoja` →
  `POST /{id}/importar`). Línea sin contar (`null`) ≠ `0`. Re-importar corrige.
- **Evidencia** (`POST/GET/DELETE /{id}/evidencias`): foto JPG/PNG/WEBP, PDF o `.xlsx`, hasta
  10 archivos de 10 MB. **El tipo se decide por los primeros bytes**, no por el Content-Type
  ni la extensión (un archivo falso con extensión `.png` se rechaza). Se sirve CON
  autorización (`conteos.ver`), a diferencia de la foto del producto.
- **Cerrar** (`POST /{id}/cerrar`): exige TODO contado + al menos 1 evidencia. Es **un solo
  UPDATE atómico** (`ExecuteUpdateAsync` con las condiciones en el `Where`), no "chequear y
  después cerrar": así dos cierres simultáneos o un cierre contra una carga de cantidades no
  se pisan. Cerrado/Cancelado son inmutables.
- **Cancelar** (`POST /{id}/cancelar`, motivo obligatorio) y **reconteo** (`POST /{id}/reconteo`):
  solo desde un conteo CERRADO con diferencias; nuevo conteo con solo las líneas que difieren y
  la existencia medida de nuevo. Un índice único filtrado impide dos reconteos abiertos del
  mismo origen.
- **`EnCursoAsync`** (ConteoService): toda modificación pasa por ahí — transacción + un UPDATE
  no-op que toma el candado de la fila solo si sigue EnCurso. Sin eso se podría guardar una
  cantidad o evidencia DESPUÉS del cierre.
- **El conteo NO ajusta stock** (decisión del usuario, "solo informe"): las diferencias quedan
  a la vista y los ajustes se registran aparte, desde Movimientos.
- Permisos: `conteos.ver` (listar/ver/bajar evidencia) y `conteos.registrar` (todo lo demás).
  No se agregó permiso nuevo.
- Auditoría: entidad `SesionConteo`, id = código; acciones Crear, Cargar cantidades,
  Adjuntar/Eliminar evidencia, Cerrar, Cancelar.
- Migración `ConteoConSesionYEvidencia`: **convierte los conteos viejos** (tabla `Conteo`) en
  conteos CERRADOS "sin evidencia" antes de borrarla. Conviene un backup antes de aplicarla.

⚠️ **El layout de celdas del Excel es un contrato con el importador** (`LeerHoja`): sesión en
`B2` (debe coincidir con el código del conteo — así no se importa la hoja de otro), `ProductoId`
en la columna 1 y `UbicacionId` en la 2 (ocultas, la ubicación viaja por FILA), cantidad en la
última columna (`ColumnasExcel` = 9) desde `FilaEncabezado + 1`. Se exige un entero exacto
(3.5 se rechaza, no se redondea). Mover una celda rompe la importación sin error de compilación.
Formato pensado para imprimir y llenar a mano: sin cuadrícula (`ShowGridLines` y
`PageSetup.ShowGridlines` en `false`), renglón fino bajo cada fila, encabezado repetido por página
y pie "Página X de Y". **No volver a poner recuadros por fila.**

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

### Auditoría completa, estilo SAP (agregado 2026-09-22, pedido explícito del usuario)

> El usuario pidió "mejorá la tabla de auditoría para que funcione al 100%, quiero
> algo parecido a lo que hace SAP" — eligió expresamente el alcance máximo (todo lo
> que modifica datos, no solo lo crítico) y el detalle campo por campo (no
> antes/después en crudo).

Antes de esto, `Auditoria` solo la escribía `ProductoService` (crear/actualizar/
desactivar), a mano, con `_db.Auditorias.Add(...)` inline y sin país. Ahora:

- **`IAuditoriaService`/`AuditoriaService`** (`Inventory.Application/Interfaces`,
  `Inventory.Infrastructure/Services`) es el punto único de escritura/lectura — ningún
  servicio toca `_db.Auditorias` directo. Dos métodos de escritura:
  - `Capturar(object snapshot)` — serializa a JSON un objeto anónimo con los campos de
    negocio relevantes de la entidad (armado a mano en cada servicio, **nunca la
    entidad de EF completa ni un DTO con binarios** — evita volcar navegaciones o,
    peor, bytes de imagen en base64 dentro de la auditoría).
  - `RegistrarAsync(entidad, entidadId, accion, valorAnteriorJson, valorNuevoJson,
    paisId, usuario, motivo, ct)` — hace su propio `SaveChangesAsync`, así el registro
    queda escrito pase lo que pase después en el método que llama.
- **Diff campo por campo, no antes/después en crudo**: cada servicio llama `Capturar`
  con la **misma forma de objeto** antes y después de mutar (mismos nombres de
  campo), para que el frontend pueda comparar clave por clave. En un alta
  (`ValorAnterior = null`) todos los campos de `ValorNuevo` se muestran como "creado".
- **`PaisId` en `Auditoria`**: no es una propiedad natural de la entidad auditada —
  es el país de la SESIÓN de quien actuó (`User.ObtenerPaisId()` en el controller),
  mismo criterio que cualquier otro servicio país-scoped. Así el listado se filtra
  igual que todo el resto del sistema, sin que un admin de un país vea la auditoría
  de otro. `PaisService.CrearAsync`/`ActualizarAsync` reciben este `paisId` **solo**
  para esto — `Pais` en sí no tiene `PaisId` propio (es la dimensión).
- **Quién escribe qué, por servicio**:
  - `ProductoService`: Crear/Actualizar/Desactivar (refactorizado de su código previo
    inline — antes auditaba el DTO entrante, ahora audita un snapshot simétrico de la
    entidad; nunca los bytes de `ImagenData`, solo `TieneImagen`).
  - `MovimientoService`: `RegistrarEntrada`/`RegistrarSalida`/`RegistrarAjustePositivo`/
    `RegistrarAjusteNegativo`/`RegistrarDevolucion` — solo "alta" (un Movimiento nunca
    se edita ni se borra, ver arriba), `ValorAnterior` siempre null.
  - `SolicitudService`: Crear/Aprobar/Rechazar (el estado + `CantidadAprobada` por
    línea, antes/después).
  - `ConteoService`: Crear/Cargar cantidades/Adjuntar y Eliminar evidencia/Cerrar/Cancelar
    (entidad `SesionConteo`, ver "Conteo físico"). Una importación de Excel es UNA fila de
    auditoría, no una por línea.
  - `UsuarioService`: Crear/Actualizar — **nunca `PasswordHash`** en el snapshot.
  - `RolService`: Crear/Actualizar — el snapshot guarda `PermisoId` (no el código),
    para no depender de que `RolPermiso.Permiso` esté incluido en la query.
  - `CategoriaService`/`AreaService`/`UnidadService`/`AlmacenService`: Crear/Actualizar.
  - `UbicacionService`: solo Crear (no tiene Actualizar, ver D6).
  - `PaisService`: Crear/Actualizar.
- **`CrearAsync`/`ActualizarAsync` de Categoría/Área/Ubicación/Unidad/Almacén/Usuario/
  Rol/País ahora reciben `UsuarioActuante usuario`** (antes no lo recibían — no hacía
  falta hasta que tuvieron que auditar quién actuó). Cambio en cascada: interfaz +
  implementación + Controller (`User.ObtenerUsuarioActuante()`) en los 8 servicios.
  **`UsuarioService`**: ojo, la entidad `Usuario` que se crea/edita ya se llamaba
  `usuario` en el código — se renombró a `nuevoUsuario`/`entidad` para no chocar con
  el parámetro `UsuarioActuante usuario` nuevo.
- **Endpoint**: `GET /api/auditoria` (filtros `Entidad`/`Accion`/`UsuarioId`/`Desde`/
  `Hasta` por query string, sin paginado server-side — mismo criterio que Movimientos/
  Solicitudes: devuelve la lista filtrada completa, el frontend pagina con
  `<Pager/>`) + `GET /api/auditoria/catalogo` (entidades/acciones REALMENTE presentes
  en la tabla para ese país, alimenta los `<select>` de filtro del frontend sin
  hardcodear una lista que se desactualizaría cada vez que se audite un módulo nuevo).
  Ambos con `[Authorize(Policy = Permisos.AuditoriaVer)]` — permiso nuevo, agregado al
  final del catálogo por el motivo de siempre (ver comentario en `Permisos.cs`).
- **Migración** `AgregarAuditoriaCompleta`: agrega `PaisId` a `Auditoria` (con el
  cuidado de siempre — `defaultValue: 1`, no el `0` que genera EF por default, para
  no dejar las filas históricas de Producto con un país inválido) + el permiso nuevo
  + su `RolPermiso` para Administrador Bolivia y Perú (se generó solo: `Permisos.Catalogo`
  es la fuente de `PermisoConfiguration`/`RolPermisoConfiguration`, así que agregar un
  permiso al final del catálogo y correr `migrations add` ya arma el `InsertData`
  correcto sin tocar nada más — confirmado con este mismo cambio).
- **Frontend**: `/auditoria` (`Components/Pages/Auditoria/Index.razor`), rail link en
  "Administración" gateado por `Permisos.AuditoriaVer`. El diff campo por campo se
  arma en el cliente: parsea `ValorAnterior`/`ValorNuevo` con `JsonDocument`, compara
  clave por clave, y solo muestra las que cambiaron (fila expandible "Ver cambios" por
  registro, no una columna más en la tabla principal — con `~10` campos por entidad
  no entraría).

**Importante para el próximo servicio que mute datos**: seguir el mismo patrón —
inyectar `IAuditoriaService`, escribir un `Snapshot(Entidad e) => new { ... }` privado
con los campos de negocio (nunca navegaciones, nunca binarios/hashes), llamarlo
antes y después de mutar, y `RegistrarAsync` después del `SaveChangesAsync` que
persiste el cambio real.

### Endurecimiento tras las pruebas multiusuario (2026-10-02)

Se simularon 12 roles (varios inventados), 36 usuarios y 40 usuarios virtuales en simultáneo contra una
base descartable; salieron 54 hallazgos. Estas reglas son el resultado — **no las deshagas**:

- **Candado por producto en TODO movimiento** (`BloquearProductoAsync`): un `UPDATE` que no cambia nada
  pero toma el candado exclusivo de la fila `Producto` hasta el commit, tomado ANTES de leer el stock.
  Sin eso el chequeo de stock era "leer y después insertar": 40 salidas simultáneas aceptaban de más y
  el stock quedaba negativo. Aplica a entradas, salidas, ajustes y devoluciones.
- **Entregas de una solicitud**: además se bloquea la fila `Solicitud` (orden fijo producto → solicitud,
  para que no haya ciclos de bloqueo). Se exige que el producto coincida con el de la línea y que el
  tipo de la solicitud coincida con el del movimiento.
- **Transiciones de estado atómicas**: aprobar/rechazar una solicitud y cerrar/cancelar un conteo son un
  único `UPDATE ... WHERE Estado = X` (`ExecuteUpdateAsync`); si afecta 0 filas, otro ya lo resolvió.
  Aprobar exige decidir **todas** las líneas (0 también es una decisión) y al menos una > 0.
- **Código de producto**: correlativo = máximo existente con ese prefijo + 1 (no la cantidad), prefijo
  con solo letras/números, y la alta se serializa por país con `sp_getapplock`. Antes dos categorías con
  las mismas 4 primeras letras (`MKT-MABEL`/`MKT-CAFE`) se pisaban.
- **Sesión validada contra el estado actual** (`SesionUsuarioCache`, `OnTokenValidated` en `Program.cs`):
  un usuario desactivado, o con permisos distintos a los del token, recibe 401 y debe volver a
  loguearse. Caché de 30 s; `UsuarioService`/`RolService` la invalidan al instante.
- **Login con bloqueo**: 5 fallos por email en 10 minutos → 429 (`LoginBloqueadoException`). Se cuenta
  también para emails inexistentes. Está en memoria: con varias instancias de la API habría que moverlo
  a una caché compartida.
- **Validación de entrada → 400, nunca 500**: `Validacion.Texto`/`TextoOpcional` y `ValidacionException`.
  Textos obligatorios sin espacios, con tope = largo de la columna; cantidades 1..1.000.000.000; préstamo
  con destino y fecha futura; ajuste con motivo; contraseña 8–72 con letra y número; email con formato.
  Red de seguridad en `ManejadorGlobalDeExcepciones`: un error de SQL Server por truncado/CHECK/clave
  duplicada/deadlock se devuelve como 400/409.
- `GET /api/productos/{id}/ubicaciones` filtra por el país del producto (antes filtraba nada).
- `GET /api/roles` y `/roles/permisos-disponibles` exigen `usuarios.gestionar` **o** `roles.gestionar`
  (policy `"a|b"` = alguno de los dos, ver `PermisoRequirement`).
- Las imágenes se validan por sus primeros bytes, no solo por el Content-Type declarado.
- Nadie puede desactivar su propia cuenta.
- Al arrancar, la API avisa por log si hay migraciones sin aplicar.

**Decisiones de producto que NO se tomaron** (quedaron como estaban; ver el informe de pruebas): se acepta
una fecha de vencimiento ya pasada y la foto del producto se sirve sin autenticación. Aprobar la propia
solicitud y la entrada libre sin solicitud YA están resueltas por `ControlesOptions` (ver más abajo).

### Controles de trazabilidad (2026-10-05) — `ControlesOptions`

Respuesta al reto "puedo pedirle de favor a Operaciones que saque el material y cargarlo después": el
sistema no puede impedir un traspaso por fuera, pero sí hacer que dentro de él no se pueda mover stock
sin respaldo y que la irregularidad quede a la vista. Sección `Controles` de `appsettings.json` (por
defecto TODO estricto; `ControlesOptions.Permisivo` cuando un servicio se arma sin opciones, que es lo
que hacen las pruebas unitarias):

- `ExigirSolicitudEnMovimientos`: toda entrada y salida se registra contra una línea de solicitud
  aprobada (`MovimientoService.ExigirSolicitud`). Las correcciones se hacen con un ajuste (motivo
  obligatorio, auditado). Ajustes y devoluciones no se ven afectados.
- `SeparacionDeFunciones`: quien pidió el material no lo aprueba ni lo rechaza (`SolicitudService`) ni
  registra su entrega (`MovimientoService.ValidarDetalleParaEntregaAsync`) → `SeparacionDeFuncionesException`
  (403). El circuito completo exige tres personas distintas; con un equipo mínimo se puede apagar, sabiendo
  que se pierde el control. La revisión de accesos usa el mismo interruptor para "nadie revisa su propia cuenta".
- `RevisionTodosCadaDias` (90), `RevisionAdministradoresCadaDias` (30), `DiasSinActividad` (90): ver
  "Revisión de accesos".

### Correo saliente (2026-10-07)

Los avisos de la campanita también pueden salir por correo (MailKit/SMTP). **Apagado por defecto** (`Correo:Habilitado = false`).
Diseño: `NotificacionService` encola un `MensajeCorreo` (`ICorreoSaliente`/`ColaCorreo`) SOLO para las noticias personales
recién creadas — pendientes (por aprobar, por entregar, devolución por recibir), resultados (aprobada, rechazada, entregada,
devolución recibida) y préstamo propio vencido/por vencer —, nunca para las alertas agregadas de inventario/control, y solo
una vez por noticia (se envía al crearse, no al actualizarse). `EnvioCorreoHostedService` (API) vacía la cola por SMTP con un
reintento y sin romper nada si falla; `SincronizacionNotificacionesHostedService` revisa las novedades cada
`SincronizarCadaSegundos` (45) para avisar aunque nadie tenga el sistema abierto.
- Configuración (sección `Correo`): `Host`/`Puerto`/`Seguridad` (StartTls | Ssl | Ninguna), `Usuario`, `Clave`, `RemitenteNombre`, `UrlBaseWeb` (botón
  «Abrir en el sistema»). **La clave NUNCA va en appsettings**: `dotnet user-secrets set "Correo:Clave" "..."` (proyecto
  `Inventory.Api`, `UserSecretsId` ya definido) o la variable de entorno `Correo__Clave`.
- `DestinatarioDePrueba`: si tiene valor, TODOS los correos van a esa dirección (con «[Para Fulano]» en el asunto y un aviso en el
  cuerpo). Imprescindible mientras los usuarios tengan emails ficticios (`@inventario.local`, como la base de demostración);
  vaciarlo para que cada persona reciba el suyo.
- `POST api/correo/prueba` (permiso `usuarios.gestionar`) manda un correo de prueba y devuelve el error de SMTP si falla.
- Gmail: hace falta verificación en 2 pasos + «contraseña de aplicación»; SMTP `smtp.gmail.com:587` STARTTLS. Una red corporativa
  puede bloquear el puerto 587.
- Probado con pruebas unitarias (qué se encola, a quién, una sola vez); el envío SMTP real depende de las credenciales de cada
  entorno y NO se probó contra un servidor real.



`Notificacion` (una fila por PERSONA, con leída/no leída), `NotificacionService`, `api/notificaciones`
(`GET` lista + contador, `POST {id}/leida`, `POST leidas`; sin policy de permiso: cada persona ve las suyas,
usuario y país salen del token). Decisión del usuario: **todo guardado** y las cuatro categorías.

- **Se generan en UN solo lugar, desde el estado real** — no hay ganchos repartidos en los servicios de negocio
  (así no se puede romper un flujo por una notificación ni olvidar un caso). Cada sincronización calcula «qué
  debería estar notificado ahora», lo compara con lo guardado por `Clave` y crea / actualiza / resuelve la diferencia.
  `Clave` = identidad por persona (`sol-pend:42`, `inv-sin-stock`, `pres-mora:17`…); índice único filtrado
  (persona + clave, solo no resueltas) contra duplicados por carrera.
- **Frenos**: sincronización «rápida» (solicitudes, avisos de devolución, préstamos) cada 30 s por país; «lenta»
  (existencias, vencimientos, conteos, cuentas, revisión de accesos) cada 10 min. Se dispara al consultar la
  campanita (`ListarAsync`); el freno es `IMemoryCache` (con varias instancias de la API cada una calcularía su
  turno: es seguro por el índice único, solo repite trabajo). Una falla al sincronizar se registra y nunca rompe la lista.
- **Categorías**: `Pendiente` (por aprobar → `solicitudes.aprobar`; por entregar/recibir → `solicitudes.entregar`;
  devolución por recibir → `movimientos.devolucion`; se RESUELVEN solas), `Resultado` (tu solicitud aprobada /
  rechazada / entregada, tu devolución recibida; solo de la última semana, quedan como historial 30 días),
  `Inventario` (sin stock, bajo mínimo, lotes vencidos / por vencer → `movimientos.ver`; préstamo vencido o por
  vencer a su solicitante y el total en mora a `movimientos.devolucion`), `Control` (revisión de accesos vencida →
  `accesos.revisar`; conteos abiertos > 7 días → `conteos.registrar`; cuentas activas que nunca ingresaron en
  14 días → `usuarios.gestionar`).
- **Reglas**: quien pidió algo no recibe el «por aprobar/entregar» de lo suyo; una notificación AGREGADA («N productos
  sin stock», `Valor`) vuelve a quedar sin leer y sube en la lista solo si el número SUBE; las resueltas de hace
  más de un mes y los resultados viejos se borran en la sincronización lenta.
- **Trampa**: `AplicarAsync` descarta el rastreo de `Notificacion` antes de leer, porque «marcar leída» es un
  `ExecuteUpdate` (no pasa por el rastreo) y una lectura previa del mismo contexto devolvería valores viejos.
- Al desplegar por primera vez cada persona recibe de golpe los resultados de la última semana (aprobadas,
  entregadas…): es esperado, no un error.
- **Tiempo límite tras leer** (`Notificaciones:HorasVisiblesTrasLeer`, 24 h): una notificación leída (o abierta con un clic)
  sale de la campanita pasado ese tiempo, EXCEPTO las de categoría `Pendiente` (son una tarea: siguen hasta resolverse).
  Solo se oculta en la consulta, la fila NO se borra: la condición sigue vigente y, si se borrara, la sincronización la
  volvería a crear como nueva y sin leer. Una agregada que empeora (el número sube) vuelve a aparecer sin leer.
- Migración `Notificaciones`. 11 pruebas unitarias (`NotificacionTests`): pendientes que se cierran solas,
  resultados, leídas por persona, no duplicar, agregadas, préstamos, control.

### Devoluciones con aviso del solicitante (2026-10-06)

Antes el operario registraba la devolución de un préstamo solo (`movimientos.devolucion`), sin que quien pidió
el material interviniera ni supiera nada. Ahora la devolución tiene dos puntas (decisión del usuario: «el
solicitante avisa, el operario solo registra la entrada»; sin aviso no hay devolución):

- **Aviso** (`AvisoDevolucion`, `DevolucionService`, `api/devoluciones`): quien pidió el material (el
  `SolicitadoPorId` de la solicitud de la que salió el préstamo) avisa «voy a devolver N». Estados Pendiente /
  Recibido / Cancelado, código `DEV-{año}-{id}`. La suma de avisos pendientes + lo ya devuelto no puede superar
  lo prestado (se serializa con el candado del producto). Solo el solicitante avisa y solo él cancela (con motivo).
- **Recepción**: `RegistrarDevolucionDto` ganó `AvisoDevolucionId`. El operario registra la entrada contra el
  aviso: puede recibir MENOS de lo avisado (el resto sigue como préstamo pendiente y se puede volver a avisar),
  nunca más. El aviso pasa a Recibido en la MISMA transacción, ligado a la entrada creada (transición atómica;
  si el solicitante lo cancela justo en ese instante, una de las dos gana y la otra falla).
- **Separación de funciones**: quien avisó no registra la recepción de su propio aviso (`SeparacionDeFunciones`).
- **Control `ExigirAvisoEnDevoluciones`** (default true): una devolución de un préstamo hecho contra una
  solicitud EXIGE aviso. Los préstamos sin solicitud (salidas libres de antes de los controles) no tienen a quién
  avisar y se siguen devolviendo directo; con ese préstamo el aviso se rechaza (403).
- **Visibilidad**: `GET api/devoluciones/prestamos` (todos, con quién los tiene, mora y avisos en camino, para
  `movimientos.ver`), `prestamos/mios` y `avisos/mios` (solo los propios), `avisos` (todos, para el operario).
  El solicitante ve qué material tiene prestado a su nombre.
- Permiso `devoluciones.avisar` (al final del catálogo, Id 39): lo reciben Administrador, Operador y Solicitante
  (también por migración en los países ya creados). `PrestamoDto`/`AvisoDevolucionDto` son los contratos nuevos;
  `GET api/movimientos/prestamos-pendientes` sigue existiendo (lo usa el contador de Inicio).
- Auditoría: entidad `AvisoDevolucion`, acciones Avisar / Cancelar / Recibir (además de `RegistrarDevolucion`
  sobre el movimiento). Migración `AvisoDeDevolucion`.
- Probado: 12 pruebas unitarias (`DevolucionTests`) y el circuito completo contra SQL Server con
  `scratchpad/prueba_devoluciones.py` (33 verificaciones: permisos, aviso por tramos, cancelación, recepción
  parcial, no recibir dos veces, auditoría).

### Módulos planificados: sección `Funciones` (2026-10-07)

La **revisión de accesos está construida pero APAGADA** (`Funciones:RevisionAccesos = false` en `appsettings.json`,
decisión del usuario: no se muestra en la reunión de hoy, queda «planificada»). Apagada: la campanita no genera
avisos de revisión de accesos (los ya guardados se resuelven solos en la siguiente sincronización) y el frontend
la muestra como «Planificado». Para habilitarla: poner `RevisionAccesos: true` en la API **y** en el frontend
(`InventoryPlatform.Web/appsettings.json`, mismo nombre). El resto de la funcionalidad (API `api/revisiones-acceso`,
permiso `accesos.revisar`, tablas) sigue intacta. Un módulo nuevo que se quiera mostrar «planificado» sigue el mismo
patrón: opción booleana en `FuncionesOptions` + chequeo en el servicio que genere avisos.

### Revisión periódica de accesos (2026-10-06)

Control de TI "gestión de accesos / cuentas privilegiadas": quien tiene `accesos.revisar` abre una
**campaña** (`RevisionAcceso` + `RevisionAccesoLinea`, `RevisionAccesoService`, `api/revisiones-acceso`),
decide por cada cuenta y la cierra. Trimestral para todas las cuentas y mensual para las administradoras.

- **Abrir**: foto de las cuentas ACTIVAS del país (nombre, email, rol, último ingreso, alta). Alcance
  `Todos` o `Administradores` (= rol con `usuarios.gestionar`, `roles.gestionar` o `accesos.revisar`).
  Una sola campaña en curso por país y alcance (índice único filtrado). La foto es fija: el acta muestra lo
  que se revisó aunque la cuenta cambie después.
- **Decidir** (`PUT .../lineas/{id}`): Mantener / Quitar / CambiarRol / Pendiente (deshacer). Quitar y
  CambiarRol exigen comentario; CambiarRol exige el rol nuevo (activo, del país, distinto del actual).
  Nada se aplica todavía. Nadie decide sobre su propia cuenta (`SeparacionDeFunciones`) y nunca puede
  quitarse a sí mismo el acceso.
- **Cerrar** (`POST .../cerrar`): UPDATE atómico "en curso y sin pendientes" (mismo patrón que cerrar un
  conteo) y, en la MISMA transacción, aplica las decisiones: desactiva cuentas / cambia roles, audita cada
  cambio sobre `Usuario` con la campaña como motivo e invalida `SesionUsuarioCache` (la baja vale ya). Si
  falla algo, o si el resultado dejara al país sin ningún administrador de usuarios activo, se revierte todo.
  Cerrada/Cancelada son inmutables.
- **Acta** (`GET .../acta`): Excel con cabecera, una fila por cuenta (decisión, quién, cuándo, aplicada) y
  bloque de firmas. Es la evidencia para TI.
- **Estado** (`GET .../estado`): última revisión CERRADA por tipo y si venció; la revisión de todas las
  cuentas también cubre a los administradores. El frontend lo muestra como aviso en Inicio.
- Permiso `accesos.revisar` (agregado AL FINAL del catálogo, Id 38), solo en Administrador. Auditoría:
  entidad `RevisionAcceso`, acciones Iniciar/Decidir/Cerrar/Cancelar.
- Migración `RevisionDeAccesos`. Probado de punta a punta contra SQL Server con
  `scratchpad/prueba_revision_accesos.py` (permisos, separación, cierre atómico, baja inmediata de sesión,
  rollback, acta, auditoría).
- PostgreSQL: el código no usa nada específico de SQL Server salvo los `HasFilter`/`HasCheckConstraint`
  con `[corchetes]` de `RevisionAccesoConfiguration`, que entran en el mismo trabajo pendiente que el
  resto de las configuraciones.

### Power BI (docs/bi, 2026-10-04)

`docs/bi/` trae las vistas de solo lectura del esquema `bi` (`01_vistas_bi.sql`, T-SQL), el tema, las medidas
DAX y una guía. No tocan ninguna tabla de la aplicación; se vuelven a crear con `CREATE OR ALTER`. Si se
agrega una vista para un módulo nuevo (por ejemplo la revisión de accesos), va ahí.

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

## Pendiente (actualizado 2026-10-06)

> Lo que falta después de la reunión corporativa y el cuestionario de TI. Fecha límite de la entrega a TI:
> **miércoles 2026-10-07**. Marcado entre corchetes quién lo desbloquea.

**Sin commit todavía** — hay trabajo local sin subir en los dos repos: `ControlesOptions` + pruebas, `docs/bi/`,
`bi.HechoExcepciones`, revisión de accesos (backend, migración `RevisionDeAccesos`, frontend `/accesos`), el
procedimiento en `docs/` y los dos `CLAUDE.md`. Se sube cuando el usuario diga "comitea y sube".

### Para la entrega a TI (miércoles)
- [ ] Actualizar el Excel del cuestionario (`generar_cuestionario.py`): filas «Gestión de accesos» y «Cuentas
      privilegiadas» con la evidencia nueva (revisión periódica + acta + procedimiento); fila de contraseñas y
      MFA como «hoy: contraseña local con bloqueo; plan: SSO Microsoft, pendiente de que TI registre la app».
- [ ] Ficha de la aplicación («declarar la aplicación»): qué es, datos que maneja, usuarios, arquitectura,
      dónde corre, quién es el dueño.
- [ ] Plan de pruebas / UAT (hay base: `informe-pruebas.md` y las 46 pruebas unitarias) y documento de gestión
      de cambios (SDLC, vulnerabilidades, retención de datos): son las filas que el desarrollo solo puede cerrar.
- [ ] Guion de la prueba en vivo (registro, entrada, salida). Con los controles activos una sola persona no
      puede hacer el circuito: hacen falta 3 usuarios (quien pide / quien aprueba / quien entrega) o relajar
      `SeparacionDeFunciones` para la demo. **Sin aclarar qué significa «Registro»** en el pedido.
- [ ] Limpiar datos de prueba antes de cualquier demo (movimientos de prueba, productos con costo 0).
- [ ] **Revisión de accesos: PLANIFICADA, apagada** (ver «Módulos planificados»). Antes de encenderla: definir QUIÉN es la
      segunda persona con `accesos.revisar` (TI o un jefe): con un solo administrador no se puede cerrar la revisión de su
      propia cuenta. El procedimiento (`docs/Procedimiento_Gestion_de_Accesos.docx`) y la respuesta al cuestionario de TI
      dependen de ella: no declararla como implementada mientras esté apagada.

### Desarrollo pendiente
- [ ] **SSO con Microsoft (Entra ID, OpenID Connect)** — trae contraseña, cambio de contraseña, MFA y bloqueo, que
      pasan a TI. Depende de que TI registre la aplicación. **No construir** cambio de contraseña propio ni
      cambio forzado en el primer ingreso (decisión del usuario 2026-10-06). Falta decidir: ¿cuenta local de
      emergencia para el administrador o solo SSO? Al implementarlo, actualizar el procedimiento.
- [ ] Parámetros de cuenta configurables (largo mínimo, bloqueo, duración de sesión) — solo mientras no haya SSO.
- [ ] Power BI: página 5 «Excepciones» en `Reporte_Inventario_Power_BI.html` (consulta del generador + plantilla),
      medidas de excepciones en `03_medidas.dax` y la guía; vista `bi` de revisiones de acceso (última revisión,
      % revisado, cuentas sin ingreso en 90 días).
- [ ] **PostgreSQL** (no hay servidor SQL Server disponible: hay que estar listos para migrar). Estimado 3–5 días.
      Falta: cambiar `UseSqlServer` por el proveedor según configuración; `sp_getapplock` de `ProductoService`
      → `pg_advisory_xact_lock`; mapear en `ManejadorGlobalDeExcepciones` los SQLSTATE (23505, 23514, 22001,
      23503, 40P01) además de los números de SQL Server; los 15 `HasFilter`/`HasCheckConstraint` con `[corchetes]`
      (incluye `RevisionAccesoConfiguration` y `AvisoDevolucionConfiguration`) necesitan una función por proveedor; regenerar las migraciones
      (las 18 actuales son de SQL Server, 3 con T-SQL a mano) + script de migración de datos; vistas `bi` en
      PostgreSQL; repetir el simulador multiusuario. **Falta dónde se alojaría y un servidor para probar.**
- [ ] **Reserva de stock**: hoy una solicitud pendiente no aparta existencia y dos personas pueden pedir lo mismo
      (opciones A reservar al aprobar —recomendada—, B, C; sin decidir).
- [ ] Tres decisiones de producto abiertas: se acepta fecha de vencimiento ya pasada, la foto del producto se
      sirve sin autenticación, y una salida con `retorna=false` ignora el destino.
- [ ] Ideas guardadas: convertir `Producto.UnidadMedida` en FK real a `Unidad` (para reportes) y modalidad
      «Solicitud FU» para consumibles (espera la aprobación del proyecto).
- [ ] Notificaciones por correo en Solicitudes (ver sección «Pendiente: notificaciones por correo»).

### Operativo
- [x] **Base de trabajo = el respaldo real (`InventoryReal`, LocalDB 2025 `(localdb)\MSSQLLocalDB`)** con la actividad de demostración
      generada el 2026-10-07 (59 solicitudes, avisos de devolución, conteos, ajustes, entradas; fechas repartidas 22/09–07/10;
      13 personas con roles reales). En ESTA PC la API de las vistas previas la usa por argumento de línea de comandos
      (`.claude/launch.json`, fuera del repo); `appsettings*.json` NO se tocó (la laptop del trabajo usa los suyos).
      Copias en `docs/demo/` (excluida de git): `InventoryReal_original.bak` (respaldo + migraciones, sin actividad nueva) y
      `InventoryReal_demo.bak`; usuarios de demostración en `docs/demo/usuarios_demo.md`. Se generó con la API
      (`scratchpad/generar_demo.py`), por eso respeta separación de funciones, auditoría y notificaciones. Las fechas se
      reescribieron por SQL; los números (SOL-/MOV-) siguen el orden de creación, no el de fecha.
- [ ] Laptop del trabajo: `git pull` en los dos repos, `dotnet ef database update` (migraciones `RevisionDeAccesos` y `AvisoDeDevolucion`;
      sin ella aparece «invalid object name») y volver a iniciar sesión (el permiso nuevo viaja en el token).
- [ ] Quitar a mano los `Co-Authored-By` de los commits viejos (reescribir el historial lo hace el usuario).
- [ ] Preguntas sin responder: el punto cortado «Ver si no se puede hacer todo en…» de la reunión, y si la API
      de desarrollo pasa a usar la base `InventoryReal` (LocalDB) como principal.

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
- ❌ No escribas `_db.Auditorias.Add(...)` a mano en un servicio nuevo — inyectá
  `IAuditoriaService` y usá `Capturar`/`RegistrarAsync` (ver "Auditoría completa").
  Y no audites `PasswordHash` ni bytes de imagen/archivo — solo campos de negocio.
