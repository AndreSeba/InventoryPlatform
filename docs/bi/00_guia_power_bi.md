# Reporte de Power BI — Inventario de Material Promocional

Guía para armar el reporte en unos 30 minutos con lo que ya está preparado en esta carpeta.

| Archivo | Para qué sirve |
|---|---|
| `01_vistas_bi.sql` | Crea las vistas de datos (esquema `bi`) en la base. Solo lectura. |
| `02_tema_nestle_inventario.json` | Tema visual (colores y fuentes) para importar en Power BI. |
| `03_medidas.dax` | Todas las medidas y la tabla de fechas, listas para copiar. |
| `Reporte_Inventario_Power_BI.html` | Reporte interactivo con el aspecto de Power BI y datos reales (copia del 02/10/2026): se abre en cualquier navegador, con filtros y 4 páginas. |

> **Qué NO está en esta carpeta:** el archivo `.pbix` terminado. Solo Power BI Desktop puede generarlo; esta guía lleva de las vistas al reporte final paso a paso.

## 1. Requisitos
- **Power BI Desktop** (gratis) en la PC donde se arme el reporte.
- Acceso a la base `InventoryPlatformDb` (en la laptop del trabajo, la de LocalDB `(localdb)\mssqllocaldb`).
- Para publicar y compartir con los directivos: licencia **Power BI Pro** (o un espacio con capacidad) y, si la base queda en una máquina local, un **gateway de datos** para la actualización automática.

## 2. Crear las vistas en la base
Desde una terminal, en la carpeta `docs/bi` (cambiá el servidor si hace falta):

```bash
sqlcmd -S "(localdb)\mssqllocaldb" -E -d InventoryPlatformDb -f 65001 -i 01_vistas_bi.sql
```

Se puede ejecutar de nuevo cuando quieras (usa `CREATE OR ALTER`). **No toca ninguna tabla ni dato de la aplicación**; solo agrega 11 vistas de lectura.

**Seguridad recomendada:** no conectar Power BI con el usuario de la aplicación. Crear un usuario de solo lectura que vea únicamente el esquema `bi`:

```sql
CREATE LOGIN bi_lector WITH PASSWORD = '<<definir-una-contraseña-segura>>';
USE InventoryPlatformDb;
CREATE USER bi_lector FOR LOGIN bi_lector;
GRANT SELECT ON SCHEMA::bi TO bi_lector;
```

(En LocalDB, que corre con tu usuario de Windows, alcanza con la conexión integrada.)

## 3. Conectar Power BI
1. **Obtener datos → SQL Server**. Servidor: `(localdb)\mssqllocaldb`. Base: `InventoryPlatformDb`. Modo: **Importar**.
2. Marcar las 11 vistas del esquema `bi`: `DimPais`, `DimProducto`, `DimUbicacion`, `StockPorProducto`, `StockPorUbicacion`, `HechoMovimientos`, `PrestamosPendientes`, `HechoSolicitudes`, `HechoConteos`, `HechoConteoLineas`, `HechoAuditoria`.
3. En Power Query, **renombrar** cada consulta quitando el prefijo `bi_` (por ejemplo `HechoMovimientos`), porque las medidas usan esos nombres.
4. **Cargar**.

## 4. Relaciones (vista Modelo)
Todas de uno a varios, con filtro en una sola dirección (de la dimensión hacia el hecho).

| Desde (1) | Hacia (varios) | Columna |
|---|---|---|
| DimPais | DimProducto, HechoSolicitudes, HechoConteos, PrestamosPendientes, HechoAuditoria | PaisId |
| DimProducto | HechoMovimientos, StockPorUbicacion, HechoConteoLineas, StockPorProducto | ProductoId |
| DimUbicacion | HechoMovimientos, StockPorUbicacion, HechoConteoLineas | UbicacionId |
| DimFecha | HechoMovimientos, HechoSolicitudes, HechoAuditoria | Date → Fecha |

No relacionar `DimPais` con `DimUbicacion` ni con las tablas de hechos que ya llegan por producto: crearía dos caminos de filtro y Power BI marcaría ambigüedad.

## 5. Tema y medidas
1. **Vista → Temas → Examinar temas** y elegir `02_tema_nestle_inventario.json`.
2. Crear la tabla de fechas y todas las medidas de `03_medidas.dax`. Las medidas se agrupan en una tabla vacía llamada `_Medidas`.

