using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class catTipoEvento
{
    public int id { get; set; }

    public string? evento { get; set; }

    public bool? estatus { get; set; }

    public virtual ICollection<Solicitude> Solicitudes { get; set; } = new List<Solicitude>();
}
