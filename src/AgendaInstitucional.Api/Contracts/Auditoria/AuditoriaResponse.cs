namespace AgendaInstitucional.Api.Contracts.Auditoria;

public class AuditoriaResponse
{
    public long Id { get; set; }

    public DateTime FechaHora { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public string? LoginSql { get; set; }

    public string Accion { get; set; } = string.Empty;

    public string Tabla { get; set; } = string.Empty;

    public string? Llave { get; set; }

    public string? Antes { get; set; }

    public string? Despues { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }

    public string? Modulo { get; set; }

    public int? SolicitudId { get; set; }
}
