namespace AgendaInstitucional.Api.Options;

public class AzureGraphOptions
{
    public const string SectionName = "AzureGraph";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";
    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
}
