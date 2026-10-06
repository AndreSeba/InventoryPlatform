/* ============================================================================
   Vistas para Power BI — Inventario de Material Promocional
   ----------------------------------------------------------------------------
   Solo LECTURA y en un esquema aparte (bi): no tocan ni reemplazan nada de la
   aplicación. Power BI se conecta a estas vistas, no a las tablas, así:
     - los nombres son claros ("Existencia", "EstadoStock", no códigos internos);
     - los estados salen en texto (no 1, 2, 3...);
     - nunca se expone PasswordHash, tokens ni archivos (fotos, evidencias).
   Se puede volver a ejecutar las veces que haga falta (CREATE OR ALTER).

   Cómo correrlo (con la API detenida o no, da igual):
     sqlcmd -S "(localdb)\mssqllocaldb" -E -d InventoryPlatformDb -f 65001 -i 01_vistas_bi.sql
   (cambiar -S por el servidor que corresponda; en SQL Express: -S ".\SQLEXPRESS")
   ============================================================================ */

IF SCHEMA_ID('bi') IS NULL EXEC('CREATE SCHEMA bi');
GO

/* ---- Dimensiones ----------------------------------------------------------- */

CREATE OR ALTER VIEW bi.DimPais AS
SELECT p.Id AS PaisId, p.Nombre AS Pais, p.CodigoIso AS Codigo
FROM dbo.Pais p
WHERE p.Activo = 1;
GO

CREATE OR ALTER VIEW bi.DimProducto AS
SELECT
    p.Id                           AS ProductoId,
    p.CodigoProducto               AS Codigo,
    p.Nombre                       AS Producto,
    c.CodigoCategoria              AS Categoria,
    p.UnidadMedida                 AS Unidad,
    ISNULL(p.CostoUnitario, 0)     AS CostoUnitario,
    p.StockMinimo                  AS StockMinimo,
    pa.Nombre                      AS Pais,
    p.PaisId                       AS PaisId,
    p.Activo                       AS Activo
FROM dbo.Producto p
JOIN dbo.Categoria c ON c.Id = p.CategoriaId
JOIN dbo.Pais pa     ON pa.Id = p.PaisId;
GO

CREATE OR ALTER VIEW bi.DimUbicacion AS
SELECT
    u.Id                                                     AS UbicacionId,
    u.CodigoUbicacion                                        AS Ubicacion,
    a.Nombre                                                 AS Almacen,
    CASE a.TipoAlmacen WHEN 1 THEN 'Propio' ELSE 'Externo' END AS TipoAlmacen,
    CASE u.TipoUbicacion WHEN 1 THEN 'Rack' ELSE 'Mueble' END  AS TipoUbicacion,
    pa.Nombre                                                AS Pais,
    a.PaisId                                                 AS PaisId
FROM dbo.Ubicacion u
JOIN dbo.Almacen a ON a.Id = u.AlmacenId
JOIN dbo.Pais pa   ON pa.Id = a.PaisId;
GO

/* ---- Stock ------------------------------------------------------------------ */

/* Existencia por producto Y ubicación (el stock siempre se calcula sumando movimientos). */
CREATE OR ALTER VIEW bi.StockPorUbicacion AS
SELECT
    m.ProductoId,
    m.UbicacionId,
    SUM(m.CantidadEfectiva)                                  AS Existencia,
    SUM(m.CantidadEfectiva * ISNULL(p.CostoUnitario, 0))     AS ValorStock,
    p.PaisId
FROM dbo.Movimiento m
JOIN dbo.Producto p ON p.Id = m.ProductoId
GROUP BY m.ProductoId, m.UbicacionId, p.PaisId
HAVING SUM(m.CantidadEfectiva) <> 0;
GO

/* Una fila por producto activo, con su estado de stock. */
CREATE OR ALTER VIEW bi.StockPorProducto AS
SELECT
    p.Id                                   AS ProductoId,
    p.CodigoProducto                       AS Codigo,
    p.Nombre                               AS Producto,
    c.CodigoCategoria                      AS Categoria,
    p.UnidadMedida                         AS Unidad,
    ISNULL(s.Existencia, 0)                AS Existencia,
    p.StockMinimo                          AS StockMinimo,
    ISNULL(p.CostoUnitario, 0)             AS CostoUnitario,
    ISNULL(s.Existencia, 0) * ISNULL(p.CostoUnitario, 0) AS ValorStock,
    CASE
        WHEN ISNULL(s.Existencia, 0) <= 0              THEN 'Sin stock'
        WHEN ISNULL(s.Existencia, 0) <= p.StockMinimo  THEN 'Bajo mínimo'
        ELSE 'Normal'
    END                                    AS EstadoStock,
    pa.Nombre                              AS Pais,
    p.PaisId                               AS PaisId
