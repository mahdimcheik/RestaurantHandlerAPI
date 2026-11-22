using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>
/// Solde d'un prof, dérivé du ledger (aucune valeur stockée) : la somme de ses lignes NON
/// réglées (<c>PayoutId == null</c>), éclatée entre ce qu'on lui doit et ce qu'il doit.
/// <para>
/// <see cref="Debt"/> n'est pas une anomalie : un prof qui annule un cours payé se voit
/// facturer une pénalité alors que son wallet est le plus souvent vide. Elle se solde
/// d'elle-même, par compensation sur ses versements suivants — d'où le besoin de la lui
/// afficher, et de la surveiller côté admin quand elle stagne.
/// </para>
/// </summary>
public class TeacherWalletBalance
{
    /// <summary>Crédits acquis pas encore versés (part prof des cours payés).</summary>
    public decimal Available { get; set; }

    /// <summary>Ce que le prof doit à la plateforme (pénalités d'annulation, clawbacks). Positif.</summary>
    public decimal Debt { get; set; }

    /// <summary>Solde net = <see cref="Available"/> − <see cref="Debt"/>. Négatif = le prof est débiteur.</summary>
    public decimal Balance { get; set; }

    /// <summary>Versements figés en attente de confirmation Stripe (argent déjà engagé).</summary>
    public decimal PendingPayouts { get; set; }
}

/// <summary>
/// Lookup DTO for a <see cref="TypeTeacherTransaction"/> (earning, refund, payout...).
/// </summary>
public class TypeTeacherTransactionDetails
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    [Required]
    public Dictionary<string, string> Translations { get; set; } = new();

    public TypeTeacherTransactionDetails() { }

    public TypeTeacherTransactionDetails(TypeTeacherTransaction type)
    {
        Id = type.Id;
        Name = type.Name;
        Color = type.Color;
        Icon = type.Icon;
        Translations = type.Translations;
    }
}

/// <summary>
/// Light DTO used in the paginated list of a teacher's wallet transactions / payouts.
/// </summary>
public class TeacherWalletTransactionDetails
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public decimal Amount { get; set; }
    [Required]
    public DateTimeOffset CreatedAt { get; set; }
    public TypeTeacherTransactionDetails? Type { get; set; }
    public Guid? ReservationId { get; set; }
    public string? ReservationTitle { get; set; }
    /// <summary>Order number of the reservation that generated this entry, if any.</summary>
    public string? ReservationNumber { get; set; }
    /// <summary>
    /// Name of the other party this entry concerns: the student for a payment/refund,
    /// the teacher (wallet owner) for a payout. Lets the UI show the direction at a glance.
    /// </summary>
    public string? CounterpartyName { get; set; }

    public TeacherWalletTransactionDetails() { }

    public TeacherWalletTransactionDetails(TeacherWalletTransaction transaction)
    {
        Id = transaction.Id;
        Amount = transaction.Amount;
        CreatedAt = transaction.CreatedAt;

        if (transaction.Type != null)
            Type = new TypeTeacherTransactionDetails(transaction.Type);

        ReservationId = transaction.ReservationId;
        ReservationTitle = transaction.Reservation?.Title;
        ReservationNumber = transaction.Reservation?.Order?.OrderNumber;

        // Payout = money leaving to the teacher -> show the teacher; otherwise the student.
        UserApp? counterparty = transaction.Type?.Name == TeacherTransactionTypeEnum.Payout.ToString()
            ? transaction.Teacher?.User
            : transaction.Reservation?.Student?.User;
        if (counterparty != null)
            CounterpartyName = $"{counterparty.FirstName} {counterparty.LastName}".Trim();
    }
}

/// <summary>
/// Full DTO for a single wallet transaction, with the related reservation,
/// student and teacher details.
/// </summary>
public class TeacherWalletTransactionDetail
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public decimal Amount { get; set; }
    [Required]
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public TypeTeacherTransactionDetails? Type { get; set; }
    public ReservationDetails? Reservation { get; set; }
    public StudentDetails? Student { get; set; }
    public TeacherDetails? Teacher { get; set; }

    public TeacherWalletTransactionDetail() { }

    public TeacherWalletTransactionDetail(TeacherWalletTransaction transaction)
    {
        Id = transaction.Id;
        Amount = transaction.Amount;
        CreatedAt = transaction.CreatedAt;
        UpdatedAt = transaction.UpdatedAt;

        if (transaction.Type != null)
            Type = new TypeTeacherTransactionDetails(transaction.Type);

        if (transaction.Reservation != null)
            Reservation = new ReservationDetails(transaction.Reservation);

        if (transaction.Reservation?.Student != null)
            Student = new StudentDetails(transaction.Reservation.Student);

        if (transaction.Teacher != null)
            Teacher = new TeacherDetails(transaction.Teacher, includeEmail: true);
    }
}
