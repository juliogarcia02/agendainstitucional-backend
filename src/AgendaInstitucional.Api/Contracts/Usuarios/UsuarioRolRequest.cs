namespace AgendaInstitucional.Api.Contracts.Usuarios;

public class UsuarioRolRequest
{
    /// <summary>"admin" o "user"</summary>
    public string Rol { get; set; } = string.Empty;
}
