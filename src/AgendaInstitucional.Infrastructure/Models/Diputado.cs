using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class Diputado
{
    public int Id { get; set; }

    public string? Nombre { get; set; }

    public bool Estatus { get; set; }

    public bool? Actual { get; set; }

    public virtual ICollection<ComisionDiputado> ComisionesDiputados { get; set; } = new List<ComisionDiputado>();
}