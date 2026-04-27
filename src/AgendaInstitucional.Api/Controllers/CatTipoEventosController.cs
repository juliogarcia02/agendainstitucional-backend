using AgendaInstitucional.Api.Contracts.CatTipoEventos;
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
public class CatTipoEventosController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatTipoEventosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CatTipoEventoResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? estatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.catTipoEventos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();
            query = query.Where(x => x.evento != null && x.evento.Contains(searchValue));
        }

        if (estatus.HasValue)
        {
            query = query.Where(x => x.estatus == estatus.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.evento)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<CatTipoEventoResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatTipoEventoResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catTipoEventos
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
    public async Task<ActionResult<CatTipoEventoResponse>> Create(CatTipoEventoRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Evento?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre del tipo de evento es obligatorio." });
        }

        var item = new catTipoEvento
        {
            evento = normalizedName,
            estatus = request.Estatus ?? true
        };

        _context.catTipoEventos.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = item.id }, ToResponse(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatTipoEventoResponse>> Update(int id, CatTipoEventoRequest request, CancellationToken cancellationToken)
    {
        var item = await _context.catTipoEventos.FirstOrDefaultAsync(x => x.id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var normalizedName = request.Evento?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre del tipo de evento es obligatorio." });
        }

        item.evento = normalizedName;
        item.estatus = request.Estatus;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catTipoEventos.FirstOrDefaultAsync(x => x.id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        _context.catTipoEventos.Remove(item);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "No se puede eliminar el tipo de evento porque tiene registros relacionados." });
        }
    }

    private static CatTipoEventoResponse ToResponse(catTipoEvento item)
    {
        return new CatTipoEventoResponse
        {
            Id = item.id,
            Evento = item.evento,
            Estatus = item.estatus
        };
    }
}
