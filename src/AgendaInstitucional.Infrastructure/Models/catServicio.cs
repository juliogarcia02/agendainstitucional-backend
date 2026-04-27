using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class catServicio
{
    public int id { get; set; }

    public string? servicio { get; set; }

    public bool? estatus { get; set; }

    public virtual ICollection<SolicitudServicio> SolicitudServicios { get; set; } = new List<SolicitudServicio>();

    public virtual ICollection<catServicioResponsable> catServicioResponsables { get; set; } = new List<catServicioResponsable>();
}
