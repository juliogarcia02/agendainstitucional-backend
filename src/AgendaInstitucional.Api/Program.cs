using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Api.Auditing;
using AgendaInstitucional.Api.Options;
using AgendaInstitucional.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = ResolveConnectionString(builder.Configuration, builder.Environment);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<CongresoCatalogSyncService>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        // Permitimos nombres completos (con espacios y acentos) cuando se usan como UserName.
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ áéíóúÁÉÍÓÚüÜñÑ";
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppIdentityDbContext>();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:3000", "http://localhost:3001"];

    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
builder.Services.AddHealthChecks();

builder.Services.Configure<AzureGraphOptions>(
    builder.Configuration.GetSection(AzureGraphOptions.SectionName));
builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Agenda Institucional API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT. Ejemplo: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<IdentityUser>();
app.MapControllers();
app.MapHealthChecks("/healthz");

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in new[] { "admin", "user" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }

}

app.MapPost("/auth/resolve-login", [AllowAnonymous] async (
    ResolveLoginRequest request,
    UserManager<IdentityUser> userManager) =>
{
    var email = request.Email?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.BadRequest(new { error = "El correo es obligatorio." });
    }

    var user = await userManager.FindByEmailAsync(email);
    // No exponemos si existe o no: si no existe, devolvemos el mismo correo.
    var login = user?.UserName ?? email;
    return Results.Ok(new { login });
})
.WithName("ResolveLogin")
.WithOpenApi();

app.MapGet("/me", (ClaimsPrincipal user) =>
{
    var email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name;

    return Results.Ok(new
    {
        message = "Acceso autorizado",
        user = email,
        claims = user.Claims.Select(c => new { c.Type, c.Value })
    });
})
.RequireAuthorization()
.WithName("GetCurrentUser")
.WithOpenApi();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

static string ResolveConnectionString(IConfiguration configuration, IWebHostEnvironment environment)
{
    // En Development puedes cambiar entre local/prod usando la variable DatabaseTarget.
    if (environment.IsDevelopment())
    {
        var databaseTarget = (configuration["DatabaseTarget"] ?? "Local").Trim().ToLowerInvariant();
        var connectionName = databaseTarget is "production" or "prod"
            ? "DevelopmentProductionConnection"
            : "DevelopmentLocalConnection";

        return configuration.GetConnectionString(connectionName)
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se encontro una cadena de conexion valida para Development.");
    }

    return configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No se encontro la cadena de conexion 'DefaultConnection'.");
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public sealed record ResolveLoginRequest(string Email);
