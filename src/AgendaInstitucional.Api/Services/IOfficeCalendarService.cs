namespace AgendaInstitucional.Api.Services;

public interface IOfficeCalendarService
{
    Task<OfficeCalendarSyncResult> SyncSolicitudAsync(
        OfficeCalendarSolicitudData solicitud,
        CancellationToken cancellationToken = default);
}

public sealed class OfficeCalendarSolicitudData
{
    public int SolicitudId { get; set; }
    public string Evento { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public string? Comision { get; set; }
    public string? Sala { get; set; }
    public string? TipoEvento { get; set; }
    public string? Lugar { get; set; }
    public string? Direccion { get; set; }
    public string? Municipio { get; set; }
    public DateOnly? FechaEvento { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
    public string? OfficeEventId { get; set; }
}

public sealed class OfficeCalendarSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Action { get; set; } = "none";
    public string? EventId { get; set; }
    public string? ICalUId { get; set; }
    public string? WebLink { get; set; }
    public string? OrganizerEmail { get; set; }
}
