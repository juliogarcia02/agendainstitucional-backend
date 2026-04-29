using AgendaInstitucional.Api.Contracts.Common;
using AgendaInstitucional.Api.Contracts.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgendaInstitucional.Infrastructure.Data;
using System.Security.Claims;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private static readonly IReadOnlySet<string> RolesValidos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin", "user" };

    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppIdentityDbContext _identityDb;

    public UsuariosController(
        UserManager<IdentityUser> userManager,
        AppIdentityDbContext identityDb)
    {
        _userManager = userManager;
        _identityDb = identityDb;
    }

    // -----------------------------------------------------------------------
    // POST /api/usuarios  – Crear usuario con rol
    // -----------------------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<UsuarioResponse>> Create([FromBody] CreateUsuarioRequest request)
    {
        var nombre = request.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new { error = "El nombre es obligatorio." });

        var rol = request.Rol?.Trim().ToLowerInvariant() ?? "user";
        if (!RolesValidos.Contains(rol))
            return BadRequest(new { error = $"Rol inválido. Valores aceptados: {string.Join(", ", RolesValidos)}." });

        var user = new IdentityUser
        {
            UserName = nombre,
            Email = request.Email?.Trim(),
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(createResult.Errors);

        var roleResult = await _userManager.AddToRoleAsync(user, rol);
        if (!roleResult.Succeeded)
        {
            // Roll back user creation if role assignment fails
            await _userManager.DeleteAsync(user);
            return BadRequest(roleResult.Errors);
        }

        var claimResult = await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Name, nombre));
        if (!claimResult.Succeeded)
        {
            // Roll back user creation if name claim assignment fails
            await _userManager.DeleteAsync(user);
            return BadRequest(claimResult.Errors);
        }

        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            await ToResponseAsync(user));
    }

    // -----------------------------------------------------------------------
    // GET /api/usuarios/{id}
    // -----------------------------------------------------------------------
    [HttpGet("{id}")]
    public async Task<ActionResult<UsuarioResponse>> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        return Ok(await ToResponseAsync(user));
    }

    // -----------------------------------------------------------------------
    // GET /api/usuarios  – Listado paginado con filtros
    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UsuarioResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? estatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<IdentityUser> query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            var userIdsByName = _identityDb.UserClaims
                .Where(c => c.ClaimType == ClaimTypes.Name && c.ClaimValue != null && c.ClaimValue.Contains(value))
                .Select(c => c.UserId);

            query = query.Where(x =>
                (x.Email != null && x.Email.Contains(value)) ||
                (x.UserName != null && x.UserName.Contains(value)) ||
                userIdsByName.Contains(x.Id));
        }

        var now = DateTimeOffset.UtcNow;
        if (estatus.HasValue)
        {
            if (estatus.Value)
                query = query.Where(x => !x.LockoutEnabled || x.LockoutEnd == null || x.LockoutEnd <= now);
            else
                query = query.Where(x => x.LockoutEnabled && x.LockoutEnd != null && x.LockoutEnd > now);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(x => x.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UsuarioResponse>(users.Count);
        foreach (var u in users)
            items.Add(await ToResponseAsync(u));

        return Ok(new PagedResponse<UsuarioResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords
        });
    }

    // -----------------------------------------------------------------------
    // PUT /api/usuarios/{id}/estatus  – Activar / dar de baja
    // -----------------------------------------------------------------------
    [HttpPut("{id}/estatus")]
    public async Task<ActionResult<UsuarioResponse>> UpdateEstatus(
        string id,
        [FromBody] UsuarioEstatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (request.Estatus)
        {
            var r1 = await _userManager.SetLockoutEnabledAsync(user, false);
            if (!r1.Succeeded) return BadRequest(r1.Errors);
            var r2 = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!r2.Succeeded) return BadRequest(r2.Errors);
        }
        else
        {
            var r1 = await _userManager.SetLockoutEnabledAsync(user, true);
            if (!r1.Succeeded) return BadRequest(r1.Errors);
            var r2 = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            if (!r2.Succeeded) return BadRequest(r2.Errors);
        }

        return Ok(await ToResponseAsync(user));
    }

    // -----------------------------------------------------------------------
    // PUT /api/usuarios/{id}/rol  – Cambiar rol
    // -----------------------------------------------------------------------
    [HttpPut("{id}/rol")]
    public async Task<ActionResult<UsuarioResponse>> UpdateRol(
        string id,
        [FromBody] UsuarioRolRequest request)
    {
        var rol = request.Rol?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!RolesValidos.Contains(rol))
            return BadRequest(new { error = $"Rol inválido. Valores aceptados: {string.Join(", ", RolesValidos)}." });

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Remove all current roles then assign the new one
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded) return BadRequest(removeResult.Errors);
        }

        var addResult = await _userManager.AddToRoleAsync(user, rol);
        if (!addResult.Succeeded) return BadRequest(addResult.Errors);

        return Ok(await ToResponseAsync(user));
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------
    private async Task<UsuarioResponse> ToResponseAsync(IdentityUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var estatus = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now;
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var nombre = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        return new UsuarioResponse
        {
            Id = user.Id,
            Email = user.Email,
            UserName = !string.IsNullOrWhiteSpace(nombre) ? nombre : user.UserName,
            Estatus = estatus,
            LockoutEnd = user.LockoutEnd,
            Rol = roles.FirstOrDefault()
        };
    }
}

