using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record RevisionAccesoResumenDto(
    int Id, string Codigo, AlcanceRevisionAcceso Alcance, EstadoRevisionAcceso Estado,
    DateTime FechaInicio, string IniciadaPor, DateTime? FechaCierre, string? CerradaPor,
    int TotalLineas, int Decididas, int Mantener, int Quitar, int CambiarRol
);

// Todo lo que muestra una línea sale de la FOTO tomada al abrir la campaña (no de la cuenta
// "viva"). NuncaIngreso / SinActividadReciente son las alertas para el revisor.
public record RevisionAccesoLineaDto(
    int Id, int UsuarioId, string UsuarioNombre, string UsuarioEmail, string Rol, bool EsPrivilegiado,
    DateTime CuentaCreadaEn, DateTime? UltimoLoginEn, bool NuncaIngreso, bool SinActividadReciente,
    DecisionAcceso Decision, int? RolNuevoId, string? RolNuevoNombre, string? Comentario,
    string? RevisadoPor, DateTime? FechaRevision, bool Aplicada, bool EsCuentaPropia
);

public record RolOpcionDto(int Id, string Nombre);

// Roles: los roles activos del país, para elegir el rol nuevo sin exigir permiso de gestionar roles.
public record RevisionAccesoDetalleDto(
    RevisionAccesoResumenDto Resumen, string? Notas, string? MotivoCancelacion, int DiasSinActividad,
    IReadOnlyList<RevisionAccesoLineaDto> Lineas, IReadOnlyList<RolOpcionDto> Roles
);

public record CrearRevisionAccesoDto(AlcanceRevisionAcceso Alcance, string? Notas);

// RolNuevoId solo con CambiarRol. Quitar y CambiarRol exigen comentario.
public record DecidirAccesoDto(DecisionAcceso Decision, int? RolNuevoId, string? Comentario);

public record CancelarRevisionAccesoDto(string Motivo);

// Para el aviso de la pantalla de Inicio: cuánto hace de la última revisión CERRADA de cada
// tipo (la revisión de todos también cubre a los administradores) y si ya venció.
public record EstadoRevisionesAccesoDto(
    DateTime? UltimaTodos, int? DiasDesdeTodos, int FrecuenciaTodosDias, bool TodosVencida, int? CampanaTodosEnCursoId,
    DateTime? UltimaAdministradores, int? DiasDesdeAdministradores, int FrecuenciaAdministradoresDias, bool AdministradoresVencida, int? CampanaAdministradoresEnCursoId
);
