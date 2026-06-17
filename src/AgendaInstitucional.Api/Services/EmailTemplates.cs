namespace AgendaInstitucional.Api.Services;

public static class EmailTemplates
{
    public static string GenerarBodyAutorizacion(
        EmailSolicitudAutorizacionData detalle)
    {
        var fechaFormato = detalle.FechaEvento.HasValue ? detalle.FechaEvento.Value.ToString("dd/MM/yyyy") : "Por definir";
        var horaInicio = detalle.HoraInicio.HasValue ? detalle.HoraInicio.Value.ToString("HH:mm") : "--:--";
        var horaFin = detalle.HoraFin.HasValue ? detalle.HoraFin.Value.ToString("HH:mm") : "--:--";
        var horario = $"{horaInicio} - {horaFin}";
        var servicios = detalle.ServiciosSolicitados.Any()
            ? string.Join(", ", detalle.ServiciosSolicitados)
            : "No especificados";
        var autorizado = detalle.Autorizado ? "Sí" : "No";
        var estatus = detalle.Estatus ? "Activo" : "Inactivo";
        var displayEvent = !string.IsNullOrWhiteSpace(detalle.Comision) && !string.IsNullOrWhiteSpace(detalle.Evento)
            ? $"{detalle.Comision} - {detalle.Evento}"
            : detalle.Evento ?? "";

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; }
                    .header { background-color: #1e40af; color: white; padding: 20px; border-radius: 8px 8px 0 0; }
                    .content { padding: 20px; background-color: #f9fafb; }
                    .info-section { margin: 15px 0; padding: 10px; background-color: white; border-left: 4px solid #1e40af; }
                    .label { font-weight: bold; color: #1e40af; }
                    .footer { color: #666; font-size: 12px; margin-top: 20px; text-align: center; }
                </style>
            </head>
            <body>
                <div class="container">
                    <div class="header">
                        <h1>Notificación de Autorización de Solicitud</h1>
                    </div>
                    <div class="content">
                        <p>Estimado/a,</p>
                        <p>Le informamos que la siguiente solicitud ha sido <strong>autorizada</strong>:</p>
                        
                        <div class="info-section">
                            <p><span class="label">Solicitud #:</span> {{detalle.SolicitudId}}</p>
                            <p><span class="label">Evento:</span> {{displayEvent}}</p>
                            <p><span class="label">Asunto:</span> {{(detalle.Asunto ?? "-")}}</p>
                            <p><span class="label">Sala:</span> {{(detalle.Sala ?? "-")}}</p>
                            <p><span class="label">Tipo evento:</span> {{(detalle.TipoEvento ?? "-")}}</p>
                            <p><span class="label">Comisión:</span> {{(detalle.Comision ?? "-")}}</p>
                            <p><span class="label">Fecha:</span> {{fechaFormato}}</p>
                            <p><span class="label">Horario:</span> {{horario}}</p>
                            <p><span class="label">Autorizado:</span> {{autorizado}}</p>
                            <p><span class="label">Estatus:</span> {{estatus}}</p>
                            <p><span class="label">Notificar:</span> {{(detalle.UsuariosNotificarServicio ?? "-")}}</p>
                            <p><span class="label">Responsable del evento:</span> {{(detalle.ResponsableEvento ?? "-")}}</p>
                            <p><span class="label">Número de personas:</span> {{(detalle.NumeroPersonas?.ToString() ?? "-")}}</p>
                            <p><span class="label">Servicios Solicitados:</span> {{servicios}}</p>
                            <p><span class="label">Otro servicio:</span> {{(detalle.OtroServicioExtra ?? "-")}}</p>
                        </div>
                        
                        <p>Por favor, tome las acciones necesarias para confirmar la disponibilidad de los servicios solicitados.</p>
                        
                        <p>Saludos cordiales,<br>Sistema de Agenda Institucional</p>
                    </div>
                    <div class="footer">
                        <p>Este es un correo automático. Por favor no responder a este mensaje.</p>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    public static string ObtenerAsuntoAutorizacion(string? comision, string nombreEvento)
    {
        var displayEvent = !string.IsNullOrWhiteSpace(comision) && !string.IsNullOrWhiteSpace(nombreEvento)
            ? $"{comision} - {nombreEvento}"
            : nombreEvento;
        return $"Notificación: Autorización de solicitud - {displayEvent}";
    }
}
