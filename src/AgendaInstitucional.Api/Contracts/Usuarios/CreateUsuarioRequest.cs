namespace AgendaInstitucional.Api.Contracts.Usuarios;

public class CreateUsuarioRequest
{
    public string Nombre { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>"admin" o "user"</summary>
    public string Rol { get; set; } = "user";
}
