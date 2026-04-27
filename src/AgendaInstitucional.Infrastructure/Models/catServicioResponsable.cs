using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class catServicioResponsable
{
    public int Id { get; set; }

    public int ServicioId { get; set; }

    public string ResponsableNombre { get; set; } = null!;

    public string? ResponsableEmail { get; set; }

    public string? ResponsableTelefono { get; set; }

    public string? Observaciones { get; set; }

    public bool Estatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual catServicio Servicio { get; set; } = null!;
}
