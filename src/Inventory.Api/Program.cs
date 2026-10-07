using System.Text;
using Inventory.Api.Middleware;
using Inventory.Api.Security;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Security;
using Inventory.Infrastructure.Seed;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Inventory.Api.Servicios.EnvioCorreoHostedService>();
builder.Services.AddHostedService<Inventory.Api.Servicios.SincronizacionNotificacionesHostedService>();

builder.Services.AddExceptionHandler<ManejadorGlobalDeExcepciones>();
builder.Services.AddProblemDetails();

// Auth: JWT bearer + policies dinámicas por permiso (ver PermissionPolicyProvider —
// [Authorize(Policy = Permisos.ProductosCrear)] funciona sin registrar cada policy acá).
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' en la configuración.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        options.Events = new JwtBearerEvents
        {
            // Una firma válida no alcanza: el usuario puede haber sido desactivado o haber
            // cambiado de permisos después de loguearse. Se compara contra el estado actual
            // (con una caché de 30 s, ver SesionUsuarioCache) y, si no coincide, el token deja
            // de valer y hay que volver a loguearse.
            OnTokenValidated = async contexto =>
            {
                var principal = contexto.Principal;
                var texto = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (principal is null || !int.TryParse(texto, out var usuarioId))
                {
                    contexto.Fail("El token no identifica a un usuario.");
                    return;
                }

                var servicios = contexto.HttpContext.RequestServices;
                var estado = await SesionUsuarioCache.ObtenerAsync(
                    servicios.GetRequiredService<IMemoryCache>(),
                    servicios.GetRequiredService<InventoryDbContext>(),
                    usuarioId, contexto.HttpContext.RequestAborted);

                var permisosDelToken = SesionUsuarioCache.Normalizar(principal.FindAll("permiso").Select(c => c.Value));
                if (estado is not { Activo: true } || estado.Permisos != permisosDelToken)
                    contexto.Fail("La sesión ya no es válida.");
            },
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermisoAuthorizationHandler>();
builder.Services.AddAuthorization();

// CORS con lista explícita de orígenes (nunca AllowAnyOrigin + credentials) —
// mismo criterio no negociable que el proyecto controAsistencia.
var origenesPermitidos = builder.Configuration.GetSection("CorsOrigenesPermitidos").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorWeb", policy =>
    {
        policy.WithOrigins(origenesPermitidos)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler(_ => { });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Inventory Platform API";
    });
}

app.UseHttpsRedirection();

app.UseCors("BlazorWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Sin migraciones/SQL Server configurados todavía (ver CLAUDE.md) — el seed no corre
// contra una base real hasta que exista. Si la conexión falla acá, el catch deja
// arrancar la API igual (mismo criterio que el resto del proyecto: sin DB, todo cae al
// 500 genérico controlado en vez de tumbar el proceso).
try
{
    await DatabaseSeeder.SeedAdminInicialAsync(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "No se pudo sembrar el usuario admin inicial (¿falta la base de datos?).");
}

// Si el código es más nuevo que la base, las consultas fallan o (peor) van lentas porque el
// esquema viejo guarda cosas como la foto de cada producto dentro de su fila. Avisarlo al
// arrancar ahorra buscar el problema en otro lado.
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pendientes.Count > 0)
        app.Logger.LogWarning(
            "La base tiene {Cantidad} migración(es) sin aplicar: {Migraciones}. Ejecutá: dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api",
            pendientes.Count, string.Join(", ", pendientes));
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "No se pudo verificar si hay migraciones pendientes.");
}

app.Run();
