using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Area",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoArea = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NombreArea = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Area", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorNuevo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoCategoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permiso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permiso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ubicacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoUbicacion = table.Column<int>(type: "int", nullable: false),
                    Nro = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Lado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nivel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodigoUbicacion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ubicacion", x => x.Id);
                    table.CheckConstraint("CK_Ubicacion_Nivel", "([TipoUbicacion] = 1 AND [Nivel] IS NOT NULL) OR ([TipoUbicacion] = 2 AND [Nivel] IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "Solicitud",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroSolicitud = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SolicitadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AprobadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitud", x => x.Id);
                    table.CheckConstraint("CK_Solicitud_MotivoRechazo", "[Estado] <> 4 OR [MotivoRechazo] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Solicitud_Area_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Area",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Producto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaveProducto = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CodigoProducto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    UnidadMedida = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StockMinimo = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImagenUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.Id);
                    table.CheckConstraint("CK_Producto_Costo", "[CostoUnitario] IS NULL OR [CostoUnitario] >= 0");
                    table.CheckConstraint("CK_Producto_StockMin", "[StockMinimo] >= 0");
                    table.CheckConstraint("CK_Producto_Unidad", "[UnidadMedida] IN ('UNI','CAJA','PQTS')");
                    table.ForeignKey(
                        name: "FK_Producto_Categoria_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolPermiso",
                columns: table => new
                {
                    RolId = table.Column<int>(type: "int", nullable: false),
                    PermisoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolPermiso", x => new { x.RolId, x.PermisoId });
                    table.ForeignKey(
                        name: "FK_RolPermiso_Permiso_PermisoId",
                        column: x => x.PermisoId,
                        principalTable: "Permiso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolPermiso_Rol_RolId",
                        column: x => x.RolId,
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoLoginEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuario_Rol_RolId",
                        column: x => x.RolId,
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conteo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SesionConteo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    UbicacionId = table.Column<int>(type: "int", nullable: false),
                    NumeroConteo = table.Column<int>(type: "int", nullable: false),
                    CantidadContada = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ContadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaConteo = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conteo", x => x.Id);
                    table.CheckConstraint("CK_Conteo_Cantidad", "[CantidadContada] >= 0");
                    table.CheckConstraint("CK_Conteo_Numero", "[NumeroConteo] >= 1");
                    table.ForeignKey(
                        name: "FK_Conteo_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conteo_Ubicacion_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "Ubicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    CantidadSolicitada = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CantidadAprobada = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CantidadEntregada = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudDetalle", x => x.Id);
                    table.CheckConstraint("CK_SolicitudDetalle_Aprobada", "[CantidadAprobada] IS NULL OR [CantidadAprobada] <= [CantidadSolicitada]");
                    table.CheckConstraint("CK_SolicitudDetalle_Entregada", "[CantidadEntregada] >= 0");
                    table.CheckConstraint("CK_SolicitudDetalle_Solicitada", "[CantidadSolicitada] > 0");
                    table.ForeignKey(
                        name: "FK_SolicitudDetalle_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudDetalle_Solicitud_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitud",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroMovimiento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    TipoMovimiento = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CantidadEfectiva = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UbicacionId = table.Column<int>(type: "int", nullable: false),
                    Retorna = table.Column<bool>(type: "bit", nullable: false),
                    UbicacionExterna = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FechaRetornoEsperada = table.Column<DateOnly>(type: "date", nullable: true),
                    MovimientoOrigenId = table.Column<int>(type: "int", nullable: true),
                    SolicitudDetalleId = table.Column<int>(type: "int", nullable: true),
                    RegistradoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaMovimiento = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimiento", x => x.Id);
                    table.CheckConstraint("CK_Movimiento_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_Movimiento_NoAutoOrigen", "[MovimientoOrigenId] IS NULL OR [MovimientoOrigenId] <> [Id]");
                    table.CheckConstraint("CK_Movimiento_OrigenSoloEntrada", "[MovimientoOrigenId] IS NULL OR [TipoMovimiento] = 1");
                    table.CheckConstraint("CK_Movimiento_RetornaSoloSalida", "[TipoMovimiento] = 2 OR ([Retorna] = 0 AND [UbicacionExterna] IS NULL AND [FechaRetornoEsperada] IS NULL)");
                    table.CheckConstraint("CK_Movimiento_Signo", "([TipoMovimiento] IN (1,3) AND [CantidadEfectiva] = [Cantidad]) OR ([TipoMovimiento] IN (2,4) AND [CantidadEfectiva] = -[Cantidad])");
                    table.CheckConstraint("CK_Movimiento_SolicitudSoloSalida", "[SolicitudDetalleId] IS NULL OR [TipoMovimiento] = 2");
                    table.ForeignKey(
                        name: "FK_Movimiento_Movimiento_MovimientoOrigenId",
                        column: x => x.MovimientoOrigenId,
                        principalTable: "Movimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_SolicitudDetalle_SolicitudDetalleId",
                        column: x => x.SolicitudDetalleId,
                        principalTable: "SolicitudDetalle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimiento_Ubicacion_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "Ubicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[,]
                {
                    { 1, "productos.ver", "Ver el catálogo de productos y su existencia", "Productos" },
                    { 2, "productos.crear", "Crear productos nuevos", "Productos" },
                    { 3, "productos.editar", "Editar productos existentes", "Productos" },
                    { 4, "productos.desactivar", "Desactivar productos", "Productos" },
                    { 5, "movimientos.ver", "Ver el historial de movimientos", "Movimientos" },
                    { 6, "movimientos.entrada", "Registrar entradas de stock", "Movimientos" },
                    { 7, "movimientos.salida", "Registrar salidas de stock", "Movimientos" },
                    { 8, "movimientos.ajuste", "Registrar ajustes positivos/negativos", "Movimientos" },
                    { 9, "movimientos.devolucion", "Registrar devoluciones de préstamo", "Movimientos" },
                    { 10, "solicitudes.ver", "Ver solicitudes de material", "Solicitudes" },
                    { 11, "solicitudes.crear", "Crear solicitudes de material", "Solicitudes" },
                    { 12, "solicitudes.aprobar", "Aprobar solicitudes pendientes", "Solicitudes" },
                    { 13, "solicitudes.rechazar", "Rechazar solicitudes pendientes", "Solicitudes" },
                    { 14, "solicitudes.entregar", "Registrar la entrega de una solicitud aprobada", "Solicitudes" },
                    { 15, "conteos.ver", "Ver conteos por sesión", "Conteo físico" },
                    { 16, "conteos.registrar", "Registrar conteos", "Conteo físico" },
                    { 17, "categorias.ver", "Ver categorías", "Categorías" },
                    { 18, "categorias.crear", "Crear categorías", "Categorías" },
                    { 19, "areas.ver", "Ver áreas", "Áreas" },
                    { 20, "areas.crear", "Crear áreas", "Áreas" },
                    { 21, "ubicaciones.ver", "Ver ubicaciones", "Ubicaciones" },
                    { 22, "ubicaciones.crear", "Crear ubicaciones", "Ubicaciones" },
                    { 23, "usuarios.gestionar", "Crear y editar usuarios", "Administración" },
                    { 24, "roles.gestionar", "Crear roles y asignarles permisos", "Administración" }
                });

            migrationBuilder.InsertData(
                table: "Rol",
                columns: new[] { "Id", "Activo", "Descripcion", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "Acceso completo, incluida la gestión de usuarios y roles.", "Administrador" },
                    { 2, true, "Opera el día a día: productos, movimientos, solicitudes y conteo. Sin gestión de catálogos ni usuarios.", "Operador" },
                    { 3, true, "Solo lectura en todos los módulos.", "Consulta" }
                });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 16, 1 },
                    { 17, 1 },
                    { 18, 1 },
                    { 19, 1 },
                    { 20, 1 },
                    { 21, 1 },
                    { 22, 1 },
                    { 23, 1 },
                    { 24, 1 },
                    { 1, 2 },
                    { 5, 2 },
                    { 6, 2 },
                    { 7, 2 },
                    { 8, 2 },
                    { 9, 2 },
                    { 10, 2 },
                    { 11, 2 },
                    { 14, 2 },
                    { 15, 2 },
                    { 16, 2 },
                    { 17, 2 },
                    { 19, 2 },
                    { 21, 2 },
                    { 1, 3 },
                    { 5, 3 },
                    { 10, 3 },
                    { 15, 3 },
                    { 17, 3 },
                    { 19, 3 },
                    { 21, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Area_CodigoArea",
                table: "Area",
                column: "CodigoArea",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Entidad_EntidadId",
                table: "Auditoria",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_CodigoCategoria",
                table: "Categoria",
                column: "CodigoCategoria",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_ProductoId",
                table: "Conteo",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_SesionConteo_ProductoId_UbicacionId_NumeroConteo",
                table: "Conteo",
                columns: new[] { "SesionConteo", "ProductoId", "UbicacionId", "NumeroConteo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_UbicacionId",
                table: "Conteo",
                column: "UbicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_MovimientoOrigenId",
                table: "Movimiento",
                column: "MovimientoOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_NumeroMovimiento",
                table: "Movimiento",
                column: "NumeroMovimiento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_ProductoId_FechaMovimiento",
                table: "Movimiento",
                columns: new[] { "ProductoId", "FechaMovimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_SolicitudDetalleId",
                table: "Movimiento",
                column: "SolicitudDetalleId");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_UbicacionId_ProductoId",
                table: "Movimiento",
                columns: new[] { "UbicacionId", "ProductoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Permiso_Codigo",
                table: "Permiso",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Producto_CategoriaId",
                table: "Producto",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_ClaveProducto",
                table: "Producto",
                column: "ClaveProducto",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Rol_Nombre",
                table: "Rol",
                column: "Nombre",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RolPermiso_PermisoId",
                table: "RolPermiso",
                column: "PermisoId");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_AreaId",
                table: "Solicitud",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_NumeroSolicitud",
                table: "Solicitud",
                column: "NumeroSolicitud",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudDetalle_ProductoId",
                table: "SolicitudDetalle",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudDetalle_SolicitudId_ProductoId",
                table: "SolicitudDetalle",
                columns: new[] { "SolicitudId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ubicacion_CodigoUbicacion",
                table: "Ubicacion",
                column: "CodigoUbicacion",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_Email",
                table: "Usuario",
                column: "Email",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_RolId",
                table: "Usuario",
                column: "RolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Conteo");

            migrationBuilder.DropTable(
                name: "Movimiento");

            migrationBuilder.DropTable(
                name: "RolPermiso");

            migrationBuilder.DropTable(
                name: "Usuario");

            migrationBuilder.DropTable(
                name: "SolicitudDetalle");

            migrationBuilder.DropTable(
                name: "Ubicacion");

            migrationBuilder.DropTable(
                name: "Permiso");

            migrationBuilder.DropTable(
                name: "Rol");

            migrationBuilder.DropTable(
                name: "Producto");

            migrationBuilder.DropTable(
                name: "Solicitud");

            migrationBuilder.DropTable(
                name: "Categoria");

            migrationBuilder.DropTable(
                name: "Area");
        }
    }
}
