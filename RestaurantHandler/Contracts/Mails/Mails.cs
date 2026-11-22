namespace RestaurantHandler.Models;

// Contrat d’envoi de mail : ce que MailService remet à Hangfire, qui le persiste
// en JSON dans la table des jobs avant que DeliverAsync ne le passe à MailKit.
public record MailInfos(
    string From,
    string To,
    string Subject,
    string Body,
    IReadOnlyList<MailAttachment>? Attachments = null,
    string? ReplyTo = null
);

public record MailAttachment(string FileName, byte[] Content, string? ContentType = null);

public class MailApp
{
    public string? MailTo { get; set; }
    public string? MailSubject { get; set; }
    public string? MailBody { get; set; }
    public string? MailFrom { get; set; }
    public string? ReplyTo { get; set; }
    public bool SendCopy { get; set; } = false;
    public UserApp? Sender { get; set; }
    public UserApp? Receiver { get; set; }

}

public class ResetPasswordModel
{
    public UserApp Receiver { get; set; }
    public string ValidationLink { get; set; }
    public string WebsiteLink { get; set; }

    public ResetPasswordModel(UserApp receiver, string resetLink, string websiteLink)
    {
        Receiver = receiver;
        ValidationLink = resetLink;
        WebsiteLink = websiteLink;
    }
}

public class ConfirmMailModel
{
    public UserApp Receiver { get; set; }
    public string ConfirmLink { get; set; }
    public string WebsiteLink { get; set; }

    public ConfirmMailModel(UserApp receiver, string confirmLink, string websiteLink)
    {
        Receiver = receiver;
        ConfirmLink = confirmLink;
        WebsiteLink = websiteLink;
    }
}

public record EmailButton(string Href, string Label);

public class TeacherContactModel
{
    public UserApp Receiver { get; set; }
    public string StudentName { get; set; }
    public string StudentEmail { get; set; }
    public string Subject { get; set; }
    public string MessageHtml { get; set; }
    public string WebsiteLink { get; set; }

    public TeacherContactModel(
        UserApp receiver,
        UserApp student,
        string subject,
        string messageHtml,
        string websiteLink
    )
    {
        Receiver = receiver;
        StudentName = $"{student.FirstName} {student.LastName}".Trim();
        StudentEmail = student.Email ?? "";
        Subject = subject;
        MessageHtml = messageHtml;
        WebsiteLink = websiteLink;
    }
}

/// <summary>Modèle de BroadcastTemplate : un envoi groupé rendu pour un destinataire.</summary>
public class BroadcastMailModel
{
    public string RecipientFirstName { get; set; } = "";
    public string SenderName { get; set; } = "";
    public MailBroadcastSenderKind SenderKind { get; set; }
    public string Subject { get; set; } = "";
    public string MessageHtml { get; set; } = "";
    public string WebsiteLink { get; set; } = "";
    public bool AllowReply { get; set; }
}
