namespace AgendaInstitucional.Api.Contracts.CatComisiones;

public class CatComisionRequest
{
    public string? Comision { get; set; }

    public bool? Estatus { get; set; } = true;
}
