namespace AgendaInstitucional.Api.Contracts.CatSalas;

public class CatSalaRequest
{
    public string? Sala { get; set; }

    public bool? Estatus { get; set; } = true;
}