FROM dbo.Producto p
JOIN dbo.Categoria c ON c.Id = p.CategoriaId
JOIN dbo.Pais pa     ON pa.Id = p.PaisId
LEFT JOIN (SELECT ProductoId, SUM(CantidadEfectiva) AS Existencia
           FROM dbo.Movimiento GROUP BY ProductoId) s ON s.ProductoId = p.Id
WHERE p.Activo = 1;
GO

/* ---- Movimientos ------------------------------------------------------------ */

CREATE OR ALTER VIEW bi.HechoMovimientos AS
SELECT
    m.Id                                   AS MovimientoId,
    m.NumeroMovimiento                     AS Numero,
    m.FechaMovimiento                      AS FechaHora,
    CAST(m.FechaMovimiento AS date)        AS Fecha,
    m.ProductoId,
    m.UbicacionId,
    CASE m.TipoMovimiento
        WHEN 1 THEN 'Entrada'  WHEN 2 THEN 'Salida'
        WHEN 3 THEN 'Ajuste +' WHEN 4 THEN 'Ajuste -'
    END                                    AS Tipo,
    m.Cantidad,
    m.CantidadEfectiva,
    m.CantidadEfectiva * ISNULL(p.CostoUnitario, 0) AS ValorEfectivo,
    m.RegistradoPorNombre                  AS RegistradoPor,
    m.Motivo,
    s.NumeroSolicitud                      AS Solicitud,
    CAST(CASE WHEN m.Retorna = 1 THEN 1 ELSE 0 END AS bit) AS EsPrestamo,
    p.PaisId
FROM dbo.Movimiento m
JOIN dbo.Producto p            ON p.Id = m.ProductoId
LEFT JOIN dbo.SolicitudDetalle sd ON sd.Id = m.SolicitudDetalleId
LEFT JOIN dbo.Solicitud s         ON s.Id = sd.SolicitudId;
GO

/* Préstamos ("retorna") que todavía no volvieron por completo, con los días de mora. */
CREATE OR ALTER VIEW bi.PrestamosPendientes AS
SELECT
    m.Id                                   AS MovimientoId,
    m.NumeroMovimiento                     AS Numero,
    m.FechaMovimiento                      AS FechaSalida,
    p.CodigoProducto                       AS Codigo,
    p.Nombre                               AS Producto,
    m.Cantidad                             AS Prestado,
    ISNULL(d.Devuelto, 0)                  AS Devuelto,
    m.Cantidad - ISNULL(d.Devuelto, 0)     AS Pendiente,
    m.UbicacionExterna                     AS Destino,
    m.FechaRetornoEsperada                 AS FechaRetornoEsperada,
    CAST(CASE WHEN m.FechaRetornoEsperada < CAST(GETDATE() AS date) THEN 1 ELSE 0 END AS bit) AS EnMora,
    CASE WHEN m.FechaRetornoEsperada < CAST(GETDATE() AS date)
         THEN DATEDIFF(DAY, m.FechaRetornoEsperada, CAST(GETDATE() AS date)) ELSE 0 END      AS DiasDeMora,
    m.RegistradoPorNombre                  AS RegistradoPor,
    p.PaisId
FROM dbo.Movimiento m
JOIN dbo.Producto p ON p.Id = m.ProductoId
LEFT JOIN (SELECT MovimientoOrigenId, SUM(Cantidad) AS Devuelto
           FROM dbo.Movimiento WHERE MovimientoOrigenId IS NOT NULL
           GROUP BY MovimientoOrigenId) d ON d.MovimientoOrigenId = m.Id
WHERE m.TipoMovimiento = 2 AND m.Retorna = 1
  AND m.Cantidad > ISNULL(d.Devuelto, 0);
GO

/* ---- Solicitudes ------------------------------------------------------------ */

CREATE OR ALTER VIEW bi.HechoSolicitudes AS
SELECT
    s.Id                                   AS SolicitudId,
    s.NumeroSolicitud                      AS Numero,
    s.FechaSolicitud                       AS FechaHora,
    CAST(s.FechaSolicitud AS date)         AS Fecha,
    CASE s.Tipo WHEN 1 THEN 'Entrada' ELSE 'Salida' END AS Tipo,
    CASE s.Estado
        WHEN 1 THEN 'Borrador' WHEN 2 THEN 'Pendiente' WHEN 3 THEN 'Aprobada'
        WHEN 4 THEN 'Rechazada' WHEN 5 THEN 'Entrega parcial' WHEN 6 THEN 'Entregada'
        WHEN 7 THEN 'Cancelada'
    END                                    AS Estado,
    a.NombreArea                           AS Area,
    s.SolicitadoPorNombre                  AS SolicitadoPor,
    s.AprobadoPorNombre                    AS ResueltoPor,
    s.FechaResolucion,
    CASE WHEN s.FechaResolucion IS NULL THEN NULL
         ELSE CAST(DATEDIFF(MINUTE, s.FechaSolicitud, s.FechaResolucion) / 1440.0 AS decimal(10, 2)) END AS DiasParaResolver,
    d.Lineas,
    d.UnidadesSolicitadas,
    d.UnidadesAprobadas,
    d.UnidadesEntregadas,
    pa.Nombre                              AS Pais,
    a.PaisId                               AS PaisId
