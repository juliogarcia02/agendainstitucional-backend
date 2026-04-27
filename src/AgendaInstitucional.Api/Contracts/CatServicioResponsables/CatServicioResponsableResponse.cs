namespace AgendaInstitucional.Api.Contracts.CatServicioResponsables;

public class CatServicioResponsableResponse
{
    public int Id { get; set; }

    public int ServicioId { get; set; }

    public string? Servicio { get; set; }

    public string ResponsableNombre { get; set; } = null!;

    public string? ResponsableEmail { get; set; }

    public string? ResponsableTelefono { get; set; }

    public string? Observaciones { get; set; }

    public bool Estatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
