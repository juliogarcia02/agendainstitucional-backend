namespace AgendaInstitucional.Infrastructure.Models;

public partial class ComisionDiputado
{
    public int ComisionId { get; set; }

    public int DiputadoId { get; set; }

    public virtual catComisione Comision { get; set; } = null!;

    public virtual Diputado Diputado { get; set; } = null!;
}