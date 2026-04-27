using System.Security.Claims;
using System.Text.Json;
using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgendaInstitucional.Api.Auditing;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? dbContext)
    {
        if (dbContext is not AppDbContext context)
        {
            return;
        }

        var entries = context.ChangeTracker
            .Entries()
            .Where(e =>
                e.Entity is not Auditorium &&
                (e.State == EntityState.Added ||
                 e.State == EntityState.Modified ||
                 e.State == EntityState.Deleted))
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User?.FindFirstValue(ClaimTypes.Email)
                   ?? httpContext?.User?.Identity?.Name
                   ?? "system";

        var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        var modulo = httpContext?.Request.Path.Value;

        foreach (var entry in entries)
        {
            var accion = entry.State switch
            {
                EntityState.Added => "INSERT",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => null
            };

            if (accion is null)
            {
                continue;
            }

            var table = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            var llave = BuildEntityKey(entry);

            var antes = entry.State == EntityState.Added
                ? null
                : JsonSerializer.Serialize(GetValues(entry, useOriginal: true), JsonOptions);

            var despues = entry.State == EntityState.Deleted
                ? null
                : JsonSerializer.Serialize(GetValues(entry, useOriginal: false), JsonOptions);

            context.Auditoria.Add(new Auditorium
            {
                FechaHora = DateTime.UtcNow,
                Usuario = user,
                Accion = accion,
                Tabla = table,
                Llave = llave,
                Antes = antes,
                Despues = despues,
                Ip = ip,
                UserAgent = userAgent,
                Modulo = modulo,
                SolicitudId = ResolveSolicitudId(entry)
            });
        }
    }

    private static string? BuildEntityKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }

        var parts = new List<string>();

        foreach (var property in key.Properties)
        {
            var value = entry.Property(property.Name).CurrentValue;
            if (value is null)
            {
                value = entry.Property(property.Name).OriginalValue;
            }

            parts.Add($"{property.Name}={value}");
        }

        return parts.Count > 0 ? string.Join(";", parts) : null;
    }

    private static Dictionary<string, object?> GetValues(EntityEntry entry, bool useOriginal)
    {
        var result = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            if (entry.State == EntityState.Modified && !property.IsModified)
            {
                continue;
            }

            var value = useOriginal ? property.OriginalValue : property.CurrentValue;
            result[property.Metadata.Name] = Normalize(value);
        }

        return result;
    }

    private static object? Normalize(object? value)
    {
        return value switch
        {
            DateTime dt => dt.ToString("O"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            TimeOnly t => t.ToString("HH:mm:ss"),
            _ => value
        };
    }

    private static int? ResolveSolicitudId(EntityEntry entry)
    {
        var solicitudIdProperty = entry.Properties
            .FirstOrDefault(p => p.Metadata.Name == "SolicitudId");

        if (solicitudIdProperty is not null)
        {
            var raw = solicitudIdProperty.CurrentValue ?? solicitudIdProperty.OriginalValue;
            if (raw is int solicitudId && solicitudId > 0)
            {
                return solicitudId;
            }
        }

        if (entry.Entity is Solicitude solicitud && solicitud.Id > 0)
        {
            return solicitud.Id;
        }

        return null;
    }
}
