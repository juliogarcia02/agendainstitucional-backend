namespace AgendaInstitucional.Api.Contracts.Solicitudes;

public sealed class ValidarConflictoDiputadosResponse
{
    public bool HasConflict { get; set; }
    public List<ConflictoDiputadoItem> Conflictos { get; set; } = [];
}

public sealed class ConflictoDiputadoItem
{
    public int DiputadoId { get; set; }
    public string? DiputadoNombre { get; set; }
    public int SolicitudConflictoId { get; set; }
    public string Evento { get; set; } = null!;
    public string? Sala { get; set; }
    public string? Comision { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
}
