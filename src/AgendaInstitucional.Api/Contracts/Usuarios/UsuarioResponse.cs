namespace AgendaInstitucional.Api.Contracts.Usuarios;

public class UsuarioResponse
{
    public string Id { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public bool Estatus { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Primer rol asignado ("admin" | "user" | null)</summary>
    public string? Rol { get; set; }
}
