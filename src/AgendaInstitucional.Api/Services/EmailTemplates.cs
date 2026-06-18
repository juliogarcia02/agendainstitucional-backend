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
        var esFueraDelCongreso = ContainsIgnoreAccents(detalle.Sala, "fuera del congreso");
        decimal latitud = 0;
        decimal longitud = 0;
        var coordenadasDisponibles = false;
        if (detalle.Latitud is decimal lat && detalle.Longitud is decimal lng)
        {
            latitud = lat;
            longitud = lng;
            coordenadasDisponibles = true;
        }

        var coordenadasTexto = coordenadasDisponibles
            ? $"{latitud:0.#######}, {longitud:0.#######}"
            : "-";
        var urlMapa = coordenadasDisponibles
            ? $"https://www.google.com/maps?q={latitud:0.#######},{longitud:0.#######}"
            : string.Empty;

        var ubicacionHtml = esFueraDelCongreso
            ? $$"""
                            <p><span class="label">Lugar:</span> {{(detalle.Lugar ?? "-")}}</p>
                            <p><span class="label">Dirección:</span> {{(detalle.Direccion ?? "-")}}</p>
                            <p><span class="label">Municipio:</span> {{(detalle.Municipio ?? "-")}}</p>
                            <p><span class="label">Coordenadas:</span> {{coordenadasTexto}}</p>
            """
            : string.Empty;

        var botonMapaHtml = esFueraDelCongreso && coordenadasDisponibles
            ? $$"""
                        <p style="margin-top: 14px;">
                            <a href="{{urlMapa}}" target="_blank" rel="noopener noreferrer" style="display:inline-block; padding:10px 14px; background-color:#383838; color:#f8f1e6; text-decoration:none; border-radius:6px; border:1px solid #c5ab81; font-weight:600;">Ver en mapa</a>
                        </p>
            """
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Roboto, Arial, sans-serif; line-height: 1.6; color: #383838; background-color: #f5f3ef; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #d8cebf; border-radius: 10px; background-color: #ffffff; }
                    .header { background-color: #383838; background-image: linear-gradient(90deg, #c5ab81 0%, #383838 100%); color: #ffffff; padding: 20px; border-radius: 8px 8px 0 0; }
                    .content { padding: 20px; background-color: #fdfaf5; }
                    .info-section { margin: 15px 0; padding: 10px; background-color: #ffffff; border-left: 4px solid #c5ab81; }
                    .label { font-weight: bold; color: #383838; }
                    .footer { color: #7a746b; font-size: 12px; margin-top: 20px; text-align: center; }
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
                            {{ubicacionHtml}}
                        </div>

                        {{botonMapaHtml}}
                        
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
