using AgendaInstitucional.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("internal/agenda")]
public class AgendaInternaController : ControllerBase
{
    private readonly AppDbContext _context;

    public AgendaInternaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgendaInternaItemResponse>>> Get(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] bool soloActivos = true,
        [FromQuery] bool soloAutorizados = true,
        [FromQuery] int maxItems = 200,
        CancellationToken cancellationToken = default)
    {
        maxItems = Math.Clamp(maxItems, 1, 1000);

        var query = _context.Solicitudes
            .AsNoTracking()
            .Where(x => x.FechaEvento.HasValue);

        if (soloActivos)
        {
            query = query.Where(x => x.Estatus);
        }

        if (soloAutorizados)
        {
            query = query.Where(x => x.Autorizado);
        }

        query = query.Where(x => x.EventoInterno);

        if (fechaDesde.HasValue)
        {
            query = query.Where(x => x.FechaEvento!.Value >= fechaDesde.Value);
        }

        if (fechaHasta.HasValue)
        {
            query = query.Where(x => x.FechaEvento!.Value <= fechaHasta.Value);
        }

        var data = await query
            .OrderBy(x => x.FechaEvento)
            .ThenBy(x => x.HoraInicio)
            .ThenBy(x => x.HoraFin)
            .ThenBy(x => x.Evento)
            .Take(maxItems)
            .Select(x => new
            {
                x.Evento,
                x.FechaEvento,
                x.HoraInicio,
                x.HoraFin,
                x.SinHoraExactaInicio,
                x.DependeParaIniciar,
                x.Estatus,
                x.Autorizado,
                x.Asunto,
                Sala = x.Sala != null ? x.Sala.sala : null
            })
            .ToListAsync(cancellationToken);

        var response = data
            .Select(item => new AgendaInternaItemResponse
            {
                Title = item.Evento,
                InicioEvento = ToIsoUtcString(item.FechaEvento, item.HoraInicio),
                FinEvento = ToIsoUtcString(item.FechaEvento, item.HoraFin ?? item.HoraInicio),
                EventoSinHora = item.SinHoraExactaInicio,
                DependeDe = item.DependeParaIniciar,
                Estatus = ResolveInternalStatus(item.Estatus, item.Autorizado),
                Asunto = item.Asunto,
                Sala = string.IsNullOrWhiteSpace(item.Sala)
                    ? null
                    : new AgendaInternaSalaResponse { Title = item.Sala },
                Lugar = null,
                Direccion = null,
                Municipio = null
            })
            .ToList();

        return Ok(response);
    }

    private static string ResolveInternalStatus(bool estatus, bool autorizado)
    {
        if (!estatus)
        {
            return "Cancelado";
        }

        if (!autorizado)
        {
            return "Pendiente";
        }

        return "Asignado";
    }

    private static string? ToIsoUtcString(DateOnly? fecha, TimeOnly? hora)
    {
        if (!fecha.HasValue)
        {
            return null;
        }

        var localDateTime = fecha.Value.ToDateTime(hora ?? TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset = AgendaTimeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset)
            .ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    private static readonly TimeZoneInfo AgendaTimeZone = ResolveAgendaTimeZone();

    private static TimeZoneInfo ResolveAgendaTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}

public record AgendaInternaItemResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("inicio_evento")]
    public string? InicioEvento { get; init; }

    [JsonPropertyName("fin_evento")]
    public string? FinEvento { get; init; }

    [JsonPropertyName("evento_sin_hora")]
    public bool EventoSinHora { get; init; }

    [JsonPropertyName("depende_de")]
    public string? DependeDe { get; init; }

    [JsonPropertyName("estatus")]
    public string? Estatus { get; init; }

    [JsonPropertyName("asunto")]
    public string? Asunto { get; init; }

    [JsonPropertyName("sala")]
    public AgendaInternaSalaResponse? Sala { get; init; }

    [JsonPropertyName("lugar")]
    public string? Lugar { get; init; }

    [JsonPropertyName("direccion")]
    public string? Direccion { get; init; }

    [JsonPropertyName("municipio")]
    public string? Municipio { get; init; }
}

public record AgendaInternaSalaResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}
