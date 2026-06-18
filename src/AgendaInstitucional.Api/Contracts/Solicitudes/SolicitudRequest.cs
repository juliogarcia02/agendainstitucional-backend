namespace AgendaInstitucional.Api.Contracts.Solicitudes;

public class SolicitudRequest
{
    public string Evento { get; set; } = null!;

    public string? Asunto { get; set; }

    public string? ResponsableEvento { get; set; }

    public int? NumeroPersonas { get; set; }

    public int? SalaId { get; set; }

    public int? TipoEventoId { get; set; }

    public int? ComisionId { get; set; }

    public string? OtroServicioExtra { get; set; }

    public string? UsuariosNotificarServicio { get; set; }

    public string? Lugar { get; set; }

    public string? Direccion { get; set; }

    public string? Municipio { get; set; }

    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    public bool SinHoraExactaInicio { get; set; }

    public string? DependeParaIniciar { get; set; }

    public DateOnly? FechaEvento { get; set; }

    public TimeOnly? HoraInicio { get; set; }

    public TimeOnly? HoraFin { get; set; }

    public bool Autorizado { get; set; }

    public bool EventoInterno { get; set; }

    public bool Estatus { get; set; } = true;

    // Permite guardar aunque exista conflicto de diputados, bajo responsabilidad del usuario.
    public bool AceptaConflictoDiputados { get; set; }

    // Lista de servicios seleccionados en el checklist del formulario.
    public IReadOnlyCollection<int> ServicioIds { get; set; } = [];
}
