using System.Text;
using Inventory.Api.Middleware;
using Inventory.Api.Security;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Security;
using Inventory.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.Run();
