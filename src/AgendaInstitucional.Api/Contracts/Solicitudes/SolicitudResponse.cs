namespace AgendaInstitucional.Api.Contracts.Solicitudes;

public class SolicitudResponse
{
    public int Id { get; set; }

    public string Evento { get; set; } = null!;

    public string? Asunto { get; set; }

    public string? ResponsableEvento { get; set; }

    public int? NumeroPersonas { get; set; }

    public int? SalaId { get; set; }

    public string? Sala { get; set; }

    public int? TipoEventoId { get; set; }

    public string? TipoEvento { get; set; }

    public int? ComisionId { get; set; }

    public string? Comision { get; set; }

    public string? OtroServicioExtra { get; set; }

    public string? UsuariosNotificarServicio { get; set; }

    public bool SinHoraExactaInicio { get; set; }

    public string? DependeParaIniciar { get; set; }

    public DateOnly? FechaEvento { get; set; }

    public TimeOnly? HoraInicio { get; set; }

    public TimeOnly? HoraFin { get; set; }

    public bool Autorizado { get; set; }

    public bool EventoInterno { get; set; }

    public bool Estatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
