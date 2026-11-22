namespace RestaurantHandler.Utilities;

/// <summary>
/// Contrôle commun aux pièces jointes des cartes (ADR-0015) et du chat (ADR-0016), fait avant
/// tout envoi au stockage. <see cref="Integrations.Storage.StorageService"/> vérifie ensuite la
/// signature du contenu.
/// </summary>
public static class AttachmentRules
{
    /// <summary>Message de refus et statut (400, 413, 415), ou <c>null</c> si le fichier est admis.</summary>
    public static string? Refusal(IFormFile? file, out int status)
    {
        status = 400;
        if (file is null || file.Length == 0)
            return "Aucun fichier reçu.";

        status = 413;
        if (file.Length > HardCode.ATTACHMENT_MAX_SIZE_BYTES)
            return $"Fichier trop volumineux (maximum {HardCode.ATTACHMENT_MAX_SIZE_BYTES / (1024 * 1024)} Mo).";

        status = 415;
        var extension = Path.GetExtension(file.FileName);
        if (!HardCode.ATTACHMENT_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return "Seuls les fichiers JPEG, PNG, WebP et PDF sont acceptés.";

        return null;
    }
}