FROM dbo.Solicitud s
JOIN dbo.Area a  ON a.Id = s.AreaId
JOIN dbo.Pais pa ON pa.Id = a.PaisId
OUTER APPLY (
    SELECT COUNT(*)                          AS Lineas,
           SUM(sd.CantidadSolicitada)        AS UnidadesSolicitadas,
           SUM(ISNULL(sd.CantidadAprobada, 0)) AS UnidadesAprobadas,
           SUM(sd.CantidadEntregada)         AS UnidadesEntregadas
    FROM dbo.SolicitudDetalle sd WHERE sd.SolicitudId = s.Id
) d;
GO

/* ---- Conteo físico ---------------------------------------------------------- */

CREATE OR ALTER VIEW bi.HechoConteos AS
SELECT
    c.Id                                   AS ConteoId,
    c.Codigo,
    c.Nombre,
    CASE c.Estado WHEN 1 THEN 'En curso' WHEN 2 THEN 'Cerrado' ELSE 'Cancelado' END AS Estado,
    c.FechaCreacion,
    c.FechaCierre,
    CASE WHEN c.FechaCierre IS NULL THEN NULL
         ELSE CAST(DATEDIFF(MINUTE, c.FechaCreacion, c.FechaCierre) / 1440.0 AS decimal(10, 2)) END AS DiasHastaCierre,
    c.CreadoPorNombre                      AS CreadoPor,
    CAST(CASE WHEN c.ConteoOrigenId IS NULL THEN 0 ELSE 1 END AS bit) AS EsReconteo,
    l.Lineas,
    l.LineasContadas,
    l.LineasConDiferencia,
    ISNULL(e.Evidencias, 0)                AS Evidencias,
    c.PaisId,
    pa.Nombre                              AS Pais
FROM dbo.SesionConteo c
JOIN dbo.Pais pa ON pa.Id = c.PaisId
OUTER APPLY (
    SELECT COUNT(*) AS Lineas,
           SUM(CASE WHEN x.CantidadContada IS NOT NULL THEN 1 ELSE 0 END) AS LineasContadas,
           SUM(CASE WHEN x.CantidadContada IS NOT NULL AND x.CantidadContada <> x.ExistenciaSistema THEN 1 ELSE 0 END) AS LineasConDiferencia
    FROM dbo.SesionConteoLinea x WHERE x.SesionConteoId = c.Id
) l
OUTER APPLY (SELECT COUNT(*) AS Evidencias FROM dbo.SesionConteoEvidencia v WHERE v.SesionConteoId = c.Id) e;
GO

CREATE OR ALTER VIEW bi.HechoConteoLineas AS
SELECT
    l.Id                                   AS LineaId,
    c.Codigo                               AS Conteo,
    CASE c.Estado WHEN 1 THEN 'En curso' WHEN 2 THEN 'Cerrado' ELSE 'Cancelado' END AS EstadoConteo,
    c.FechaCreacion,
    l.ProductoId,
    l.UbicacionId,
    l.ExistenciaSistema,
    l.CantidadContada,
    l.CantidadContada - l.ExistenciaSistema AS Diferencia,
    (l.CantidadContada - l.ExistenciaSistema) * ISNULL(p.CostoUnitario, 0) AS ValorDiferencia,
    c.PaisId
FROM dbo.SesionConteoLinea l
JOIN dbo.SesionConteo c ON c.Id = l.SesionConteoId
JOIN dbo.Producto p     ON p.Id = l.ProductoId
WHERE l.CantidadContada IS NOT NULL;
GO

/* ---- Excepciones: lo que conviene revisar -----------------------------------
   Una fila por cada hecho que un auditor querría mirar. No son "errores": son señales.
   El sistema no puede impedir que alguien entregue material por fuera; esto hace que, cuando
   se intenta regularizar después, quede a la vista.
     - Ajuste negativo ........ stock que baja SIN una solicitud detrás (siempre con motivo)
     - Movimiento sin solicitud  entrada/salida que no nació de una solicitud (excluye cargas iniciales)
     - Entrega muy rápida ..... la entrega se registró a menos de 30 minutos de crearse la
                                solicitud: patrón típico de "primero se saca, después se carga"
     - Préstamo en mora ....... material prestado que no volvió en la fecha prometida
     - Diferencia de conteo ... lo contado no coincide con lo que decía el sistema
   El umbral de "rápida" (30 minutos) se cambia en la línea marcada abajo.                  */
