using AgendaInstitucional.Api.Contracts.CatServicios;
using AgendaInstitucional.Api.Contracts.Common;
using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class CatServiciosController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatServiciosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CatServicioResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? estatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.catServicios.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();
            query = query.Where(x => x.servicio != null && x.servicio.Contains(searchValue));
        }

        if (estatus.HasValue)
        {
            query = query.Where(x => x.estatus == estatus.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.servicio)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<CatServicioResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatServicioResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catServicios
            .AsNoTracking()
            .Where(x => x.id == id)
            .Select(x => ToResponse(x))
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CatServicioResponse>> Create(CatServicioRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Servicio?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre del servicio es obligatorio." });
        }

        var item = new catServicio
        {
            servicio = normalizedName,
            estatus = request.Estatus ?? true
        };

        _context.catServicios.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = item.id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatServicioResponse>> Update(int id, CatServicioRequest request, CancellationToken cancellationToken)
    {
        var item = await _context.catServicios.FirstOrDefaultAsync(x => x.id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var normalizedName = request.Servicio?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre del servicio es obligatorio." });
        }

        item.servicio = normalizedName;
        item.estatus = request.Estatus;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catServicios.FirstOrDefaultAsync(x => x.id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        _context.catServicios.Remove(item);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "No se puede eliminar el servicio porque tiene registros relacionados." });
        }
    }

    private static CatServicioResponse ToResponse(catServicio item)
    {
        return new CatServicioResponse
        {
            Id = item.id,
            Servicio = item.servicio,
            Estatus = item.estatus
        };
    }
}
