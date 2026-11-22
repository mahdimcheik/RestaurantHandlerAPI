using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class UserApp : IdentityUser<Guid>, IArchivable, IUpdateable, ICreatable
{
    [Required]
    [MaxLength(64)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public required DateTimeOffset DateOfBirth { get; set; }

    [MaxLength(500)]
    public string? ImgUrl { get; set; }

    [Required]
    public required bool DataProcessingConsent { get; set; } = false;

    [Required]
    public required bool PrivacyPolicyConsent { get; set; } = false;

    // Dernière acceptation des CGU : à comparer à HardCode.TERMS_VERSION.
    public DateTimeOffset? TermsAcceptedAt { get; set; }

    [MaxLength(32)]
    public string? TermsVersion { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;

    // Status account
    [Required]
    [ForeignKey(nameof(Status))]
    public Guid StatusId { get; set; }
    public StatusAccount? Status { get; set; }

    // Dernier envoi du lien d'activation, pour limiter les renvois.
    public DateTimeOffset? ConfirmationEmailSentAt { get; set; }

    // notifications
    public ICollection<Notification> Notifications { get; set; }

    // roles
    public ICollection<IdentityUserRole<Guid>> UserRoles { get; set; } = new List<IdentityUserRole<Guid>>();

    [SetsRequiredMembers]
    public UserApp() { }

    [SetsRequiredMembers]
    public UserApp(UserCreate newUser)
    {
        UserName = newUser.Email;
        Email = newUser.Email;
        FirstName = newUser.FirstName;
        LastName = newUser.LastName;
        DateOfBirth = newUser.DateOfBirth;
        DataProcessingConsent = true;
        PrivacyPolicyConsent = true;
        StatusId = HardCode.ACCOUNT_PENDING;
    }

    public void AcceptTerms()
    {
        TermsAcceptedAt = DateTimeOffset.UtcNow;
        TermsVersion = HardCode.TERMS_VERSION;
    }
}
