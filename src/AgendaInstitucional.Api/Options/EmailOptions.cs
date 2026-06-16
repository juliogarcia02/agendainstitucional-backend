namespace AgendaInstitucional.Api.Options;

public class EmailOptions
{
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public bool EnableSsl { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public List<string> DefaultRecipients { get; set; } = [];
}
