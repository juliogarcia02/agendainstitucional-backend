using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgendaInstitucional.Api.Options;
using Microsoft.Extensions.Options;

namespace AgendaInstitucional.Api.Services;

public sealed class OfficeCalendarService : IOfficeCalendarService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureGraphOptions _azureOptions;
    private readonly OfficeCalendarOptions _calendarOptions;
    private readonly ILogger<OfficeCalendarService> _logger;

    public OfficeCalendarService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureGraphOptions> azureOptions,
        IOptions<OfficeCalendarOptions> calendarOptions,
        ILogger<OfficeCalendarService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _azureOptions = azureOptions.Value;
        _calendarOptions = calendarOptions.Value;
        _logger = logger;
    }

    public async Task<OfficeCalendarSyncResult> SyncSolicitudAsync(
        OfficeCalendarSolicitudData solicitud,
        CancellationToken cancellationToken = default)
    {
        if (!_calendarOptions.Enabled)
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "Office Calendar sync está deshabilitado en configuración.",
                Action = "disabled"
            };
        }

        if (string.IsNullOrWhiteSpace(_calendarOptions.OrganizerEmail))
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "No se configuró OfficeCalendar:OrganizerEmail.",
                Action = "invalid-config"
            };
        }

        if (solicitud.FechaEvento is null)
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "La solicitud no tiene fecha de evento.",
                Action = "invalid-event"
            };
        }

        if (string.IsNullOrWhiteSpace(_azureOptions.TenantId) ||
            string.IsNullOrWhiteSpace(_azureOptions.ClientId) ||
            string.IsNullOrWhiteSpace(_azureOptions.ClientSecret))
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "AzureGraph no está configurado correctamente para Microsoft Graph.",
                Action = "invalid-config"
            };
        }

        try
        {
            var token = await AcquireAppTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new OfficeCalendarSyncResult
                {
                    Success = false,
                    Message = "No se pudo obtener token de Microsoft Graph.",
                    Action = "auth-error"
                };
            }

            var organizerEmail = _calendarOptions.OrganizerEmail.Trim();
            var payload = BuildEventPayload(solicitud);

            if (!string.IsNullOrWhiteSpace(solicitud.OfficeEventId))
            {
                var updated = await PatchEventAsync(
                    organizerEmail,
                    solicitud.OfficeEventId,
                    payload,
                    token,
                    cancellationToken);

                if (updated.Success)
                {
                    updated.Action = "updated";
                    return updated;
                }

                // Si no existe en Outlook, intentar crearlo de nuevo para recuperar sincronización.
                if (updated.Action == "not-found")
                {
                    _logger.LogInformation(
                        "Evento Outlook {EventId} no encontrado para solicitud {SolicitudId}. Intentando recrear.",
                        solicitud.OfficeEventId,
                        solicitud.SolicitudId);
                }
                else
                {
                    return updated;
                }
            }

            var created = await CreateEventAsync(
                organizerEmail,
                payload,
                token,
                cancellationToken);

            created.Action = "created";
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sincronizando solicitud {SolicitudId} con Office Calendar.", solicitud.SolicitudId);
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = ex.Message,
                Action = "exception"
            };
        }
    }

    public async Task<OfficeCalendarSyncResult> DeleteEventAsync(
        string officeEventId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(officeEventId))
        {
            return new OfficeCalendarSyncResult
            {
                Success = true,
                Message = "No hay evento de Outlook para eliminar.",
                Action = "deleted-noop"
            };
        }

        if (!_calendarOptions.Enabled)
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "Office Calendar sync está deshabilitado en configuración.",
                Action = "disabled"
            };
        }

        if (string.IsNullOrWhiteSpace(_calendarOptions.OrganizerEmail))
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "No se configuró OfficeCalendar:OrganizerEmail.",
                Action = "invalid-config"
            };
        }

        if (string.IsNullOrWhiteSpace(_azureOptions.TenantId) ||
            string.IsNullOrWhiteSpace(_azureOptions.ClientId) ||
            string.IsNullOrWhiteSpace(_azureOptions.ClientSecret))
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "AzureGraph no está configurado correctamente para Microsoft Graph.",
                Action = "invalid-config"
            };
        }

        try
        {
            var token = await AcquireAppTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new OfficeCalendarSyncResult
                {
                    Success = false,
                    Message = "No se pudo obtener token de Microsoft Graph.",
                    Action = "auth-error"
                };
            }

            var organizerEmail = _calendarOptions.OrganizerEmail.Trim();
            var endpoint =
                $"{_calendarOptions.GraphBaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(organizerEmail)}/events/{Uri.EscapeDataString(officeEventId)}";

            var http = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new OfficeCalendarSyncResult
                {
                    Success = true,
                    Message = "El evento ya no existe en Outlook.",
                    Action = "deleted-not-found",
                    OrganizerEmail = organizerEmail
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new OfficeCalendarSyncResult
                {
                    Success = false,
                    Message = $"Graph delete error {(int)response.StatusCode}: {body}",
                    Action = "delete-error"
                };
            }

            return new OfficeCalendarSyncResult
            {
                Success = true,
                Message = "Evento eliminado de Outlook Calendar.",
                Action = "deleted",
                OrganizerEmail = organizerEmail
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando evento {OfficeEventId} en Office Calendar.", officeEventId);
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = ex.Message,
                Action = "exception"
            };
        }
    }

    private async Task<string?> AcquireAppTokenAsync(CancellationToken cancellationToken)
    {
        var tokenUrl = $"https://login.microsoftonline.com/{_azureOptions.TenantId}/oauth2/v2.0/token";
        var http = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _azureOptions.ClientId,
                ["client_secret"] = _azureOptions.ClientSecret,
                ["scope"] = _azureOptions.Scope,
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Error obteniendo token de Graph. Status: {StatusCode}. Body: {Body}",
                response.StatusCode,
                body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("access_token", out var accessToken)
            ? accessToken.GetString()
            : null;
    }

    private object BuildEventPayload(OfficeCalendarSolicitudData solicitud)
    {
        var start = solicitud.FechaEvento!.Value.ToDateTime(solicitud.HoraInicio ?? new TimeOnly(9, 0));
        var end = solicitud.HoraFin.HasValue
            ? solicitud.FechaEvento.Value.ToDateTime(solicitud.HoraFin.Value)
            : start.AddHours(1);

        if (end <= start)
        {
            end = start.AddHours(1);
        }

        var subject = !string.IsNullOrWhiteSpace(solicitud.Comision)
            ? $"{solicitud.Comision} - {solicitud.Evento}"
            : solicitud.Evento;

        var location = BuildLocation(solicitud);
        var bodyHtml = BuildBodyHtml(solicitud);
        var timeZone = string.IsNullOrWhiteSpace(_calendarOptions.TimeZone)
            ? "America/Mexico_City"
            : _calendarOptions.TimeZone;

        return new
        {
            subject,
            body = new
            {
                contentType = "HTML",
                content = bodyHtml
            },
            start = new
            {
                dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"),
                timeZone
            },
            end = new
            {
                dateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"),
                timeZone
            },
            location = new
            {
                displayName = location
            }
        };
    }

    private static string BuildLocation(OfficeCalendarSolicitudData solicitud)
    {
        if (!string.IsNullOrWhiteSpace(solicitud.Sala) &&
            !ContainsIgnoreAccents(solicitud.Sala, "fuera del congreso"))
        {
            return solicitud.Sala;
        }

        var parts = new[] { solicitud.Lugar, solicitud.Direccion, solicitud.Municipio }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        if (parts.Count > 0)
        {
            return string.Join(" | ", parts);
        }

        return solicitud.Sala ?? "Evento institucional";
    }

    private static string BuildBodyHtml(OfficeCalendarSolicitudData solicitud)
    {
        var fecha = solicitud.FechaEvento?.ToString("dd/MM/yyyy") ?? "-";
        var horaInicio = solicitud.HoraInicio?.ToString("HH:mm") ?? "--:--";
        var horaFin = solicitud.HoraFin?.ToString("HH:mm") ?? "--:--";
        var evento = !string.IsNullOrWhiteSpace(solicitud.Comision)
            ? $"{solicitud.Comision} - {solicitud.Evento}"
            : solicitud.Evento;

        return $"""
<div style=\"font-family: Roboto, Arial, sans-serif; color: #383838; line-height: 1.5;\">
  <h3 style=\"margin: 0 0 10px 0; color: #383838;\">Agenda Institucional</h3>
  <p style=\"margin: 0 0 8px 0;\"><strong>Solicitud #:</strong> {solicitud.SolicitudId}</p>
  <p style=\"margin: 0 0 8px 0;\"><strong>Evento:</strong> {evento}</p>
  <p style=\"margin: 0 0 8px 0;\"><strong>Asunto:</strong> {solicitud.Asunto ?? "-"}</p>
  <p style=\"margin: 0 0 8px 0;\"><strong>Sala:</strong> {solicitud.Sala ?? "-"}</p>
  <p style=\"margin: 0 0 8px 0;\"><strong>Tipo de evento:</strong> {solicitud.TipoEvento ?? "-"}</p>
  <p style=\"margin: 0 0 8px 0;\"><strong>Fecha:</strong> {fecha}</p>
  <p style=\"margin: 0 0 8px 0;\"><strong>Horario:</strong> {horaInicio} - {horaFin}</p>
</div>
""";
    }

    private async Task<OfficeCalendarSyncResult> CreateEventAsync(
        string organizerEmail,
        object payload,
        string token,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{_calendarOptions.GraphBaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(organizerEmail)}/events";
        var http = _httpClientFactory.CreateClient();
        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = $"Graph create error {(int)response.StatusCode}: {body}",
                Action = "create-error"
            };
        }

        using var doc = JsonDocument.Parse(body);
        return new OfficeCalendarSyncResult
        {
            Success = true,
            Message = "Evento creado en Outlook Calendar.",
            EventId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null,
            ICalUId = doc.RootElement.TryGetProperty("iCalUId", out var ical) ? ical.GetString() : null,
            WebLink = doc.RootElement.TryGetProperty("webLink", out var link) ? link.GetString() : null,
            OrganizerEmail = organizerEmail
        };
    }

    private async Task<OfficeCalendarSyncResult> PatchEventAsync(
        string organizerEmail,
        string officeEventId,
        object payload,
        string token,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{_calendarOptions.GraphBaseUrl.TrimEnd('/')}/users/{Uri.EscapeDataString(organizerEmail)}/events/{Uri.EscapeDataString(officeEventId)}";
        var http = _httpClientFactory.CreateClient();
        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = "Evento previo no encontrado en Outlook.",
                Action = "not-found"
            };
        }

        if (!response.IsSuccessStatusCode)
        {
            return new OfficeCalendarSyncResult
            {
                Success = false,
                Message = $"Graph update error {(int)response.StatusCode}: {body}",
                Action = "update-error"
            };
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new OfficeCalendarSyncResult
            {
                Success = true,
                Message = "Evento actualizado en Outlook Calendar.",
                EventId = officeEventId,
                OrganizerEmail = organizerEmail
            };
        }

        using var doc = JsonDocument.Parse(body);
        return new OfficeCalendarSyncResult
        {
            Success = true,
            Message = "Evento actualizado en Outlook Calendar.",
            EventId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : officeEventId,
            ICalUId = doc.RootElement.TryGetProperty("iCalUId", out var ical) ? ical.GetString() : null,
            WebLink = doc.RootElement.TryGetProperty("webLink", out var link) ? link.GetString() : null,
            OrganizerEmail = organizerEmail
        };
    }

    private static bool ContainsIgnoreAccents(string? source, string value)
    {
        return NormalizeText(source).Contains(NormalizeText(value), StringComparison.Ordinal);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Aggregate(string.Empty, static (current, c) => current + c)
            .ToLowerInvariant()
            .Trim();
    }
}
