using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class Auditorium
{
    public long Id { get; set; }

    public DateTime FechaHora { get; set; }

    public string Usuario { get; set; } = null!;

    public string? LoginSql { get; set; }

    public string Accion { get; set; } = null!;

    public string Tabla { get; set; } = null!;

    public string? Llave { get; set; }

    public string? Antes { get; set; }

    public string? Despues { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }

    public string? Modulo { get; set; }

    public int? SolicitudId { get; set; }
}
