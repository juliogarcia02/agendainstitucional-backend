using AgendaInstitucional.Api.Contracts.Auditoria;
using AgendaInstitucional.Api.Contracts.Common;
using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private static readonly TimeZoneInfo LeonTimeZone = ResolveLeonTimeZone();
    private readonly AppDbContext _context;

    public AuditoriaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditoriaResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? usuario = null,
        [FromQuery] string? tabla = null,
        [FromQuery] string? accion = null,
        [FromQuery] string? modulo = null,
        [FromQuery] int? solicitudId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Auditorium> query = _context.Auditoria.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x =>
                x.Usuario.Contains(value) ||
                x.Tabla.Contains(value) ||
                x.Accion.Contains(value) ||
                (x.Modulo != null && x.Modulo.Contains(value)) ||
                (x.Llave != null && x.Llave.Contains(value)) ||
                (x.Ip != null && x.Ip.Contains(value)) ||
                (x.LoginSql != null && x.LoginSql.Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            var value = usuario.Trim();
            query = query.Where(x => x.Usuario.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(tabla))
        {
            var value = tabla.Trim();
            query = query.Where(x => x.Tabla.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(accion))
        {
            var value = accion.Trim();
            query = query.Where(x => x.Accion.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(modulo))
        {
            var value = modulo.Trim();
            query = query.Where(x => x.Modulo != null && x.Modulo.Contains(value));
        }

        if (solicitudId.HasValue)
        {
            query = query.Where(x => x.SolicitudId == solicitudId.Value);
        }

        if (fechaDesde.HasValue)
        {
            var from = LeonLocalDateStartToUtc(fechaDesde.Value);
            query = query.Where(x => x.FechaHora >= from);
        }

        if (fechaHasta.HasValue)
        {
            var to = LeonLocalDateEndToUtc(fechaHasta.Value);
            query = query.Where(x => x.FechaHora <= to);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.FechaHora)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<AuditoriaResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuditoriaResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _context.Auditoria
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => ToResponse(x))
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    private static AuditoriaResponse ToResponse(Auditorium item)
    {
        return new AuditoriaResponse
        {
            Id = item.Id,
            FechaHora = EnsureUtc(item.FechaHora),
            Usuario = item.Usuario,
            LoginSql = item.LoginSql,
            Accion = item.Accion,
            Tabla = item.Tabla,
            Llave = item.Llave,
            Antes = item.Antes,
            Despues = item.Despues,
            Ip = item.Ip,
            UserAgent = item.UserAgent,
            Modulo = item.Modulo,
            SolicitudId = item.SolicitudId
        };
    }

    private static DateTime LeonLocalDateStartToUtc(DateTime value)
    {
        var localStart = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, LeonTimeZone);
    }

    private static DateTime LeonLocalDateEndToUtc(DateTime value)
    {
        var localEnd = DateTime.SpecifyKind(value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localEnd, LeonTimeZone);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveLeonTimeZone()
    {
        var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Central Standard Time (Mexico)"
            : "America/Mexico_City";

        return TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }
}