## 6. Las cuatro páginas

### Página 1 — Resumen ejecutivo
- **Tarjetas:** Valor del Stock · Productos Activos · % Productos en Riesgo · Solicitudes Pendientes · % Exactitud del Inventario · Préstamos en Mora.
- **Barras:** Valor del Stock por Categoría.
- **Anillo:** Productos por EstadoStock.
- **Columnas apiladas:** Movimientos por día, leyenda por Tipo.
- **Anillo:** Solicitudes por Estado.
- **Segmentador:** País (DimPais).

### Página 2 — Inventario
- **Tarjetas:** Existencia Total · Productos Bajo Mínimo · Productos Sin Stock.
- **Matriz:** Categoría → Producto con Existencia, StockMinimo, ValorStock y EstadoStock (formato condicional: rojo si "Sin stock", ámbar si "Bajo mínimo").
- **Árbol (treemap):** ValorStock por producto, para ver el valor inmovilizado.
- **Barras:** Existencia por Almacén (DimUbicacion).
- **Segmentadores:** País, Categoría, EstadoStock.

### Página 3 — Movimientos y préstamos
- **Tarjetas:** Movimientos · Unidades Salidas · Salidas Últimos 30 Días.
- **Líneas:** unidades por semana, leyenda por Tipo.
- **Barras:** Top 10 productos por unidades de salida.
- **Tabla:** PrestamosPendientes (Numero, Producto, Pendiente, Destino, FechaRetornoEsperada, DiasDeMora) con DiasDeMora en rojo si es mayor que 0.
- **Segmentadores:** Fecha, Tipo, Categoría.

### Página 4 — Solicitudes y conteo
- **Tarjetas:** Solicitudes · % Aprobación · Días Promedio de Resolución · % Cumplimiento de Entrega.
- **Barras:** Solicitudes por Área.
- **Tarjetas de conteo:** Conteos Cerrados · % Exactitud del Inventario · Valor Absoluto de Diferencias.
- **Tabla:** conteos con Estado, líneas, líneas con diferencia y evidencias.
- **Barras:** Acciones Auditadas por Entidad (trazabilidad).

## 7. Publicar y actualizar
1. **Archivo → Publicar → Power BI Service**.
2. En el servicio: **Configuración del conjunto de datos → Credenciales** y **Actualización programada** (por ejemplo, diaria a primera hora).
3. Si la base está en una PC local, instalar el **gateway de datos** y asociarlo al conjunto de datos.
4. Compartir el informe con los directivos o publicarlo como **aplicación**.

## 8. Seguridad por país
Si Bolivia y Perú no deben ver los datos del otro, agregar **seguridad a nivel de fila** (Modelado → Administrar roles). Un rol por país con este filtro sobre `DimPais`:

```
[Pais] = "Bolivia"
```

y asignar cada directivo a su rol en el servicio. Como todas las tablas cuelgan de `DimPais` o de `DimProducto`, el filtro se propaga.

## 9. Qué pregunta de la dirección responde cada página

| Pregunta de la dirección | Dónde se responde | Medida clave |
|---|---|---|
| ¿Cuánto material tenemos y cuánto vale? | Resumen / Inventario | Valor del Stock |
| ¿Qué se va a acabar o ya se acabó? | Inventario | % Productos en Riesgo |
| ¿Qué se está entregando y a quién? | Movimientos | Unidades Salidas |
| ¿Hay material prestado sin volver? | Movimientos | Préstamos en Mora |
| ¿Cuánto tardan en aprobarse los pedidos? | Solicitudes | Días Promedio de Resolución |
| ¿Cuánto coincide el sistema con lo físico? | Conteo | % Exactitud del Inventario |
| ¿Quién hizo qué? | Conteo / trazabilidad | Acciones Auditadas |

## 10. Límites a tener presentes
- Los números del reporte son tan buenos como los datos cargados: con poco uso, las páginas se verán casi vacías. Conviene mostrarlo con la base real.
- El stock es una foto calculada sobre los movimientos; no guarda historia de existencias por día. Si se necesita ver la evolución del stock en el tiempo, hay que reconstruirla sumando movimientos acumulados (se puede agregar una medida).
- La exactitud del inventario depende de que se hagan conteos físicos; sin conteos cerrados, esa tarjeta aparece vacía.
