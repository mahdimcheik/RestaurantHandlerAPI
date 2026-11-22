using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Hangfire;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using RazorLight;

namespace RestaurantHandler.Services;

public class MailService
{
    // Le logo part en pièce jointe inline (CID) : Gmail bloque les data: URI et une
    // URL distante dépendrait du front et du « afficher les images » du client mail.
    private const string LOGO_CONTENT_ID = "bonprof-logo";

    private static readonly string TemplatesRoot = Path.Combine(AppContext.BaseDirectory, "Templates");
    private static readonly string LogoPath = Path.Combine(TemplatesRoot, "Assets", "logo.png");

    private readonly IRazorLightEngine _razorLightEngine;
    private readonly ILogger<MailService> _logger;

    public MailService(ILogger<MailService> logger)
    {
        _logger = logger;
        _razorLightEngine = new RazorLightEngineBuilder()
            .UseFileSystemProject(TemplatesRoot)
            .SetOperatingAssembly(typeof(MailService).Assembly)
            .UseMemoryCachingProvider()
            .Build();
    }

    public Task<string> RenderAsync<TModel>(string templateName, TModel model) =>
        _razorLightEngine.CompileRenderAsync(templateName, model);

    /// <summary>
    /// Met le mail en file Hangfire au lieu de l'envoyer sur place : la requête HTTP
    /// ne paie pas l'aller-retour SMTP, et un échec de Brevo est rejoué puis visible
    /// dans le dashboard au lieu d'être perdu.
    /// </summary>
    public virtual Task SendEmail(MailApp mail)
    {
        var mailInfos = new MailInfos(
            mail.MailFrom ?? EnvironmentVariables.DO_NO_REPLY_MAIL,
            mail.MailTo ?? "",
            mail.MailSubject ?? "",
            mail.MailBody ?? "",
            ReplyTo: mail.ReplyTo
        );

        BackgroundJob.Enqueue<MailService>(s => s.DeliverAsync(mailInfos));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Envoi SMTP effectif, exécuté par un worker Hangfire. Les exceptions remontent
    /// volontairement : c'est ce qui déclenche le rejeu.
    /// </summary>
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 30, 60, 120, 300 })]
    public async Task DeliverAsync(MailInfos mail)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(mail.From));
        message.To.Add(MailboxAddress.Parse(mail.To));
        message.Subject = mail.Subject;
        if (!string.IsNullOrWhiteSpace(mail.ReplyTo))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(mail.ReplyTo));
        }

        var builder = new BodyBuilder { HtmlBody = mail.Body };

        // Ajouté à l'envoi et non à la mise en file, pour ne pas stocker l'image
        // dans le JSON du job Hangfire.
        if (mail.Body.Contains($"cid:{LOGO_CONTENT_ID}") && File.Exists(LogoPath))
        {
            var logo = builder.LinkedResources.Add(LogoPath);
            logo.ContentId = LOGO_CONTENT_ID;
        }

        foreach (var attachment in mail.Attachments ?? [])
        {
            builder.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                ContentType.Parse(attachment.ContentType ?? "application/octet-stream")
            );
        }

        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        if (EnvironmentVariables.SMTP_ALLOW_SELF_SIGNED)
        {
            smtp.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }

        // 465 = TLS implicite ; 587 / 2525 (le cas de Brevo) = STARTTLS. Se tromper
        // ici fait échouer le handshake, pas seulement baisser la sécurité.
        var security =
            EnvironmentVariables.SMTP_PORT == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

        await smtp.ConnectAsync(
            EnvironmentVariables.SMTP_HOST,
            EnvironmentVariables.SMTP_PORT,
            security
        );

        if (!string.IsNullOrWhiteSpace(EnvironmentVariables.SMTP_LOGIN))
        {
            await smtp.AuthenticateAsync(
                EnvironmentVariables.SMTP_LOGIN,
                EnvironmentVariables.SMTP_KEY
            );
        }

        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        _logger.LogInformation("Mail « {Subject} » envoyé à {Recipient}.", mail.Subject, mail.To);
    }

    public async Task SendConfirmAccount(UserApp receiver, string confirmLink)
    {
        try
        {
            var model = new ConfirmMailModel(
                receiver,
                confirmLink,
                EnvironmentVariables.API_FRONT_URL
            );

            string template = await RenderAsync("ConfirmAccountTemplate.cshtml", model);

            MailApp mailApp = new MailApp
            {
                MailFrom = EnvironmentVariables.DO_NO_REPLY_MAIL,
                MailTo = receiver.Email!,
                MailSubject = "Activez votre compte BonProf",
                MailBody = template,
            };

            await SendEmail(mailApp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'envoi du mail de confirmation à {Recipient}.", receiver.Email);
        }
    }

    public async Task SendResetPasswordAccount(UserApp receiver, string confirmLink)
    {
        try
        {
            var model = new ResetPasswordModel(
                receiver,
                confirmLink,
                EnvironmentVariables.API_FRONT_URL
            );

            string template = await RenderAsync("ForgotPasswordTemplate.cshtml", model);

            MailApp mailApp = new MailApp
            {
                MailFrom = EnvironmentVariables.DO_NO_REPLY_MAIL,
                MailTo = receiver.Email!,
                MailSubject = "Réinitialisation de votre mot de passe BonProf",
                MailBody = template,
            };

            await SendEmail(mailApp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'envoi du mail de réinitialisation à {Recipient}.", receiver.Email);
        }
    }

    /// <summary>Un exemplaire d'envoi groupé (ADR-0023), adressé à un seul destinataire.</summary>
    public async Task SendBroadcast(string to, BroadcastMailModel model, string? replyTo)
    {
        string body = await RenderAsync("BroadcastTemplate.cshtml", model);

        await SendEmail(
            new MailApp
            {
                MailFrom = EnvironmentVariables.DO_NO_REPLY_MAIL,
                MailTo = to,
                MailSubject = model.Subject,
                MailBody = body,
                ReplyTo = replyTo,
            }
        );
    }
}
