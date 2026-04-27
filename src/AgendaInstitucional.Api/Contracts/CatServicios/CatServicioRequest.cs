namespace AgendaInstitucional.Api.Contracts.CatServicios;

public class CatServicioRequest
{
    public string? Servicio { get; set; }

    public bool? Estatus { get; set; } = true;
}
