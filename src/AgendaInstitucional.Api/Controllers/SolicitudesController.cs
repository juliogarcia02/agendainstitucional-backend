using AgendaInstitucional.Api.Contracts.Solicitudes;
using AgendaInstitucional.Api.Contracts.Common;
using AgendaInstitucional.Api.Services;
using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Linq.Expressions;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class SolicitudesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public SolicitudesController(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<SolicitudResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? salaId = null,
        [FromQuery] int? tipoEventoId = null,
        [FromQuery] bool? estatus = null,
        [FromQuery] bool? autorizado = null,
        [FromQuery] bool misSolicitudes = false,
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Solicitudes.AsNoTracking();

        if (misSolicitudes)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.Email)
                              ?? User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(currentUser))
            {
                query = query.Where(_ => false);
            }
            else
            {
                // El creador se infiere como el primer usuario auditado para esa solicitud.
                query = query.Where(s =>
                    _context.Auditoria
                        .AsNoTracking()
                        .Where(a => a.SolicitudId == s.Id)
                        .OrderBy(a => a.FechaHora)
                        .Select(a => a.Usuario)
                        .FirstOrDefault() == currentUser);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();
            var numericSearch = searchValue.StartsWith("#") ? searchValue[1..] : searchValue;
            var hasSolicitudId = int.TryParse(numericSearch, out var solicitudId);

            query = query.Where(x =>
                x.Evento.Contains(searchValue) ||
                (x.Asunto != null && x.Asunto.Contains(searchValue)) ||
                (x.ResponsableEvento != null && x.ResponsableEvento.Contains(searchValue)) ||
                (hasSolicitudId && x.Id == solicitudId));
        }

        if (salaId.HasValue)
        {
            query = query.Where(x => x.SalaId == salaId.Value);
        }

        if (tipoEventoId.HasValue)
        {
            query = query.Where(x => x.TipoEventoId == tipoEventoId.Value);
        }

        if (estatus.HasValue)
        {
            query = query.Where(x => x.Estatus == estatus.Value);
        }

        if (autorizado.HasValue)
        {
            query = query.Where(x => x.Autorizado == autorizado.Value);
        }

        if (fechaDesde.HasValue)
        {
            query = query.Where(x => x.FechaEvento.HasValue && x.FechaEvento.Value >= fechaDesde.Value);
        }

        if (fechaHasta.HasValue)
        {
            query = query.Where(x => x.FechaEvento.HasValue && x.FechaEvento.Value <= fechaHasta.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var solicitudes = await query
            .OrderByDescending(x => x.FechaEvento)
            .ThenByDescending(x => x.HoraInicio)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);

        foreach (var item in solicitudes)
        {
            var coords = await GetUbicacionCoordinatesBySolicitudIdAsync(item.Id, cancellationToken);
            item.Latitud = coords.Latitud;
            item.Longitud = coords.Longitud;
        }

        return Ok(new PagedResponse<SolicitudResponse>
        {
            Items = solicitudes,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SolicitudResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var solicitud = await _context.Solicitudes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ResponseProjection)
            .FirstOrDefaultAsync(cancellationToken);

        if (solicitud is null)
        {
            return NotFound();
        }

        var coords = await GetUbicacionCoordinatesBySolicitudIdAsync(id, cancellationToken);
        solicitud.Latitud = coords.Latitud;
        solicitud.Longitud = coords.Longitud;

        return Ok(solicitud);
    }

    [HttpGet("{id:int}/notificacion-autorizacion-preview")]
    public async Task<ActionResult<NotificacionAutorizacionPreviewResponse>> GetNotificacionAutorizacionPreview(
        int id,
        CancellationToken cancellationToken)
    {
        var exists = await _context.Solicitudes
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!exists)
        {
            return NotFound();
        }

        var destinatarios = await ObtenerDestinatariosNotificacionAutorizacionAsync(id, cancellationToken);

        return Ok(new NotificacionAutorizacionPreviewResponse
        {
            SolicitudId = id,
            Destinatarios = destinatarios
        });
    }

    [HttpPost("validar-conflicto-diputados")]
    public async Task<ActionResult<ValidarConflictoDiputadosResponse>> ValidarConflictoDiputados(
        ValidarConflictoDiputadosRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await FindConflictoDiputadosAsync(request, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<SolicitudResponse>> Create(SolicitudRequest request, CancellationToken cancellationToken)
    {
        var servicioIds = request.ServicioIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var missingServicioIds = await GetMissingServicioIds(servicioIds, cancellationToken);
        if (missingServicioIds.Count > 0)
        {
            return BadRequest(new
            {
                message = "Uno o mas servicios no existen.",
                servicioIdsInvalidos = missingServicioIds
            });
        }

        if (request.ComisionId.HasValue && !request.SinHoraExactaInicio && !request.AceptaConflictoDiputados
            && request.FechaEvento.HasValue && request.HoraInicio.HasValue && request.HoraFin.HasValue)
        {
            var conflictoReq = new ValidarConflictoDiputadosRequest
            {
                ComisionId = request.ComisionId.Value,
                FechaEvento = request.FechaEvento.Value,
                HoraInicio = request.HoraInicio.Value,
                HoraFin = request.HoraFin.Value
            };
            var conflictoRes = await FindConflictoDiputadosAsync(conflictoReq, cancellationToken);
            if (conflictoRes.HasConflict)
            {
                return Conflict(new
                {
                    message = "Uno o más diputados de la comisión tienen conflicto de horario.",
                    conflictos = conflictoRes.Conflictos
                });
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var solicitud = new Solicitude();
        MapRequest(request, solicitud);
        solicitud.CreatedAt = DateTime.UtcNow;
        solicitud.UpdatedAt = null;

        _context.Solicitudes.Add(solicitud);
        await _context.SaveChangesAsync(cancellationToken);

        await UpdateUbicacionGeograficaAsync(
            solicitud.Id,
            request.Latitud,
            request.Longitud,
            cancellationToken);

        await ReplaceSolicitudServicios(solicitud.Id, servicioIds, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await GetSolicitudResponseById(solicitud.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = solicitud.Id }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SolicitudResponse>> Update(int id, SolicitudRequest request, CancellationToken cancellationToken)
    {
        var solicitud = await _context.Solicitudes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (solicitud is null)
        {
            return NotFound();
        }

        // Guardar el estado anterior de Autorizado
        var eraAutorizado = solicitud.Autorizado;

        var servicioIds = request.ServicioIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var missingServicioIds = await GetMissingServicioIds(servicioIds, cancellationToken);
        if (missingServicioIds.Count > 0)
        {
            return BadRequest(new
            {
                message = "Uno o mas servicios no existen.",
                servicioIdsInvalidos = missingServicioIds
            });
        }

        if (request.ComisionId.HasValue && !request.SinHoraExactaInicio && !request.AceptaConflictoDiputados
            && request.FechaEvento.HasValue && request.HoraInicio.HasValue && request.HoraFin.HasValue)
        {
            var conflictoReq = new ValidarConflictoDiputadosRequest
            {
                ComisionId = request.ComisionId.Value,
                FechaEvento = request.FechaEvento.Value,
                HoraInicio = request.HoraInicio.Value,
                HoraFin = request.HoraFin.Value,
                SolicitudId = id
            };
            var conflictoRes = await FindConflictoDiputadosAsync(conflictoReq, cancellationToken);
            if (conflictoRes.HasConflict)
            {
                return Conflict(new
                {
                    message = "Uno o más diputados de la comisión tienen conflicto de horario.",
                    conflictos = conflictoRes.Conflictos
                });
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        MapRequest(request, solicitud);

        await UpdateUbicacionGeograficaAsync(
            solicitud.Id,
            request.Latitud,
            request.Longitud,
            cancellationToken);

        solicitud.UpdatedAt = DateTime.UtcNow;

        await ReplaceSolicitudServicios(id, servicioIds, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Enviar correo si se acaba de autorizar
        if (!eraAutorizado && request.Autorizado)
        {
            await EnviarNotificacionAutorizacion(solicitud, cancellationToken);
        }

        var response = await GetSolicitudResponseById(solicitud.Id, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var solicitud = await _context.Solicitudes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (solicitud is null)
        {
            return NotFound();
        }

        _context.Solicitudes.Remove(solicitud);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void MapRequest(SolicitudRequest request, Solicitude solicitud)
    {
        solicitud.Evento = request.Evento;
        solicitud.Asunto = request.Asunto;
        solicitud.ResponsableEvento = request.ResponsableEvento;
        solicitud.NumeroPersonas = request.NumeroPersonas;
        solicitud.SalaId = request.SalaId;
        solicitud.TipoEventoId = request.TipoEventoId;
        solicitud.ComisionId = request.ComisionId;
        solicitud.OtroServicioExtra = request.OtroServicioExtra;
        solicitud.UsuariosNotificarServicio = request.UsuariosNotificarServicio;
        solicitud.Lugar = request.Lugar;
        solicitud.Direccion = request.Direccion;
        solicitud.Municipio = request.Municipio;
        solicitud.SinHoraExactaInicio = request.SinHoraExactaInicio;
        solicitud.DependeParaIniciar = request.DependeParaIniciar;
        solicitud.FechaEvento = request.FechaEvento;
        solicitud.HoraInicio = request.HoraInicio;
        solicitud.HoraFin = request.HoraFin;
        solicitud.Autorizado = request.Autorizado;
        solicitud.EventoInterno = request.EventoInterno;
        solicitud.Estatus = request.Estatus;
    }

    private async Task<SolicitudResponse> GetSolicitudResponseById(int id, CancellationToken cancellationToken)
    {
        var response = await _context.Solicitudes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ResponseProjection)
            .FirstAsync(cancellationToken);

        var coords = await GetUbicacionCoordinatesBySolicitudIdAsync(id, cancellationToken);
        response.Latitud = coords.Latitud;
        response.Longitud = coords.Longitud;

        return response;
    }

    private async Task UpdateUbicacionGeograficaAsync(
        int solicitudId,
        decimal? latitud,
        decimal? longitud,
        CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE dbo.Solicitudes
SET Ubicacion =
    CASE
        WHEN @latitud IS NULL OR @longitud IS NULL THEN NULL
        ELSE geography::Point(CONVERT(float, @latitud), CONVERT(float, @longitud), 4326)
    END
WHERE Id = @id;";

        var idParam = new SqlParameter("@id", SqlDbType.Int) { Value = solicitudId };
        var latParam = new SqlParameter("@latitud", SqlDbType.Decimal)
        {
            Precision = 10,
            Scale = 7,
            Value = latitud.HasValue ? latitud.Value : DBNull.Value
        };
        var longParam = new SqlParameter("@longitud", SqlDbType.Decimal)
        {
            Precision = 10,
            Scale = 7,
            Value = longitud.HasValue ? longitud.Value : DBNull.Value
        };

        await _context.Database.ExecuteSqlRawAsync(
            sql,
            [idParam, latParam, longParam],
            cancellationToken);
    }

    private async Task<(decimal? Latitud, decimal? Longitud)> GetUbicacionCoordinatesBySolicitudIdAsync(
        int solicitudId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    CAST(Ubicacion.Lat AS decimal(10,7)) AS Latitud,
    CAST(Ubicacion.Long AS decimal(10,7)) AS Longitud
FROM dbo.Solicitudes
WHERE Id = @id;";

        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = solicitudId });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (null, null);
            }

            decimal? latitud = reader.IsDBNull(0) ? null : reader.GetDecimal(0);
            decimal? longitud = reader.IsDBNull(1) ? null : reader.GetDecimal(1);

            return (latitud, longitud);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<int>> GetMissingServicioIds(List<int> servicioIds, CancellationToken cancellationToken)
    {
        if (servicioIds.Count == 0)
        {
            return [];
        }

        var existingServicioIds = await _context.catServicios
            .AsNoTracking()
            .Where(x => servicioIds.Contains(x.id))
            .Select(x => x.id)
            .ToListAsync(cancellationToken);

        return servicioIds.Except(existingServicioIds).ToList();
    }

    private async Task ReplaceSolicitudServicios(int solicitudId, List<int> servicioIds, CancellationToken cancellationToken)
    {
        var currentItems = await _context.SolicitudServicios
            .Where(x => x.SolicitudId == solicitudId)
            .ToListAsync(cancellationToken);

        var currentServicioIds = currentItems.Select(x => x.ServicioId).ToHashSet();
        var requestedServicioIds = servicioIds.ToHashSet();

        var itemsToDelete = currentItems
            .Where(x => !requestedServicioIds.Contains(x.ServicioId))
            .ToList();

        if (itemsToDelete.Count > 0)
        {
            _context.SolicitudServicios.RemoveRange(itemsToDelete);
        }

        var itemsToAdd = servicioIds
            .Where(x => !currentServicioIds.Contains(x))
            .Select(x => new SolicitudServicio
            {
                SolicitudId = solicitudId,
                ServicioId = x,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (itemsToAdd.Count > 0)
        {
            _context.SolicitudServicios.AddRange(itemsToAdd);
        }
    }

    private async Task EnviarNotificacionAutorizacion(Solicitude solicitud, CancellationToken cancellationToken)
    {
        try
        {
            var destinatariosUnicos = await ObtenerDestinatariosNotificacionAutorizacionAsync(
                solicitud.Id,
                cancellationToken);

            if (!destinatariosUnicos.Any())
            {
                return; // No hay destinatarios, salir silenciosamente
            }

            // Obtener los nombres de los servicios solicitados
            var serviciosNombres = await _context.SolicitudServicios
                .AsNoTracking()
                .Where(ss => ss.SolicitudId == solicitud.Id)
                .Select(ss => ss.Servicio.servicio)
                .ToListAsync(cancellationToken);

            var serviciosNombresValidos = serviciosNombres
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            var detalleCorreo = await _context.Solicitudes
                .AsNoTracking()
                .Where(s => s.Id == solicitud.Id)
                .Select(s => new EmailSolicitudAutorizacionData
                {
                    SolicitudId = s.Id,
                    Evento = s.Evento,
                    Asunto = s.Asunto,
                    Sala = s.Sala != null ? s.Sala.sala : null,
                    TipoEvento = s.TipoEvento != null ? s.TipoEvento.evento : null,
                    Comision = s.Comision != null ? s.Comision.comision : null,
                    FechaEvento = s.FechaEvento,
                    HoraInicio = s.HoraInicio,
                    HoraFin = s.HoraFin,
                    Autorizado = s.Autorizado,
                    Estatus = s.Estatus,
                    UsuariosNotificarServicio = s.UsuariosNotificarServicio,
                    ResponsableEvento = s.ResponsableEvento,
                    NumeroPersonas = s.NumeroPersonas,
                    OtroServicioExtra = s.OtroServicioExtra,
                    Lugar = s.Lugar,
                    Direccion = s.Direccion,
                    Municipio = s.Municipio
                })
                .FirstAsync(cancellationToken);

            var coords = await GetUbicacionCoordinatesBySolicitudIdAsync(solicitud.Id, cancellationToken);
            detalleCorreo.Latitud = coords.Latitud;
            detalleCorreo.Longitud = coords.Longitud;

            detalleCorreo.ServiciosSolicitados = serviciosNombresValidos;

            // Enviar el correo
            await _emailService.EnviarNotificacionAutorizacionAsync(
                destinatariosUnicos,
                detalleCorreo,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // No relanzamos la excepción para no fallar la operación de actualización
            System.Diagnostics.Debug.WriteLine($"Error al enviar notificación de autorización: {ex.Message}");
        }
    }

    private async Task<string?> ObtenerCorreoCreadorSolicitudAsync(int solicitudId, CancellationToken cancellationToken)
    {
        var correoCreador = await _context.Auditoria
            .AsNoTracking()
            .Where(a => a.SolicitudId == solicitudId)
            .OrderBy(a => a.FechaHora)
            .Select(a => a.Usuario)
            .FirstOrDefaultAsync(cancellationToken);

        return correoCreador;
    }

    private async Task<List<string>> ObtenerDestinatariosNotificacionAutorizacionAsync(
        int solicitudId,
        CancellationToken cancellationToken)
    {
        var correoCreador = await ObtenerCorreoCreadorSolicitudAsync(solicitudId, cancellationToken);
        var correosServicios = await ObtenerCorreosResponsablesServiciosAsync(solicitudId, cancellationToken);

        var todosDestinatarios = new List<string>();
        if (!string.IsNullOrWhiteSpace(correoCreador))
        {
            todosDestinatarios.Add(correoCreador);
        }

        todosDestinatarios.AddRange(correosServicios);

        return todosDestinatarios
            .Where(x => !string.IsNullOrWhiteSpace(x) && EsEmailValido(x.Trim()))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool EsEmailValido(string email) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private async Task<List<string>> ObtenerCorreosResponsablesServiciosAsync(int solicitudId, CancellationToken cancellationToken)
    {
        var correosResponsables = await _context.SolicitudServicios
            .AsNoTracking()
            .Where(ss => ss.SolicitudId == solicitudId)
            .SelectMany(ss => ss.Servicio.catServicioResponsables
                .Where(r => !string.IsNullOrWhiteSpace(r.ResponsableEmail) && r.Estatus)
                .Select(r => r.ResponsableEmail!))
            .ToListAsync(cancellationToken);

        return correosResponsables;
    }

    private async Task<ValidarConflictoDiputadosResponse> FindConflictoDiputadosAsync(
        ValidarConflictoDiputadosRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Diputados que pertenecen a la comisión entrante
        var diputadosNueva = await _context.ComisionesDiputados
            .AsNoTracking()
            .Where(cd => cd.ComisionId == request.ComisionId)
            .Select(cd => new { cd.DiputadoId, cd.Diputado.Nombre })
            .ToListAsync(cancellationToken);

        if (diputadosNueva.Count == 0)
            return new ValidarConflictoDiputadosResponse();

        var diputadoIdsNueva = diputadosNueva.Select(d => d.DiputadoId).ToHashSet();

        // 2. Solicitudes activas con comisión en el mismo día que se traslapan en hora
        var solicitudesConflicto = await _context.Solicitudes
            .AsNoTracking()
            .Where(s =>
                s.ComisionId != null &&
                s.FechaEvento == request.FechaEvento &&
                s.Estatus == true &&
                (request.SolicitudId == null || s.Id != request.SolicitudId) &&
                s.HoraInicio != null && s.HoraFin != null &&
                s.HoraInicio < request.HoraFin && request.HoraInicio < s.HoraFin)
            .Select(s => new
            {
                s.Id,
                s.Evento,
                s.ComisionId,
                Sala = s.Sala != null ? s.Sala.sala : null,
                Comision = s.Comision != null ? s.Comision.comision : null,
                s.HoraInicio,
                s.HoraFin
            })
            .ToListAsync(cancellationToken);

        if (solicitudesConflicto.Count == 0)
            return new ValidarConflictoDiputadosResponse();

        var comisionIdsConflicto = solicitudesConflicto
            .Select(s => s.ComisionId!.Value)
            .ToHashSet();

        // 3. Diputados de las comisiones conflictivas que coinciden con los de la nueva
        var diputadosEnConflicto = await _context.ComisionesDiputados
            .AsNoTracking()
            .Where(cd =>
                comisionIdsConflicto.Contains(cd.ComisionId) &&
                diputadoIdsNueva.Contains(cd.DiputadoId))
            .Select(cd => new { cd.ComisionId, cd.DiputadoId })
            .ToListAsync(cancellationToken);

        if (diputadosEnConflicto.Count == 0)
            return new ValidarConflictoDiputadosResponse();

        // 4. Construir la lista de conflictos
        var conflictos = new List<ConflictoDiputadoItem>();
        foreach (var sol in solicitudesConflicto)
        {
            var comunIds = diputadosEnConflicto
                .Where(dc => dc.ComisionId == sol.ComisionId.GetValueOrDefault())
                .Select(dc => dc.DiputadoId)
                .ToHashSet();

            foreach (var dip in diputadosNueva.Where(d => comunIds.Contains(d.DiputadoId)))
            {
                conflictos.Add(new ConflictoDiputadoItem
                {
                    DiputadoId = dip.DiputadoId,
                    DiputadoNombre = dip.Nombre,
                    SolicitudConflictoId = sol.Id,
                    Evento = sol.Evento,
                    Sala = sol.Sala,
                    Comision = sol.Comision,
                    HoraInicio = sol.HoraInicio,
                    HoraFin = sol.HoraFin
                });
            }
        }

        return new ValidarConflictoDiputadosResponse
        {
            HasConflict = conflictos.Count > 0,
            Conflictos = conflictos
        };
    }

    private static readonly Expression<Func<Solicitude, SolicitudResponse>> ResponseProjection = solicitud => new SolicitudResponse
    {
        Id = solicitud.Id,
        Evento = solicitud.Evento,
        Asunto = solicitud.Asunto,
        ResponsableEvento = solicitud.ResponsableEvento,
        NumeroPersonas = solicitud.NumeroPersonas,
        SalaId = solicitud.SalaId,
        Sala = solicitud.Sala != null ? solicitud.Sala.sala : null,
        TipoEventoId = solicitud.TipoEventoId,
        TipoEvento = solicitud.TipoEvento != null ? solicitud.TipoEvento.evento : null,
        ComisionId = solicitud.ComisionId,
        Comision = solicitud.Comision != null ? solicitud.Comision.comision : null,
        OtroServicioExtra = solicitud.OtroServicioExtra,
        UsuariosNotificarServicio = solicitud.UsuariosNotificarServicio,
        Lugar = solicitud.Lugar,
        Direccion = solicitud.Direccion,
        Municipio = solicitud.Municipio,
        SinHoraExactaInicio = solicitud.SinHoraExactaInicio,
        DependeParaIniciar = solicitud.DependeParaIniciar,
        FechaEvento = solicitud.FechaEvento,
        HoraInicio = solicitud.HoraInicio,
        HoraFin = solicitud.HoraFin,
        Autorizado = solicitud.Autorizado,
        EventoInterno = solicitud.EventoInterno,
        Estatus = solicitud.Estatus,
        CreatedAt = solicitud.CreatedAt,
        UpdatedAt = solicitud.UpdatedAt
    };
}
