using System;
using System.Collections.Generic;

namespace AgendaInstitucional.Infrastructure.Models;

public partial class Usuario
{
    public int Id { get; set; }

    public string Usuario1 { get; set; } = null!;

    public string NombreCompleto { get; set; } = null!;

    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Rol { get; set; } = null!;

    public bool Estatus { get; set; }

    public DateTime? UltimoAccesoAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
