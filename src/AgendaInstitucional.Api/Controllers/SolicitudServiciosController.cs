using AgendaInstitucional.Api.Contracts.SolicitudServicios;
using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("api/solicitudes/{solicitudId:int}/servicios")]
[Authorize]
public class SolicitudServiciosController : ControllerBase
{
    private readonly AppDbContext _context;

    public SolicitudServiciosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SolicitudServicioResponse>>> GetAll(int solicitudId, CancellationToken cancellationToken)
    {
        if (!await SolicitudExists(solicitudId, cancellationToken))
        {
            return NotFound(new { message = "La solicitud no existe." });
        }

        var items = await _context.SolicitudServicios
            .AsNoTracking()
            .Where(x => x.SolicitudId == solicitudId)
            .OrderBy(x => x.Servicio.servicio)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{servicioId:int}")]
    public async Task<ActionResult<SolicitudServicioResponse>> GetByServicioId(int solicitudId, int servicioId, CancellationToken cancellationToken)
    {
        var item = await _context.SolicitudServicios
            .AsNoTracking()
            .Where(x => x.SolicitudId == solicitudId && x.ServicioId == servicioId)
            .Select(ResponseProjection)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<SolicitudServicioResponse>> Create(int solicitudId, SolicitudServicioCreateRequest request, CancellationToken cancellationToken)
    {
        if (!await SolicitudExists(solicitudId, cancellationToken))
        {
            return NotFound(new { message = "La solicitud no existe." });
        }

        if (!await ServicioExists(request.ServicioId, cancellationToken))
        {
            return BadRequest(new { message = "El servicio no existe." });
        }

        var exists = await _context.SolicitudServicios
            .AsNoTracking()
            .AnyAsync(x => x.SolicitudId == solicitudId && x.ServicioId == request.ServicioId, cancellationToken);

        if (exists)
        {
            return Conflict(new { message = "El servicio ya esta relacionado con la solicitud." });
        }

        var item = new SolicitudServicio
        {
            SolicitudId = solicitudId,
            ServicioId = request.ServicioId,
            CreatedAt = DateTime.UtcNow
        };

        _context.SolicitudServicios.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        var response = await _context.SolicitudServicios
            .AsNoTracking()
            .Where(x => x.SolicitudId == solicitudId && x.ServicioId == request.ServicioId)
            .Select(ResponseProjection)
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByServicioId), new { solicitudId, servicioId = request.ServicioId }, response);
    }

    [HttpPut]
    public async Task<ActionResult<IEnumerable<SolicitudServicioResponse>>> ReplaceAll(int solicitudId, SolicitudServiciosReplaceRequest request, CancellationToken cancellationToken)
    {
        if (!await SolicitudExists(solicitudId, cancellationToken))
        {
            return NotFound(new { message = "La solicitud no existe." });
        }

        var servicioIds = request.ServicioIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (servicioIds.Count > 0)
        {
            var existingServicioIds = await _context.catServicios
                .AsNoTracking()
                .Where(x => servicioIds.Contains(x.id))
                .Select(x => x.id)
                .ToListAsync(cancellationToken);

            var missingIds = servicioIds.Except(existingServicioIds).ToList();
            if (missingIds.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Uno o mas servicios no existen.",
                    servicioIdsInvalidos = missingIds
                });
            }
        }

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

        await _context.SaveChangesAsync(cancellationToken);

        var response = await _context.SolicitudServicios
            .AsNoTracking()
            .Where(x => x.SolicitudId == solicitudId)
            .OrderBy(x => x.Servicio.servicio)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);

        return Ok(response);
    }

    [HttpDelete("{servicioId:int}")]
    public async Task<IActionResult> Delete(int solicitudId, int servicioId, CancellationToken cancellationToken)
    {
        var item = await _context.SolicitudServicios
            .FirstOrDefaultAsync(x => x.SolicitudId == solicitudId && x.ServicioId == servicioId, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        _context.SolicitudServicios.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Task<bool> SolicitudExists(int solicitudId, CancellationToken cancellationToken)
    {
        return _context.Solicitudes.AsNoTracking().AnyAsync(x => x.Id == solicitudId, cancellationToken);
    }

    private Task<bool> ServicioExists(int servicioId, CancellationToken cancellationToken)
    {
        return _context.catServicios.AsNoTracking().AnyAsync(x => x.id == servicioId, cancellationToken);
    }

    private static readonly Expression<Func<SolicitudServicio, SolicitudServicioResponse>> ResponseProjection = item => new SolicitudServicioResponse
    {
        SolicitudId = item.SolicitudId,
        ServicioId = item.ServicioId,
        Servicio = item.Servicio.servicio,
        CreatedAt = item.CreatedAt
    };
}