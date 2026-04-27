using AgendaInstitucional.Api.Contracts.CatServicioResponsables;
using AgendaInstitucional.Api.Contracts.Common;
using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatServicioResponsablesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatServicioResponsablesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CatServicioResponsableResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? estatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<catServicioResponsable> query = _context.catServicioResponsables
            .AsNoTracking()
            .Include(x => x.Servicio);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();
            query = query.Where(x =>
                x.ResponsableNombre.Contains(searchValue) ||
                (x.ResponsableEmail != null && x.ResponsableEmail.Contains(searchValue)) ||
                x.Servicio.servicio != null && x.Servicio.servicio.Contains(searchValue));
        }

        if (estatus.HasValue)
        {
            query = query.Where(x => x.Estatus == estatus.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.ResponsableNombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CatServicioResponsableResponse
            {
                Id = x.Id,
                ServicioId = x.ServicioId,
                Servicio = x.Servicio.servicio,
                ResponsableNombre = x.ResponsableNombre,
                ResponsableEmail = x.ResponsableEmail,
                ResponsableTelefono = x.ResponsableTelefono,
                Observaciones = x.Observaciones,
                Estatus = x.Estatus,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<CatServicioResponsableResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatServicioResponsableResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catServicioResponsables
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CatServicioResponsableResponse
            {
                Id = x.Id,
                ServicioId = x.ServicioId,
                Servicio = x.Servicio.servicio,
                ResponsableNombre = x.ResponsableNombre,
                ResponsableEmail = x.ResponsableEmail,
                ResponsableTelefono = x.ResponsableTelefono,
                Observaciones = x.Observaciones,
                Estatus = x.Estatus,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CatServicioResponsableResponse>> Create(CatServicioResponsableRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateRequest(request, cancellationToken);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var item = new catServicioResponsable();
        MapRequest(request, item);
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = null;

        _context.catServicioResponsables.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(item).Reference(x => x.Servicio).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatServicioResponsableResponse>> Update(int id, CatServicioResponsableRequest request, CancellationToken cancellationToken)
    {
        var item = await _context.catServicioResponsables
            .Include(x => x.Servicio)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var validationResult = await ValidateRequest(request, cancellationToken);
        if (validationResult is not null)
        {
            return validationResult;
        }

        MapRequest(request, item);
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(item).Reference(x => x.Servicio).LoadAsync(cancellationToken);

        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catServicioResponsables.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        _context.catServicioResponsables.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult?> ValidateRequest(CatServicioResponsableRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.ResponsableNombre?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre del responsable es obligatorio." });
        }

        var servicioExiste = await _context.catServicios
            .AsNoTracking()
            .AnyAsync(x => x.id == request.ServicioId, cancellationToken);

        if (!servicioExiste)
        {
            return BadRequest(new { message = "El ServicioId no existe en catalogo de servicios." });
        }

        return null;
    }

    private static void MapRequest(CatServicioResponsableRequest request, catServicioResponsable item)
    {
        item.ServicioId = request.ServicioId;
        item.ResponsableNombre = request.ResponsableNombre!.Trim();
        item.ResponsableEmail = request.ResponsableEmail?.Trim();
        item.ResponsableTelefono = request.ResponsableTelefono?.Trim();
        item.Observaciones = request.Observaciones?.Trim();
        item.Estatus = request.Estatus;
    }

    private static CatServicioResponsableResponse ToResponse(catServicioResponsable item)
    {
        return new CatServicioResponsableResponse
        {
            Id = item.Id,
            ServicioId = item.ServicioId,
            Servicio = item.Servicio?.servicio,
            ResponsableNombre = item.ResponsableNombre,
            ResponsableEmail = item.ResponsableEmail,
            ResponsableTelefono = item.ResponsableTelefono,
            Observaciones = item.Observaciones,
            Estatus = item.Estatus,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
