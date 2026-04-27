using AgendaInstitucional.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GacetaReporteController : ControllerBase
{
    private readonly AppDbContext _context;

    public GacetaReporteController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<GacetaReporteResponse>> Get(
        [FromQuery] int maxRowsPerTable = 2000,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminUser(User))
        {
            return Forbid();
        }

        maxRowsPerTable = Math.Clamp(maxRowsPerTable, 1, 10000);

        var tables = new List<GacetaTableResponse>(3);

        var comisionesData = await _context.catComisiones
            .AsNoTracking()
            .OrderBy(x => x.id)
            .Take(maxRowsPerTable)
            .Select(x => new { x.id, x.comision, x.estatus })
            .ToListAsync(cancellationToken);

        var comisionesRows = comisionesData
            .Select(x => new Dictionary<string, object?>
            {
                ["id"] = x.id,
                ["comision"] = x.comision,
                ["estatus"] = x.estatus
            })
            .ToList();

        tables.Add(new GacetaTableResponse
        {
            TableName = "comisiones",
            Columns =
            [
                new GacetaColumnResponse { Name = "id", DataType = "int", IsNullable = false, OrdinalPosition = 1 },
                new GacetaColumnResponse { Name = "comision", DataType = "nvarchar", IsNullable = true, OrdinalPosition = 2 },
                new GacetaColumnResponse { Name = "estatus", DataType = "bit", IsNullable = true, OrdinalPosition = 3 }
            ],
            Rows = comisionesRows,
            RowCount = comisionesRows.Count
        });

        var diputadosData = await _context.Diputados
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Take(maxRowsPerTable)
            .Select(x => new { x.Id, x.Nombre, x.Estatus, x.Actual })
            .ToListAsync(cancellationToken);

        var diputadosRows = diputadosData
            .Select(x => new Dictionary<string, object?>
            {
                ["id"] = x.Id,
                ["nombre"] = x.Nombre,
                ["estatus"] = x.Estatus,
                ["actual"] = x.Actual
            })
            .ToList();

        tables.Add(new GacetaTableResponse
        {
            TableName = "diputados",
            Columns =
            [
                new GacetaColumnResponse { Name = "id", DataType = "int", IsNullable = false, OrdinalPosition = 1 },
                new GacetaColumnResponse { Name = "nombre", DataType = "nvarchar", IsNullable = true, OrdinalPosition = 2 },
                new GacetaColumnResponse { Name = "estatus", DataType = "bit", IsNullable = false, OrdinalPosition = 3 },
                new GacetaColumnResponse { Name = "actual", DataType = "bit", IsNullable = true, OrdinalPosition = 4 }
            ],
            Rows = diputadosRows,
            RowCount = diputadosRows.Count
        });

        var relacionesData = await _context.ComisionesDiputados
            .AsNoTracking()
            .OrderBy(x => x.ComisionId)
            .ThenBy(x => x.DiputadoId)
            .Take(maxRowsPerTable)
            .Select(x => new { x.ComisionId, x.DiputadoId })
            .ToListAsync(cancellationToken);

        var relacionesRows = relacionesData
            .Select(x => new Dictionary<string, object?>
            {
                ["comision_id"] = x.ComisionId,
                ["diputado_id"] = x.DiputadoId
            })
            .ToList();

        tables.Add(new GacetaTableResponse
        {
            TableName = "comisiones_diputados",
            Columns =
            [
                new GacetaColumnResponse { Name = "comision_id", DataType = "int", IsNullable = false, OrdinalPosition = 1 },
                new GacetaColumnResponse { Name = "diputado_id", DataType = "int", IsNullable = false, OrdinalPosition = 2 }
            ],
            Rows = relacionesRows,
            RowCount = relacionesRows.Count
        });

        return Ok(new GacetaReporteResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            MaxRowsPerTable = maxRowsPerTable,
            Tables = tables
        });
    }

    private static bool IsAdminUser(ClaimsPrincipal user)
    {
        return user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            && string.Equals(c.Value, "admin", StringComparison.OrdinalIgnoreCase));
    }

}

public sealed class GacetaReporteResponse
{
    public DateTimeOffset GeneratedAt { get; set; }
    public int MaxRowsPerTable { get; set; }
    public List<GacetaTableResponse> Tables { get; set; } = [];
}

public sealed class GacetaTableResponse
{
    public string TableName { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public List<GacetaColumnResponse> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}

public sealed class GacetaColumnResponse
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int OrdinalPosition { get; set; }
}
