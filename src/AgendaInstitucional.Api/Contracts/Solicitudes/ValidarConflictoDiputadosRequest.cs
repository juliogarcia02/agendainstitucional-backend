namespace AgendaInstitucional.Api.Contracts.Solicitudes;

public sealed class ValidarConflictoDiputadosRequest
{
    public int ComisionId { get; set; }
    public DateOnly FechaEvento { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    /// <summary>
    /// Si se pasa, esa solicitud se excluye de la búsqueda de conflictos (útil al editar).
    /// </summary>
    public int? SolicitudId { get; set; }
}
