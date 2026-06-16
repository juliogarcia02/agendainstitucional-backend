namespace AgendaInstitucional.Api.Services;

public interface IEmailService
{
    /// <summary>
    /// Envía un correo de autorización de solicitud a los destinatarios
    /// </summary>
    /// <param name="destinatarios">Lista de direcciones de correo electrónico</param>
    /// <param name="detalle">Datos completos de la solicitud autorizada</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Task que completa cuando el correo se envía</returns>
    Task EnviarNotificacionAutorizacionAsync(
        List<string> destinatarios,
        EmailSolicitudAutorizacionData detalle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prueba la conexión SMTP
    /// </summary>
    Task PruebaSMTPAsync(CancellationToken cancellationToken = default);
}

public class EmailSolicitudAutorizacionData
{
    public int SolicitudId { get; set; }
    public string Evento { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public string? Sala { get; set; }
    public string? TipoEvento { get; set; }
    public string? Comision { get; set; }
    public DateOnly? FechaEvento { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
    public bool Autorizado { get; set; }
    public bool Estatus { get; set; }
    public string? UsuariosNotificarServicio { get; set; }
    public string? ResponsableEvento { get; set; }
    public int? NumeroPersonas { get; set; }
    public string? OtroServicioExtra { get; set; }
    public List<string> ServiciosSolicitados { get; set; } = [];
}
