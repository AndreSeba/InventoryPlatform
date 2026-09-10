using Inventory.Api.Middleware;
using Inventory.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<ManejadorGlobalDeExcepciones>();
builder.Services.AddProblemDetails();

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
app.UseAuthorization();
app.MapControllers();

app.Run();
