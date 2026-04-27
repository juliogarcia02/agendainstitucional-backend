using AgendaInstitucional.Api.Contracts.CatComisiones;
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
public class CatComisionesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatComisionesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CatComisionResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? estatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.catComisiones.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();
            query = query.Where(x => x.comision != null && x.comision.Contains(searchValue));
        }

        if (estatus.HasValue)
        {
            query = query.Where(x => x.estatus == estatus.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.comision)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<CatComisionResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatComisionResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _context.catComisiones
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

    [HttpGet("{id:int}/diputados")]
    public async Task<ActionResult<List<ComisionDiputadoResponse>>> GetDiputadosByComision(int id, CancellationToken cancellationToken)
    {
        var comisionExists = await _context.catComisiones
            .AsNoTracking()
            .AnyAsync(x => x.id == id, cancellationToken);

        if (!comisionExists)
        {
            return NotFound();
        }

        var diputados = await _context.ComisionesDiputados
            .AsNoTracking()
            .Where(x => x.ComisionId == id)
            .Select(x => new ComisionDiputadoResponse
            {
                Id = x.Diputado.Id,
                Nombre = x.Diputado.Nombre,
                Estatus = x.Diputado.Estatus,
                Actual = x.Diputado.Actual
            })
            .OrderBy(x => x.Nombre)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Ok(diputados);
    }

    [HttpPost]
    public async Task<ActionResult<CatComisionResponse>> Create(CatComisionRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            message = "Las comisiones se sincronizan desde congresogto. Usa la función de sincronización."
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatComisionResponse>> Update(int id, CatComisionRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            message = "Las comisiones se sincronizan desde congresogto. Usa la función de sincronización."
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new
        {
            message = "Las comisiones se sincronizan desde congresogto. Usa la función de sincronización."
        });
    }

    private static CatComisionResponse ToResponse(catComisione item)
    {
        return new CatComisionResponse
        {
            Id = item.id,
            Comision = item.comision,
            Estatus = item.estatus
        };
    }
}
