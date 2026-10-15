using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoExternoSesionService : IConteoExternoSesionService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoExternoSesionService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<ConteoExternoSesionResumenDto>> ListarAsync(int paisId, CancellationToken ct)
    {
        var sesiones = await _db.ConteoExternoSesiones.AsNoTracking()
            .Where(s => s.PaisId == paisId)
            .OrderByDescending(s => s.Id)
            .ToListAsync(ct);

        var ids = sesiones.Select(s => s.Id).ToList();
        var porSesion = await _db.ConteoExternoLineas.AsNoTracking()
            .Where(l => ids.Contains(l.ConteoExternoSesionId))
            .Select(l => new { l.Id, l.ConteoExternoSesionId, Contada = l.Registros.Any() })
            .ToListAsync(ct);

        return sesiones.Select(s =>
        {
            var lineas = porSesion.Where(l => l.ConteoExternoSesionId == s.Id).ToList();
            return new ConteoExternoSesionResumenDto(
                s.Id, s.Nombre, s.Estado, s.PaisId, s.CreadoPorNombre, s.CreadoEn,
                s.CerradoPorNombre, s.FechaCierre, lineas.Count, lineas.Count(l => l.Contada));
        }).ToList();
    }

    public async Task<ConteoExternoSesionDetalleDto> ObtenerAsync(int id, int paisId, CancellationToken ct)
    {
        var sesion = await _db.ConteoExternoSesiones.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.PaisId == paisId, ct)
            ?? throw new ConteoExternoSesionNoEncontradaException(id);

        var lineas = await _db.ConteoExternoLineas.AsNoTracking()
            .Include(l => l.Registros)
            .Where(l => l.ConteoExternoSesionId == id)
            .OrderBy(l => l.Familia).ThenBy(l => l.ProductoNombre).ThenBy(l => l.AlmacenCodigo)
            .ToListAsync(ct);

        var nombresAlmacen = await ResolverNombresAlmacenAsync(paisId, ct);

        return new ConteoExternoSesionDetalleDto(
            sesion.Id, sesion.Nombre, sesion.Estado, sesion.CreadoPorNombre, sesion.CreadoEn,
            sesion.CerradoPorNombre, sesion.FechaCierre,
            lineas.Select(l => ArmarLineaDto(l, nombresAlmacen)).ToList());
    }

    // Solo para mostrar (Fase 6) — código → nombre de los almacenes con nombre
    // registrado en este país; un código sin nombre se muestra tal cual.
    private async Task<Dictionary<string, string>> ResolverNombresAlmacenAsync(int paisId, CancellationToken ct)
        => await _db.ConteoExternoAlmacenes.AsNoTracking()
            .Where(a => a.PaisId == paisId && a.Activo)
            .ToDictionaryAsync(a => a.Codigo, a => a.Nombre, ct);

    public async Task<ConteoExternoSesionResumenDto> CrearAsync(CrearConteoExternoSesionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var nombre = Validacion.Texto(dto.Nombre, 150, "El nombre de la sesión");
        var familia = Validacion.TextoOpcional(dto.Familia, 100, "La familia")?.ToUpperInvariant();
        var almacenCodigo = Validacion.TextoOpcional(dto.AlmacenCodigo, 30, "El almacén");

        var query = _db.ConteoExternoExistencias.Include(e => e.ConteoExternoProducto)
            .Where(e => e.PaisId == paisId && e.Activo);
        if (familia is not null) query = query.Where(e => e.ConteoExternoProducto!.Familia == familia);
        if (almacenCodigo is not null) query = query.Where(e => e.AlmacenCodigo == almacenCodigo);

        var existencias = await query.ToListAsync(ct);
        if (existencias.Count == 0)
            throw new ConteoExternoSesionSinExistenciasException();

        var sesion = new ConteoExternoSesion
        {
            Nombre = nombre,
            Estado = EstadoConteoExternoSesion.EnCurso,
            PaisId = paisId,
            CreadoPorId = usuario.Id,
            CreadoPorNombre = usuario.Nombre,
            CreadoEn = DateTime.UtcNow,
        };
        _db.ConteoExternoSesiones.Add(sesion);

        foreach (var existencia in existencias)
        {
            var producto = existencia.ConteoExternoProducto!;
            sesion.Lineas.Add(new ConteoExternoLinea
            {
                ConteoExternoExistenciaId = existencia.Id,
                ConteoExternoProductoId = producto.Id,
                ProductoCodigo = producto.Codigo,
                ProductoNombre = producto.Nombre,
                Familia = producto.Familia,
                AlmacenCodigo = existencia.AlmacenCodigo,
                Lote = existencia.Lote,
                UnidadCodigo = producto.UnidadCodigo ?? "",
                ExistenciaSistema = existencia.CantidadSistema,
                // Costo único por producto, nunca por almacén/lote.
                CostoUnitario = producto.CostoReferencia ?? 0m,
            });
        }

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoSesion), sesion.Id.ToString(), "Crear", null,
            _auditoria.Capturar(new { sesion.Nombre, TotalLineas = existencias.Count, Familia = familia, AlmacenCodigo = almacenCodigo }),
            paisId, usuario, null, ct);

        return new ConteoExternoSesionResumenDto(sesion.Id, sesion.Nombre, sesion.Estado, paisId, sesion.CreadoPorNombre, sesion.CreadoEn, null, null, existencias.Count, 0);
    }

    public async Task RegistrarConteoAsync(int lineaId, int numeroConteo, RegistrarConteoExternoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (numeroConteo is < 1 or > ConteoExternoRegistro.MaxConteos)
            throw new ValidacionException($"El número de conteo debe estar entre 1 y {ConteoExternoRegistro.MaxConteos}.");

        var sesionId = await SesionDeLineaAsync(lineaId, paisId, ct);
        await EnCursoAsync(sesionId, paisId, () => AplicarConteoAsync(lineaId, numeroConteo, dto.Cantidades, paisId, usuario, ct), ct);
    }

    // Transcripción ágil: el supervisor escribe SOLO la cantidad final de la línea. Dónde se guarda:
    //  · línea sin datos            → conteo 1, ubicación "S/U".
    //  · exactamente 1 conteo con 1 ubicación → se reemplaza ESA ubicación (conserva su número y nombre).
    //  · varias ubicaciones o varios conteos  → se rechaza: ese detalle se edita con RegistrarConteoAsync
    //    (el frontend abre el diálogo de detalle), así nunca se pisa información.
    // Cantidad null = no tocar nada (dejar vacío no borra un conteo existente).
    public async Task RegistrarCantidadFinalAsync(int lineaId, ConteoExternoCantidadFinalDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var sesionId = await SesionDeLineaAsync(lineaId, paisId, ct);
        await EnCursoAsync(sesionId, paisId, async () =>
        {
            if (dto.Cantidad is null) return true;

            var registros = await _db.ConteoExternoRegistros.AsNoTracking()
                .Where(r => r.ConteoExternoLineaId == lineaId)
                .Select(r => new { r.NumeroConteo, r.Ubicacion })
                .ToListAsync(ct);

            int numeroConteo;
            string ubicacion;
            var numeros = registros.Select(r => r.NumeroConteo).Distinct().ToList();
            if (registros.Count == 0)
            {
                numeroConteo = 1;
                ubicacion = ConteoExternoRegistro.SinUbicacion;
            }
            else if (numeros.Count == 1 && registros.Count == 1)
            {
                numeroConteo = registros[0].NumeroConteo;
                ubicacion = registros[0].Ubicacion;
            }
            else
            {
                throw new ValidacionException("Esta línea tiene varias ubicaciones o reconteos: editala desde el detalle.");
            }

            return await AplicarConteoAsync(lineaId, numeroConteo, new Dictionary<string, decimal?> { [ubicacion] = dto.Cantidad }, paisId, usuario, ct);
        }, ct);
    }

    // Núcleo de "registrar un conteo": REEMPLAZA todas las filas de ESE número de conteo de la línea.
    // Debe llamarse dentro de EnCursoAsync (ya con la sesión bloqueada y en curso).
    private async Task<bool> AplicarConteoAsync(int lineaId, int numeroConteo, IReadOnlyDictionary<string, decimal?> cantidades,
        int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var linea = await _db.ConteoExternoLineas.Include(l => l.ConteoExternoSesion).Include(l => l.Registros)
            .FirstOrDefaultAsync(l => l.Id == lineaId && l.ConteoExternoSesion!.PaisId == paisId, ct)
            ?? throw new ConteoExternoLineaNoEncontradaException(lineaId);

        var unidad = await _db.ConteoExternoUnidades.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PaisId == paisId && u.Codigo == linea.UnidadCodigo, ct)
            ?? throw new ConteoExternoUnidadNoEncontradaException(linea.UnidadCodigo);

        var validadas = new Dictionary<string, decimal>();
        foreach (var (ubicacionCruda, cantidadCruda) in cantidades)
        {
            var ubicacion = NormalizarUbicacion(ubicacionCruda);
            var cantidad = CantidadConteoExterno.ValidarOpcional(cantidadCruda, unidad.DecimalesPermitidos, "La cantidad");
            if (cantidad is null) continue; // sin contar — no se guarda fila

            if (ubicacion.Length > ConteoExternoRegistro.LargoMaxUbicacion)
                throw new ValidacionException($"La ubicación '{ubicacion[..20]}…' pasa de {ConteoExternoRegistro.LargoMaxUbicacion} caracteres.");
            if (!validadas.TryAdd(ubicacion, cantidad.Value))
                throw new ValidacionException($"Conteo {numeroConteo}: la ubicación '{ubicacion}' está repetida.");
        }

        var existentes = linea.Registros.Where(r => r.NumeroConteo == numeroConteo).ToList();
        _db.ConteoExternoRegistros.RemoveRange(existentes);
        foreach (var (ubicacion, cantidad) in validadas)
        {
            linea.Registros.Add(new ConteoExternoRegistro
            {
                ConteoExternoLineaId = linea.Id,
                NumeroConteo = numeroConteo,
                Ubicacion = ubicacion,
                Cantidad = cantidad,
                ContadoPorId = usuario.Id,
                ContadoPorNombre = usuario.Nombre,
                FechaConteo = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoLinea), linea.Id.ToString(), "Registrar conteo", null,
            _auditoria.Capturar(new { linea.ProductoCodigo, linea.AlmacenCodigo, linea.Lote, NumeroConteo = numeroConteo, Ubicaciones = validadas }),
            paisId, usuario, null, ct);
        return true;
    }

    private async Task<int> SesionDeLineaAsync(int lineaId, int paisId, CancellationToken ct)
        => await _db.ConteoExternoLineas.AsNoTracking()
               .Where(l => l.Id == lineaId && l.ConteoExternoSesion!.PaisId == paisId)
               .Select(l => (int?)l.ConteoExternoSesionId)
               .FirstOrDefaultAsync(ct)
           ?? throw new ConteoExternoLineaNoEncontradaException(lineaId);

    // Ejecuta una modificación sobre una sesión EN CURSO. Primero toma el candado de la fila con un UPDATE
    // que solo matchea si sigue en curso (mismo patrón que ConteoService.EnCursoAsync): si otro la cerró
    // antes da 0 filas y se rechaza; si el cierre llega después, espera a que esta transacción termine.
    // Sin esto se podría guardar una cantidad o un sobrante DESPUÉS del cierre.
    private async Task<T> EnCursoAsync<T>(int sesionId, int paisId, Func<Task<T>> accion, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var bloqueadas = await _db.ConteoExternoSesiones
            .Where(s => s.Id == sesionId && s.PaisId == paisId && s.Estado == EstadoConteoExternoSesion.EnCurso)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Estado, EstadoConteoExternoSesion.EnCurso), ct);
        if (bloqueadas == 0)
        {
            var existe = await _db.ConteoExternoSesiones.AsNoTracking().AnyAsync(s => s.Id == sesionId && s.PaisId == paisId, ct);
            if (!existe) throw new ConteoExternoSesionNoEncontradaException(sesionId);
            throw new ConteoExternoSesionCerradaException();
        }

        var resultado = await accion();
        await tx.CommitAsync(ct);
        return resultado;
    }

    // Producto SOBRANTE: está físicamente pero el sistema no lo tiene. Todo o nada, en una transacción:
    // (1) alta del producto en el catálogo SOLO si el código no existe, (2) lote en Existencias con existencia 0,
    // (3) línea en la sesión con existencia del sistema 0 y el costo del producto. Lo contado sale como
    // diferencia positiva. Costo ÚNICO por producto: si el producto no lo tiene, se exige y se fija acá.
    public async Task<ConteoExternoLineaDto> AgregarSobranteAsync(int sesionId, AgregarSobranteConteoExternoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = Validacion.Texto(dto.Codigo, 30, "El código del producto");
        if (codigo.Any(char.IsWhiteSpace))
            throw new ValidacionException("El código del producto no puede contener espacios.");
        var almacenCodigo = Validacion.Texto(dto.AlmacenCodigo, 30, "El almacén");
        var lote = Validacion.Texto(dto.Lote, 50, "El lote");
        var normalizado = CodigoConteoExterno.Normalizar(codigo);

        var lineaId = await EnCursoAsync(sesionId, paisId, async () =>
        {
            var producto = await _db.ConteoExternoProductos
                .FirstOrDefaultAsync(p => p.PaisId == paisId && p.CodigoNormalizado == normalizado && p.Activo, ct);
            var productoNuevo = producto is null;
            if (producto is null)
            {
                producto = new ConteoExternoProducto
                {
                    Codigo = codigo,
                    CodigoNormalizado = normalizado,
                    Nombre = Validacion.Texto(dto.Nombre, 200, "El nombre del producto"),
                    Familia = (Validacion.TextoOpcional(dto.Familia, 100, "La familia") ?? string.Empty).ToUpperInvariant(),
                    Origen = OrigenConteoExternoProducto.Manual,
                    PaisId = paisId,
                    Activo = true,
                };
                // No se agrega al contexto todavía: entra junto con su existencia (por la navegación) recién
                // cuando todas las validaciones pasaron, así un rechazo no deja nada pendiente de guardar.
            }

            // Unidad: única por producto. Se fija con el primer lote; si ya estaba fijada tiene que coincidir.
            var unidadPedida = string.IsNullOrWhiteSpace(dto.UnidadCodigo) ? null : dto.UnidadCodigo.Trim().ToUpperInvariant();
            if (producto.UnidadCodigo is not null && unidadPedida is not null && unidadPedida != producto.UnidadCodigo)
                throw new UnidadConteoExternoProductoYaFijadaException(producto.Codigo, producto.UnidadCodigo);
            var unidadCodigo = producto.UnidadCodigo ?? unidadPedida
                ?? throw new ValidacionException("Elegí la unidad del producto.");
            var unidad = await _db.ConteoExternoUnidades.AsNoTracking()
                .FirstOrDefaultAsync(u => u.PaisId == paisId && u.Codigo == unidadCodigo && u.Activo, ct)
                ?? throw new ConteoExternoUnidadNoEncontradaException(unidadCodigo);
            producto.UnidadCodigo = unidadCodigo;

            // Costo único por producto.
            if (producto.CostoReferencia is null)
            {
                if (dto.Costo is null)
                    throw new ValidacionException($"{producto.Codigo} no tiene costo: ingresá el costo unitario.");
                if (dto.Costo < 0)
                    throw new ValidacionException("El costo no puede ser negativo.");
                producto.CostoReferencia = dto.Costo;
            }

            var contada = CantidadConteoExterno.ValidarOpcional(dto.CantidadContada, unidad.DecimalesPermitidos, "La cantidad contada");

            // Si el producto ya existía: el lote no puede estar ya en el sistema ni en esta sesión (no sería un sobrante).
            if (!productoNuevo)
            {
                var yaEnSistema = await _db.ConteoExternoExistencias.AnyAsync(e =>
                    e.PaisId == paisId && e.Activo && e.ConteoExternoProductoId == producto.Id && e.AlmacenCodigo == almacenCodigo && e.Lote == lote, ct);
                var yaEnSesion = await _db.ConteoExternoLineas.AnyAsync(l =>
                    l.ConteoExternoSesionId == sesionId && l.ConteoExternoProductoId == producto.Id && l.AlmacenCodigo == almacenCodigo && l.Lote == lote, ct);
                if (yaEnSistema || yaEnSesion)
                    throw new ConteoExternoSobranteYaExisteException(producto.Codigo, almacenCodigo, lote);
            }

            var existencia = new ConteoExternoExistencia
            {
                ConteoExternoProducto = producto,
                AlmacenCodigo = almacenCodigo,
                Lote = lote,
                CantidadSistema = 0m,
                PaisId = paisId,
                Activo = true,
            };
            _db.ConteoExternoExistencias.Add(existencia);

            // La línea apunta al producto y a la existencia por Id: se guardan primero para tenerlos.
            await _db.SaveChangesAsync(ct);

            var linea = new ConteoExternoLinea
            {
                ConteoExternoSesionId = sesionId,
                ConteoExternoExistenciaId = existencia.Id,
                ConteoExternoProductoId = producto.Id,
                ProductoCodigo = producto.Codigo,
                ProductoNombre = producto.Nombre,
                Familia = producto.Familia,
                AlmacenCodigo = almacenCodigo,
                Lote = lote,
                UnidadCodigo = unidadCodigo,
                ExistenciaSistema = 0m,
                CostoUnitario = producto.CostoReferencia ?? 0m,
            };
            _db.ConteoExternoLineas.Add(linea);
            await _db.SaveChangesAsync(ct);

            if (contada is not null)
            {
                var ubicacion = NormalizarUbicacion(dto.Ubicacion);
                if (ubicacion.Length > ConteoExternoRegistro.LargoMaxUbicacion)
                    throw new ValidacionException($"La ubicación pasa de {ConteoExternoRegistro.LargoMaxUbicacion} caracteres.");
                _db.ConteoExternoRegistros.Add(new ConteoExternoRegistro
                {
                    ConteoExternoLineaId = linea.Id,
                    NumeroConteo = 1,
                    Ubicacion = ubicacion,
                    Cantidad = contada.Value,
                    ContadoPorId = usuario.Id,
                    ContadoPorNombre = usuario.Nombre,
                    FechaConteo = DateTime.UtcNow,
                });
                await _db.SaveChangesAsync(ct);
            }

            await _auditoria.RegistrarAsync(nameof(ConteoExternoSesion), sesionId.ToString(), "Agregar sobrante", null,
                _auditoria.Capturar(new { ProductoCodigo = producto.Codigo, ProductoNuevo = productoNuevo, AlmacenCodigo = almacenCodigo, Lote = lote, CantidadContada = contada }),
                paisId, usuario, null, ct);
            return linea.Id;
        }, ct);

        var creada = await _db.ConteoExternoLineas.AsNoTracking().Include(l => l.Registros)
            .FirstAsync(l => l.Id == lineaId, ct);
        return ArmarLineaDto(creada, await ResolverNombresAlmacenAsync(paisId, ct));
    }

    public async Task ActualizarObservacionAsync(int lineaId, ActualizarObservacionConteoExternoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var sesionId = await SesionDeLineaAsync(lineaId, paisId, ct);
        await EnCursoAsync(sesionId, paisId, async () =>
        {
            var linea = await _db.ConteoExternoLineas
                .FirstOrDefaultAsync(l => l.Id == lineaId, ct)
                ?? throw new ConteoExternoLineaNoEncontradaException(lineaId);

            var anterior = linea.Observacion;
            linea.Observacion = Validacion.TextoOpcional(dto.Observacion, 200, "La observación");
            await _db.SaveChangesAsync(ct);

            await _auditoria.RegistrarAsync(nameof(ConteoExternoLinea), linea.Id.ToString(), "Actualizar observación",
                _auditoria.Capturar(new { Observacion = anterior }), _auditoria.Capturar(new { linea.Observacion }), paisId, usuario, null, ct);
            return true;
        }, ct);
    }

    public async Task<ConteoExternoSesionResumenDto> CerrarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var sesion = await _db.ConteoExternoSesiones.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.PaisId == paisId, ct)
            ?? throw new ConteoExternoSesionNoEncontradaException(id);

        var lineas = await _db.ConteoExternoLineas.AsNoTracking()
            .Select(l => new { l.Id, l.ConteoExternoSesionId, Contada = l.Registros.Any() })
            .Where(l => l.ConteoExternoSesionId == id)
            .ToListAsync(ct);

        if (sesion.Estado == EstadoConteoExternoSesion.EnCurso)
        {
            var sinContar = lineas.Count(l => !l.Contada);
            if (sinContar > 0)
                throw new ValidacionException($"No se puede cerrar: faltan {sinContar} línea(s) por contar.");

            var ahora = DateTime.UtcNow;
            // Cerrar y quitar de Existencias los lotes contados van juntos: la sesión conserva su propia
            // foto (historial y KPIs no cambian) y el próximo conteo parte de una importación nueva.
            // "Quitar" = Activo=false (nunca se borra la fila, como el resto del sistema).
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var afectadas = await _db.ConteoExternoSesiones
                .Where(s => s.Id == id && s.Estado == EstadoConteoExternoSesion.EnCurso)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Estado, EstadoConteoExternoSesion.Cerrada)
                    .SetProperty(s => s.FechaCierre, ahora)
                    .SetProperty(s => s.CerradoPorId, usuario.Id)
                    .SetProperty(s => s.CerradoPorNombre, usuario.Nombre), ct);

            var lotesQuitados = 0;
            if (afectadas > 0)
                lotesQuitados = await _db.ConteoExternoExistencias
                    .Where(e => e.PaisId == paisId && e.Activo &&
                                _db.ConteoExternoLineas.Any(l => l.ConteoExternoSesionId == id && l.ConteoExternoExistenciaId == e.Id))
                    .ExecuteUpdateAsync(u => u.SetProperty(e => e.Activo, false), ct);
            await tx.CommitAsync(ct);

            if (afectadas > 0)
                await _auditoria.RegistrarAsync(nameof(ConteoExternoSesion), id.ToString(), "Cerrar", null,
                    _auditoria.Capturar(new { sesion.Nombre, LotesQuitadosDeExistencias = lotesQuitados }), paisId, usuario, null, ct);
        }

        var actual = await _db.ConteoExternoSesiones.AsNoTracking().FirstAsync(s => s.Id == id, ct);
        return new ConteoExternoSesionResumenDto(actual.Id, actual.Nombre, actual.Estado, actual.PaisId, actual.CreadoPorNombre, actual.CreadoEn,
            actual.CerradoPorNombre, actual.FechaCierre, lineas.Count, lineas.Count(l => l.Contada));
    }

    public async Task<ConteoExternoSesionKpiDto> ObtenerKpisAsync(int sesionId, int paisId, CancellationToken ct)
    {
        var sesion = await _db.ConteoExternoSesiones.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sesionId && s.PaisId == paisId, ct)
            ?? throw new ConteoExternoSesionNoEncontradaException(sesionId);

        var lineas = await _db.ConteoExternoLineas.AsNoTracking().Include(l => l.Registros)
            .Where(l => l.ConteoExternoSesionId == sesionId).ToListAsync(ct);

        var kpis = KpiConteoExternoCalculador.Calcular(lineas.Select(ArmarKpiLinea));
        return ArmarKpiDto(sesion, kpis);
    }

    public async Task<IReadOnlyList<ConteoExternoSesionKpiDto>> ObtenerHistorialKpisAsync(int paisId, CancellationToken ct)
    {
        var sesiones = await _db.ConteoExternoSesiones.AsNoTracking()
            .Where(s => s.PaisId == paisId).OrderByDescending(s => s.Id).ToListAsync(ct);

        var ids = sesiones.Select(s => s.Id).ToList();
        var lineas = await _db.ConteoExternoLineas.AsNoTracking().Include(l => l.Registros)
            .Where(l => ids.Contains(l.ConteoExternoSesionId)).ToListAsync(ct);

        return sesiones.Select(s =>
        {
            var propias = lineas.Where(l => l.ConteoExternoSesionId == s.Id).Select(ArmarKpiLinea);
            return ArmarKpiDto(s, KpiConteoExternoCalculador.Calcular(propias));
        }).ToList();
    }

    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarKpisExcelAsync(int sesionId, int paisId, CancellationToken ct)
    {
        var sesion = await _db.ConteoExternoSesiones.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sesionId && s.PaisId == paisId, ct)
            ?? throw new ConteoExternoSesionNoEncontradaException(sesionId);

        var lineas = await _db.ConteoExternoLineas.AsNoTracking().Include(l => l.Registros)
            .Where(l => l.ConteoExternoSesionId == sesionId)
            .OrderBy(l => l.Familia).ThenBy(l => l.ProductoNombre).ThenBy(l => l.AlmacenCodigo)
            .ToListAsync(ct);

        var kpis = KpiConteoExternoCalculador.Calcular(lineas.Select(ArmarKpiLinea));
        var nombresAlmacen = await ResolverNombresAlmacenAsync(paisId, ct);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("KPIs");
        const int columnas = 9;

        hoja.Cell(1, 1).Value = "Conteo Externo — KPIs";
        hoja.Range(1, 1, 1, columnas).Merge();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = "Sesión:"; hoja.Cell(2, 1).Style.Font.Bold = true; hoja.Cell(2, 2).Value = sesion.Nombre;
        hoja.Cell(3, 1).Value = "Estado:"; hoja.Cell(3, 1).Style.Font.Bold = true; hoja.Cell(3, 2).Value = sesion.Estado.ToString();
        hoja.Cell(4, 1).Value = "Creada:"; hoja.Cell(4, 1).Style.Font.Bold = true; hoja.Cell(4, 2).Value = sesion.CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        var filaKpi = 6;
        void Kpi(string etiqueta, string valor)
        {
            hoja.Cell(filaKpi, 1).Value = etiqueta;
            hoja.Cell(filaKpi, 1).Style.Font.Bold = true;
            hoja.Cell(filaKpi, 2).Value = valor;
            filaKpi++;
        }
        Kpi("Lotes inventariados", kpis.LotesInventariados.ToString());
        Kpi("Lotes correctos", kpis.LotesCorrectos.ToString());
        Kpi("IRA por lote", kpis.IraLotes is null ? "—" : kpis.IraLotes.Value.ToString("P2"));
        Kpi("Códigos inventariados", kpis.CodigosInventariados.ToString());
        Kpi("Códigos correctos", kpis.CodigosCorrectos.ToString());
        Kpi("IRA por código", kpis.IraCodigos is null ? "—" : kpis.IraCodigos.Value.ToString("P2"));
        Kpi("Valor del inventario (Bs)", kpis.ValorInventario.ToString("0.00"));
        Kpi("Diferencia neta (Bs)", kpis.DiferenciaNetaBob.ToString("0.00"));
        Kpi("Diferencia absoluta (Bs)", kpis.DiferenciaAbsolutaBob.ToString("0.00"));
        Kpi("Exactitud de valor", kpis.ExactitudValor is null ? "—" : kpis.ExactitudValor.Value.ToString("P2"));
        Kpi("Sin contar", kpis.SinContar.ToString());

        var filaEncabezado = filaKpi + 1;
        string[] encabezados = ["Código", "Producto", "Familia", "Almacén", "Lote", "Existencia", "Contado", "Diferencia", "Dif. valor (Bs)"];
        for (var col = 0; col < encabezados.Length; col++)
            hoja.Cell(filaEncabezado, col + 1).Value = encabezados[col];
        var rangoEncabezado = hoja.Range(filaEncabezado, 1, filaEncabezado, columnas);
        rangoEncabezado.Style.Font.Bold = true;
        rangoEncabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var fila = filaEncabezado + 1;
        foreach (var l in lineas)
        {
            var (_, total) = CalcularVigente(l.Registros);
            decimal? diferencia = total is null ? null : total - l.ExistenciaSistema;

            hoja.Cell(fila, 1).Value = l.ProductoCodigo;
            hoja.Cell(fila, 2).Value = l.ProductoNombre;
            hoja.Cell(fila, 3).Value = l.Familia;
            hoja.Cell(fila, 4).Value = nombresAlmacen.GetValueOrDefault(l.AlmacenCodigo, l.AlmacenCodigo);
            hoja.Cell(fila, 5).Value = l.Lote ?? "";
            hoja.Cell(fila, 6).Value = l.ExistenciaSistema;
            hoja.Cell(fila, 7).Value = total is null ? "Sin contar" : total.Value.ToString();
            hoja.Cell(fila, 8).Value = diferencia?.ToString() ?? "—";
            hoja.Cell(fila, 9).Value = (diferencia * l.CostoUnitario)?.ToString("0.00") ?? "—";
            fila++;
        }

        hoja.Columns().AdjustToContents();
        hoja.ShowGridLines = false;
        hoja.PageSetup.ShowGridlines = false;

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return (stream.ToArray(), $"ConteoExterno_KPIs_Sesion_{sesion.Id}.xlsx");
    }

    private static KpiConteoExternoLinea ArmarKpiLinea(ConteoExternoLinea linea)
    {
        var (_, total) = CalcularVigente(linea.Registros);
        decimal? diferencia = total is null ? null : total - linea.ExistenciaSistema;
        decimal? diferenciaValor = diferencia is null ? null : diferencia * linea.CostoUnitario;
        return new KpiConteoExternoLinea(linea.ProductoCodigo, diferencia, diferenciaValor, linea.ExistenciaSistema * linea.CostoUnitario);
    }

    private static ConteoExternoSesionKpiDto ArmarKpiDto(ConteoExternoSesion sesion, KpiConteoExterno k) => new(
        sesion.Id, sesion.Nombre, sesion.Estado, sesion.CreadoEn, sesion.FechaCierre,
        k.LotesInventariados, k.LotesCorrectos, k.IraLotes,
        k.CodigosInventariados, k.CodigosCorrectos, k.IraCodigos,
        k.ValorInventario, k.DiferenciaNetaBob, k.DiferenciaAbsolutaBob,
        k.ExactitudValor, k.ExactitudValorAbsoluta, k.SinContar);

    private static ConteoExternoLineaDto ArmarLineaDto(ConteoExternoLinea linea, Dictionary<string, string> nombresAlmacen)
    {
        var (vigente, total) = CalcularVigente(linea.Registros);
        decimal? diferencia = vigente is null ? null : total - linea.ExistenciaSistema;
        var registros = vigente is null
            ? []
            : linea.Registros.Where(r => r.NumeroConteo == vigente).Select(r => new ConteoExternoRegistroDto(r.NumeroConteo, r.Ubicacion, r.Cantidad)).ToList();

        return new ConteoExternoLineaDto(
            linea.Id, linea.ConteoExternoProductoId, linea.ProductoCodigo, linea.ProductoNombre, linea.Familia,
            linea.AlmacenCodigo, nombresAlmacen.GetValueOrDefault(linea.AlmacenCodigo, linea.AlmacenCodigo),
            linea.Lote, linea.UnidadCodigo, linea.ExistenciaSistema, linea.CostoUnitario,
            linea.Observacion, vigente, total, diferencia, diferencia * linea.CostoUnitario, registros);
    }

    // "Conteo vigente" = el número más alto que tenga algún registro cargado — los
    // números anteriores no se combinan con él.
    private static (int? vigente, decimal? total) CalcularVigente(IEnumerable<ConteoExternoRegistro> registros)
    {
        var porNumero = registros.GroupBy(r => r.NumeroConteo).OrderByDescending(g => g.Key);
        foreach (var grupo in porNumero)
        {
            var lista = grupo.ToList();
            if (lista.Count > 0)
                return (grupo.Key, lista.Sum(r => r.Cantidad));
        }
        return (null, null);
    }

    private static string NormalizarUbicacion(string? ubicacionCruda)
    {
        var colapsada = Regex.Replace((ubicacionCruda ?? string.Empty).Trim(), @"\s+", " ");
        return colapsada.Length == 0 ? ConteoExternoRegistro.SinUbicacion : colapsada;
    }
}
