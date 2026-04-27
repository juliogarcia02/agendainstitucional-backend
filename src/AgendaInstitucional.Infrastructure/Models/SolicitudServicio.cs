using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class SolicitudServicio
{
    public int SolicitudId { get; set; }

    public int ServicioId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual catServicio Servicio { get; set; } = null!;

    public virtual Solicitude Solicitud { get; set; } = null!;
}
