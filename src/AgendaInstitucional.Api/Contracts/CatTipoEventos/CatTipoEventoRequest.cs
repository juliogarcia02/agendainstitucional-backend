namespace AgendaInstitucional.Api.Contracts.CatTipoEventos;

public class CatTipoEventoRequest
{
    public string? Evento { get; set; }

    public bool? Estatus { get; set; } = true;
}
