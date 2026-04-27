using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class catComisione
{
    public int id { get; set; }

    public string? comision { get; set; }

    public bool? estatus { get; set; }

    public virtual ICollection<ComisionDiputado> ComisionesDiputados { get; set; } = new List<ComisionDiputado>();

    public virtual ICollection<Solicitude> Solicitudes { get; set; } = new List<Solicitude>();
}
