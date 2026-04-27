namespace AgendaInstitucional.Api.Contracts.SolicitudServicios;

public class SolicitudServicioResponse
{
    public int SolicitudId { get; set; }

    public int ServicioId { get; set; }

    public string? Servicio { get; set; }

    public DateTime CreatedAt { get; set; }
}