## Módulo de Notificaciones por Correo - Guía de Configuración

### Descripción General
Este módulo envía notificaciones por correo automáticamente cuando un administrador autoriza una solicitud en la sección "Agenda General". Los correos se envían a:
1. **La persona que hizo la solicitud**
2. **Los responsables de los servicios solicitados**

---

## 🔧 Configuración

### 1. **Configurar appsettings.json**

En `appsettings.json` (o `appsettings.Development.json` para desarrollo), actualiza la sección `Email`:

```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",        // Servidor SMTP
    "SmtpPort": 587,                       // Puerto SMTP
    "SmtpUsername": "tu-correo@gmail.com", // Usuario SMTP
    "SmtpPassword": "tu-contraseña-app",   // Contraseña o token de app
    "FromEmail": "tu-correo@gmail.com",    // Correo desde el que enviarás
    "FromName": "Agenda Institucional",    // Nombre visible en los correos
    "EnableSsl": true                      // Usar TLS/SSL para conexión segura
  }
}
```

### 2. **Opciones de Servidores SMTP**

#### **Gmail**
```json
{
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUsername": "tu-correo@gmail.com",
  "SmtpPassword": "tu-contraseña-app",  // Usar App Password, no contraseña normal
  "EnableSsl": true
}
```
**Nota:** Para Gmail, necesitas:
- Habilitar autenticación de 2 factores
- Generar una "App Password" en https://myaccount.google.com/apppasswords

#### **Servidor SMTP Corporativo**
```json
{
  "SmtpServer": "smtp.tudominio.com",
  "SmtpPort": 587,
  "SmtpUsername": "usuario@tudominio.com",
  "SmtpPassword": "tu-contraseña",
  "EnableSsl": true
}
```

#### **Servidor SMTP sin Autenticación (Local)**
```json
{
  "SmtpServer": "localhost",
  "SmtpPort": 25,
  "SmtpUsername": "",
  "SmtpPassword": "",
  "EnableSsl": false
}
```

---

## 📧 Archivos Creados/Modificados

### Nuevos Archivos:

| Archivo | Descripción |
|---------|-------------|
| `src/AgendaInstitucional.Api/Services/IEmailService.cs` | Interfaz del servicio de correos |
| `src/AgendaInstitucional.Api/Services/SmtpEmailService.cs` | Implementación SMTP del servicio |
| `src/AgendaInstitucional.Api/Services/EmailTemplates.cs` | Templates HTML de correos |
| `src/AgendaInstitucional.Api/Options/EmailOptions.cs` | Opciones de configuración |

### Archivos Modificados:

| Archivo | Cambios |
|---------|---------|
| `Program.cs` | Registrar `IEmailService` y configuración `EmailOptions` |
| `SolicitudesController.cs` | Inyectar `IEmailService` y enviar correos al autorizar |
| `appsettings.json` | Agregar sección `Email` con configuración SMTP |

---

## 🔄 Flujo de Funcionamiento

1. **Admin accede a Agenda General** → Sección "Pendientes"
2. **Admin hace clic en el botón "Editar"** de una solicitud
3. **Admin cambia "Autorizado" a "Si"** y guarda
4. **Backend detecta el cambio** (false → true)
5. **Se activa el envío de correos a:**
   - El correo del creador de la solicitud
   - Los correos de todos los responsables de los servicios solicitados
6. **Se envía un correo HTML** con detalles de la solicitud

---

## 📝 Contenido del Correo

El correo incluye:
- **Título:** "Notificación de Autorización de Solicitud"
- **Solicitante:** Responsable del evento
- **Evento:** Nombre del evento/solicitud
- **Fecha:** Fecha del evento
- **Servicios Solicitados:** Lista de todos los servicios solicitados

Ejemplo:
```
███████████████████████████████████████████
  Notificación de Autorización de Solicitud
███████████████████████████████████████████

Estimado/a,

Le informamos que la siguiente solicitud ha sido autorizada:

┌─────────────────────────────────────┐
│ Solicitante: Juan García Mendez     │
│ Evento: Sesión Plenaria #5          │
│ Fecha: 15/06/2026                   │
│ Servicios: Catering, Sonido, Video  │
└─────────────────────────────────────┘

Por favor, tome las acciones necesarias para confirmar 
la disponibilidad de los servicios solicitados.

Saludos cordiales,
Sistema de Agenda Institucional
```

---

## 🧪 Pruebas

### Prueba de Conectividad SMTP

Puedes hacer un POST a un endpoint de prueba que crearemos:

```bash
POST /solicitudes/prueba-email
```

Respuesta exitosa:
```json
{
  "success": true,
  "message": "Correo de prueba enviado exitosamente"
}
```

---

## ⚠️ Manejo de Errores

- Si **no hay destinatarios válidos**, el correo se omite silenciosamente (se registra en logs)
- Si **hay error en la conexión SMTP**, se registra en logs pero **NO falla** la autorización de la solicitud
- Si **la configuración está incompleta**, se lanza excepción al iniciar la aplicación

---

## 🔐 Seguridad

✅ **Buenas prácticas implementadas:**
- Las contraseñas SMTP NO se registran en logs
- Se filtra duplicados y correos inválidos
- Se usa TLS/SSL para conexiones seguras
- No se relanza excepción de email para no interrumpir operaciones críticas

❌ **Cuidado:**
- NO guardes contraseñas en appsettings.json en producción
- Usa **Secrets Manager** o **variables de entorno**
- Usa **App Passwords** para servicios como Gmail, no contraseñas normales

---

## 📋 Próximos Pasos

1. ✅ Configurar credenciales SMTP en `appsettings.json`
2. ✅ Crear un usuario test para verificar flujo completo
3. ✅ Revisar logs para confirmar envíos correctos
4. ✅ (Opcional) Crear endpoint `/solicitudes/prueba-email` para testing

---

## 📞 Estructura de Clases

### IEmailService
```csharp
public interface IEmailService
{
    Task EnviarNotificacionAutorizacionAsync(
        List<string> destinatarios,
        string nombreSolicitante,
        string nombreEvento,
        DateOnly? fecha,
        List<string> serviciosSolicitados,
        CancellationToken cancellationToken = default);

    Task PruebaSMTPAsync(CancellationToken cancellationToken = default);
}
```

### EmailOptions
```csharp
public class EmailOptions
{
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public bool EnableSsl { get; set; } = true;
}
```
