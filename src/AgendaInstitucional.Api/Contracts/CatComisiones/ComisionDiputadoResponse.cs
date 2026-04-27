namespace AgendaInstitucional.Api.Contracts.CatComisiones;

public sealed class ComisionDiputadoResponse
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public bool Estatus { get; set; }
    public bool? Actual { get; set; }
}