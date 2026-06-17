using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using AgendaInstitucional.Api.Options;

namespace AgendaInstitucional.Api.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> emailOptions, ILogger<SmtpEmailService> logger)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task EnviarNotificacionAutorizacionAsync(
        List<string> destinatarios,
        EmailSolicitudAutorizacionData detalle,
        CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled)
        {
            _logger.LogInformation("Envío de correos desactivado (Email:Enabled = false). Se omite notificación para el evento {NombreEvento}.", detalle.Evento);
            return;
        }

        try
        {
            ValidarConfiguracion();

            var smtpServer = _emailOptions.SmtpServer!;
            var smtpUsername = _emailOptions.SmtpUsername!;
            var smtpPassword = _emailOptions.SmtpPassword!;
            var fromEmail = _emailOptions.FromEmail!;
            var fromName = _emailOptions.FromName ?? "Sistema Agenda Institucional";

            var destinatariosConfigurados = _emailOptions.DefaultRecipients ?? [];
            var destinatariosTotales = destinatarios
                .Concat(destinatariosConfigurados)
                .ToList();

            // Filtrar destinatarios válidos con formato de email correcto
            var destinatariosValidos = destinatariosTotales
                .Where(x => !string.IsNullOrWhiteSpace(x) && EsEmailValido(x.Trim()))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!destinatariosValidos.Any())
            {
                _logger.LogWarning("No hay destinatarios con email válido para enviar la notificación de autorización. Emails recibidos: {Emails}",
                    string.Join(", ", destinatariosTotales));
                return;
            }

            var asunto = EmailTemplates.ObtenerAsuntoAutorizacion(detalle.Comision, detalle.Evento);
            var body = EmailTemplates.GenerarBodyAutorizacion(detalle);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));

            foreach (var email in destinatariosValidos)
            {
                message.To.Add(new MailboxAddress(string.Empty, email));
            }

            message.Subject = asunto;
            var builder = new BodyBuilder { HtmlBody = body };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var secureSocket = _emailOptions.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(smtpServer, _emailOptions.SmtpPort, secureSocket, cancellationToken);
            await client.AuthenticateAsync(smtpUsername, smtpPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "Correo de autorización enviado exitosamente a {DestinatarioCount} destinatarios para el evento {NombreEvento}",
                destinatariosValidos.Count,
                detalle.Evento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo de autorización para el evento {NombreEvento}", detalle.Evento);
            // No relanzamos la excepción para no fallar la actualización de la solicitud
            // pero se registra en los logs para seguimiento
        }
    }

    public async Task PruebaSMTPAsync(CancellationToken cancellationToken = default)
    {
        ValidarConfiguracion();

        var smtpServer = _emailOptions.SmtpServer!;
        var smtpUsername = _emailOptions.SmtpUsername!;
        var smtpPassword = _emailOptions.SmtpPassword!;
        var fromEmail = _emailOptions.FromEmail!;
        var fromName = _emailOptions.FromName ?? "Sistema Agenda Institucional";

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(string.Empty, fromEmail));
            message.Subject = "Prueba de Conexión SMTP";
            message.Body = new TextPart("plain")
            {
                Text = "Este es un correo de prueba del sistema Agenda Institucional."
            };

            using var client = new SmtpClient();
            var secureSocket = _emailOptions.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(smtpServer, _emailOptions.SmtpPort, secureSocket, cancellationToken);
            await client.AuthenticateAsync(smtpUsername, smtpPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Prueba SMTP completada exitosamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la conexión SMTP");
            throw;
        }
    }

    private void ValidarConfiguracion()
    {
        if (string.IsNullOrWhiteSpace(_emailOptions.SmtpServer))
            throw new InvalidOperationException("SmtpServer no está configurado en appsettings.json");

        if (string.IsNullOrWhiteSpace(_emailOptions.SmtpUsername))
            throw new InvalidOperationException("SmtpUsername no está configurado en appsettings.json");

        if (string.IsNullOrWhiteSpace(_emailOptions.SmtpPassword))
            throw new InvalidOperationException("SmtpPassword no está configurado en appsettings.json");

        if (string.IsNullOrWhiteSpace(_emailOptions.FromEmail))
            throw new InvalidOperationException("FromEmail no está configurado en appsettings.json");
    }

    private static bool EsEmailValido(string email)
    {
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
    }
}
