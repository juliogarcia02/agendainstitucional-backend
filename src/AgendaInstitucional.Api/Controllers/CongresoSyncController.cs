using AgendaInstitucional.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CongresoSyncController : ControllerBase
{
    private readonly CongresoCatalogSyncService _syncService;

    public CongresoSyncController(CongresoCatalogSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("catalogos")]
    public async Task<ActionResult<CongresoCatalogSyncResponse>> SyncCatalogos([FromBody] SyncCatalogosRequest? request, CancellationToken cancellationToken)
    {
        if (!IsAdminUser(User))
        {
            return Forbid();
        }

        var response = await _syncService.SyncAsync(request?.ActiveVariableId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("catalogos/diff")]
    public async Task<ActionResult<CongresoCatalogDiffResponse>> GetCatalogosDiff([FromQuery] int? activeVariableId, CancellationToken cancellationToken)
    {
        if (!IsAdminUser(User))
        {
            return Forbid();
        }

        var response = await _syncService.GetDiffAsync(activeVariableId, cancellationToken);
        return Ok(response);
    }

    private static bool IsAdminUser(ClaimsPrincipal user)
    {
        return user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            && string.Equals(c.Value, "admin", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class SyncCatalogosRequest
{
    public int? ActiveVariableId { get; set; }
}