namespace AgendaInstitucional.Api.Contracts.Solicitudes;

public class NotificacionAutorizacionPreviewResponse
{
    public int SolicitudId { get; set; }
    public List<string> Destinatarios { get; set; } = [];
}
