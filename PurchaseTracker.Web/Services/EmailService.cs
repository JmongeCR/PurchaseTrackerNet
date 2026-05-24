using System.Net;
using System.Net.Mail;

namespace PurchaseTracker.Web.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _config["Email:Host"];
        var user = _config["Email:User"];
        var pass = _config["Email:Pass"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            _logger.LogWarning("[EmailService] No configurado. Se omite envío a {To}", to);
            return;
        }

        if (!int.TryParse(_config["Email:Port"], out var port))
            port = 587;

        var fromName = _config["Email:FromName"] ?? "PurchaseTracker";

        try
        {
            var message = new MailMessage
            {
                From    = new MailAddress(user, fromName),
                Subject = subject,
                Body    = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl   = true
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("[EmailService] Correo enviado a {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmailService] Error al enviar correo a {To}", to);
            // No relanzar — el flujo del negocio no debe fallar por un email
        }
    }

    public async Task SendWelcomeEmailAsync(string to, string fullName, string password)
    {
        var appUrl = _config["AppUrl"] ?? "https://purchasetrackerhub.tryasp.net/";
        var subject = "Bienvenido a PurchaseTracker — Tus datos de acceso";

        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"></head>
            <body style="margin:0;padding:0;background:#f1f5f9;font-family:system-ui,-apple-system,sans-serif">
              <div style="max-width:520px;margin:40px auto;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 6px rgba(0,0,0,.07)">
                <div style="background:#0f172a;padding:28px 32px;text-align:center">
                  <div style="display:inline-block;background:#3b82f6;width:44px;height:44px;border-radius:10px;line-height:44px;font-size:20px;color:#fff;font-weight:700;text-align:center">PT</div>
                  <p style="color:#94a3b8;font-size:13px;margin:8px 0 0">Control financiero inteligente</p>
                </div>
                <div style="padding:32px">
                  <h2 style="color:#0f172a;margin:0 0 8px;font-size:22px">Hola, {fullName} 👋</h2>
                  <p style="color:#475569;line-height:1.6;margin:0 0 24px">
                    Tu cuenta en <strong>PurchaseTracker</strong> ha sido creada por un administrador.
                    Puedes ingresar con los siguientes datos:
                  </p>
                  <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:20px 24px;margin-bottom:24px">
                    <table style="width:100%;border-collapse:collapse">
                      <tr>
                        <td style="padding:6px 0;color:#64748b;font-size:13px;width:110px">Correo</td>
                        <td style="padding:6px 0;font-weight:600;color:#0f172a">{to}</td>
                      </tr>
                      <tr>
                        <td style="padding:6px 0;color:#64748b;font-size:13px">Contraseña</td>
                        <td style="padding:6px 0;font-weight:600;color:#0f172a;font-family:monospace;font-size:15px">{password}</td>
                      </tr>
                    </table>
                  </div>
                  <p style="color:#64748b;font-size:13px;margin:0 0 20px">
                    Por seguridad, te recomendamos cambiar tu contraseña la primera vez que ingreses.
                  </p>
                  <a href="{appUrl}/auth/login"
                     style="display:inline-block;background:#3b82f6;color:#ffffff;padding:12px 28px;border-radius:8px;text-decoration:none;font-weight:600;font-size:14px">
                    Ingresar al sistema →
                  </a>
                </div>
                <div style="padding:16px 32px;border-top:1px solid #f1f5f9;text-align:center">
                  <p style="color:#94a3b8;font-size:11px;margin:0">
                    Este correo fue generado automáticamente. Por favor no respondas a este mensaje.
                  </p>
                </div>
              </div>
            </body>
            </html>
            """;

        await SendAsync(to, subject, html);
    }
}