CREATE OR ALTER VIEW bi.HechoExcepciones AS
SELECT 'Ajuste negativo' AS Tipo, 'Alta' AS Gravedad, m.FechaMovimiento AS FechaHora, CAST(m.FechaMovimiento AS date) AS Fecha,
       m.NumeroMovimiento AS Referencia, p.CodigoProducto + ' · ' + p.Nombre AS Producto, m.Cantidad AS Cantidad,
       m.RegistradoPorNombre AS Usuario, ISNULL(m.Motivo, '(sin motivo)') AS Detalle, p.PaisId
FROM dbo.Movimiento m JOIN dbo.Producto p ON p.Id = m.ProductoId
WHERE m.TipoMovimiento = 4
UNION ALL
SELECT 'Movimiento sin solicitud', 'Alta', m.FechaMovimiento, CAST(m.FechaMovimiento AS date),
       m.NumeroMovimiento, p.CodigoProducto + ' · ' + p.Nombre, m.Cantidad,
       m.RegistradoPorNombre, CASE m.TipoMovimiento WHEN 1 THEN 'Entrada' ELSE 'Salida' END + ' registrada sin solicitud', p.PaisId
FROM dbo.Movimiento m JOIN dbo.Producto p ON p.Id = m.ProductoId
WHERE m.TipoMovimiento IN (1, 2) AND m.SolicitudDetalleId IS NULL AND m.MovimientoOrigenId IS NULL
  AND m.NumeroMovimiento NOT LIKE 'CARGA-%'
UNION ALL
SELECT 'Entrega muy rápida', 'Media', m.FechaMovimiento, CAST(m.FechaMovimiento AS date),
       s.NumeroSolicitud, p.CodigoProducto + ' · ' + p.Nombre, m.Cantidad,
       m.RegistradoPorNombre,
       'Entregado ' + CAST(DATEDIFF(MINUTE, s.FechaSolicitud, m.FechaMovimiento) AS varchar(10)) + ' min después de crear la solicitud', p.PaisId
FROM dbo.Movimiento m
JOIN dbo.Producto p ON p.Id = m.ProductoId
JOIN dbo.SolicitudDetalle sd ON sd.Id = m.SolicitudDetalleId
JOIN dbo.Solicitud s ON s.Id = sd.SolicitudId
WHERE m.TipoMovimiento = 2
  AND DATEDIFF(MINUTE, s.FechaSolicitud, m.FechaMovimiento) < 30      /* <- umbral en minutos */
UNION ALL
SELECT 'Préstamo en mora', 'Media', pp.FechaSalida, CAST(pp.FechaSalida AS date),
       pp.Numero, pp.Codigo + ' · ' + pp.Producto, pp.Pendiente,
       pp.RegistradoPor, 'Debía volver el ' + CONVERT(varchar(10), pp.FechaRetornoEsperada, 23) + ' (' + CAST(pp.DiasDeMora AS varchar(10)) + ' días de mora)', pp.PaisId
FROM bi.PrestamosPendientes pp
WHERE pp.EnMora = 1
UNION ALL
SELECT 'Diferencia de conteo', 'Media', l.FechaCreacion, CAST(l.FechaCreacion AS date),
       l.Conteo, p.CodigoProducto + ' · ' + p.Nombre, ABS(l.Diferencia),
       c.CreadoPorNombre, 'Contado ' + CAST(l.CantidadContada AS varchar(12)) + ' y el sistema decía ' + CAST(l.ExistenciaSistema AS varchar(12)), l.PaisId
FROM bi.HechoConteoLineas l
JOIN dbo.Producto p ON p.Id = l.ProductoId
JOIN dbo.SesionConteo c ON c.Codigo = l.Conteo AND c.PaisId = l.PaisId
WHERE l.Diferencia <> 0 AND l.EstadoConteo = 'Cerrado';
GO

/* ---- Auditoría (resumen, sin valores anteriores/nuevos) --------------------- */

CREATE OR ALTER VIEW bi.HechoAuditoria AS
SELECT
    a.Id          AS AuditoriaId,
    a.FechaHora,
    CAST(a.FechaHora AS date) AS Fecha,
    a.Entidad,
    a.Accion,
    a.UsuarioNombre AS Usuario,
    a.PaisId
FROM dbo.Auditoria a;
GO
