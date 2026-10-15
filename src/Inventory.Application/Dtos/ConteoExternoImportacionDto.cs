namespace Inventory.Application.Dtos;

// Módulo de conteo independiente — importación desde Excel (Fase 5). El archivo viene de un
// sistema EXTERNO con su propio layout de columnas, nunca el que genera este sistema para
// /conteos: por eso no hay una fila/columna fija, el usuario mapea qué columna es cada cosa
// a partir de la vista previa.
public record VistaPreviaImportacionConteoExternoDto(
    IReadOnlyList<string> Encabezados,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    int TotalFilas);

// Índices de columna 1-based, tal como el usuario los lee en la vista previa (columna 1 = A
// de Excel). ColCodigo/ColAlmacen/ColCantidad son obligatorios; el resto es opcional — si no
// se manda ColNombre/ColFamilia en un producto nuevo, queda con el código como nombre.
public record MapeoColumnasConteoExternoDto(
    int FilaInicioDatos,
    int ColCodigo,
    int? ColNombre,
    int? ColFamilia,
    int ColAlmacen,
    int? ColLote,
    int ColCantidad,
    int? ColUnidad,
    int? ColCosto,
    bool CrearProductosNuevos);

public record ImportacionConteoExternoResultadoDto(
    int ProductosCreados,
    int ExistenciasCreadas,
    int ExistenciasActualizadas,
    int FilasOmitidas,
    IReadOnlyList<string> Advertencias);
