namespace AgendaInstitucional.Api.Contracts.SolicitudServicios;

public class SolicitudServiciosReplaceRequest
{
    public IReadOnlyCollection<int> ServicioIds { get; set; } = [];
}