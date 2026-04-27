namespace AgendaInstitucional.Api.Contracts.CatServicioResponsables;

public class CatServicioResponsableRequest
{
    public int ServicioId { get; set; }

    public string? ResponsableNombre { get; set; }

    public string? ResponsableEmail { get; set; }

    public string? ResponsableTelefono { get; set; }

    public string? Observaciones { get; set; }

    public bool Estatus { get; set; } = true;
}
