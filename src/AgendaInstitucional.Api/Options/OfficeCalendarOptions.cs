namespace AgendaInstitucional.Api.Options;

public class OfficeCalendarOptions
{
    public const string SectionName = "OfficeCalendar";

    public bool Enabled { get; set; }

    public string OrganizerEmail { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "America/Mexico_City";

    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
}
