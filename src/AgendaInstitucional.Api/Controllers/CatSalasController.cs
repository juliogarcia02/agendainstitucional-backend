using AgendaInstitucional.Api.Contracts.CatSalas;
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
public class CatSalasController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatSalasController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CatSalaResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? estatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.catSalas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();
            query = query.Where(x => x.sala != null && x.sala.Contains(searchValue));
        }

        if (estatus.HasValue)
        {
            query = query.Where(x => x.estatus == estatus.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var salas = await query
            .OrderBy(x => x.sala)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<CatSalaResponse>
        {
            Items = salas,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatSalaResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var sala = await _context.catSalas
            .AsNoTracking()
            .Where(x => x.id == id)
            .Select(x => ToResponse(x))
            .FirstOrDefaultAsync(cancellationToken);

        if (sala is null)
        {
            return NotFound();
        }

        return Ok(sala);
    }

    [HttpPost]
    public async Task<ActionResult<CatSalaResponse>> Create(CatSalaRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Sala?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre de la sala es obligatorio." });
        }

        var sala = new catSala
        {
            sala = normalizedName,
            estatus = request.Estatus ?? true
        };

        _context.catSalas.Add(sala);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = sala.id }, ToResponse(sala));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatSalaResponse>> Update(int id, CatSalaRequest request, CancellationToken cancellationToken)
    {
        var sala = await _context.catSalas.FirstOrDefaultAsync(x => x.id == id, cancellationToken);

        if (sala is null)
        {
            return NotFound();
        }

        var normalizedName = request.Sala?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "El nombre de la sala es obligatorio." });
        }

        sala.sala = normalizedName;
        sala.estatus = request.Estatus;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(sala));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var sala = await _context.catSalas.FirstOrDefaultAsync(x => x.id == id, cancellationToken);

        if (sala is null)
        {
            return NotFound();
        }

        _context.catSalas.Remove(sala);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "No se puede eliminar la sala porque tiene registros relacionados." });
        }
    }

    private static CatSalaResponse ToResponse(catSala sala)
    {
        return new CatSalaResponse
        {
            Id = sala.id,
            Sala = sala.sala,
            Estatus = sala.estatus
        };
    }
}
