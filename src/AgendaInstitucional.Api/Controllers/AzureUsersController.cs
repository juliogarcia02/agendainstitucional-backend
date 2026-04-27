using System.Net.Http.Headers;
using System.Text.Json;
using AgendaInstitucional.Api.Contracts.AzureUsers;
using AgendaInstitucional.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgendaInstitucional.Api.Controllers;

[ApiController]
[Route("api/azure-users")]
[Authorize]
public class AzureUsersController : ControllerBase
{
    private readonly ILogger<AzureUsersController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureGraphOptions _options;

    public AzureUsersController(
        ILogger<AzureUsersController> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AzureGraphOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AzureUserLookupResponse>>> Search(
        [FromQuery] string search,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            return Ok(Array.Empty<AzureUserLookupResponse>());

        if (string.IsNullOrWhiteSpace(_options.TenantId) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            return BadRequest(new
            {
                message = "AzureGraph no configurado. Define TenantId, ClientId y ClientSecret en configuración del API."
            });
        }

        var safeLimit = Math.Clamp(limit, 1, 25);

        var tokenResult = await AcquireAppToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            _logger.LogWarning(
                "Azure AD token acquisition failed. Status: {StatusCode}. Body: {Body}",
                tokenResult.StatusCode,
                tokenResult.ErrorBody);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "No se pudo obtener token de Azure AD.",
                detail = tokenResult.ErrorBody,
                status = tokenResult.StatusCode
            });
        }

        var trimmed = search.Trim();
        var escaped = trimmed.Replace("'", "''", StringComparison.Ordinal);
        var filter =
            $"startswith(displayName,'{escaped}') or startswith(mail,'{escaped}') or startswith(userPrincipalName,'{escaped}')";

        var url =
            $"{_options.GraphBaseUrl.TrimEnd('/')}/users?$select=id,displayName,mail,userPrincipalName&$top={safeLimit}&$filter={Uri.EscapeDataString(filter)}";

        var http = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new
            {
                message = "Error al consultar usuarios en Microsoft Graph.",
                detail = body
            });
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return Ok(Array.Empty<AzureUserLookupResponse>());
        }

        var users = new List<AzureUserLookupResponse>();
        foreach (var item in value.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idElement)
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
            var displayName = item.TryGetProperty("displayName", out var displayElement)
                ? displayElement.GetString() ?? string.Empty
                : string.Empty;
            var mail = item.TryGetProperty("mail", out var mailElement)
                ? mailElement.GetString() ?? string.Empty
                : string.Empty;
            var upn = item.TryGetProperty("userPrincipalName", out var upnElement)
                ? upnElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(mail) && string.IsNullOrWhiteSpace(upn))
                continue;

            users.Add(new AzureUserLookupResponse
            {
                Id = id,
                DisplayName = displayName,
                Email = !string.IsNullOrWhiteSpace(mail) ? mail : upn
            });
        }

        return Ok(users);
    }

    private async Task<TokenResult> AcquireAppToken(CancellationToken cancellationToken)
    {
        var tokenUrl = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";
        var http = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scope,
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new TokenResult
            {
                AccessToken = null,
                StatusCode = (int)response.StatusCode,
                ErrorBody = body
            };
        }

        using var document = JsonDocument.Parse(body);
        return new TokenResult
        {
            AccessToken = document.RootElement.TryGetProperty("access_token", out var token)
                ? token.GetString()
                : null,
            StatusCode = (int)response.StatusCode,
            ErrorBody = null
        };
    }

    private sealed class TokenResult
    {
        public string? AccessToken { get; init; }
        public int StatusCode { get; init; }
        public string? ErrorBody { get; init; }
    }
}
