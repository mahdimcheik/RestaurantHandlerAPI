using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace RestaurantHandler.Services;

public class StripeConnectService(MainContext context, ILogger<StripeConnectService> logger)
{
    public record CreateAccountRequest(
        [property: Obsolete("Ignoré : le professeur est celui de la session.")] string? Email = null
    );

    public record TransferRequest(
        string DestinationAccountId,
        long Amount,
        string Currency = "eur",
        string? Description = null,
        // When set, Stripe deduplicates retries of the same transfer for 24h. Must be
        // derived from stable data (not a fresh GUID) so a retry reuses the same key.
        string? IdempotencyKey = null
    );

    /// <summary>
    /// Creates a Stripe Connect (Express) account and returns an onboarding URL the user
    /// should be redirected to in order to fill in their details (bank, identity, etc.).
    /// Persist the returned accountId against your user; it is what you transfer funds to later.
    /// </summary>
    public async Task<Response<object>> CreateConnectedAccountAsync(CallerIdentity? principal)
    {
        // Le professeur est celui de la session, jamais celui désigné par la requête : un
        // tiers ne doit pas pouvoir saisir ses coordonnées bancaires à sa place.
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await context
            .Teachers.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.ArchivedAt == null);

        if (teacher is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
                Data = null,
            };
        }

        if (teacher.IsStripeConfirmed && !string.IsNullOrEmpty(teacher.StripeAccount))
        {
            Console.WriteLine("Profil confirmé");

            return new Response<object>
            {
                Status = 404,
                Message = "Profil confirmé",
                Data = null,
            };
        }

        // 1. Reuse the existing connected account if the teacher already started onboarding,
        //    otherwise create a fresh "express" account (Stripe-hosted onboarding flow).
        var accountService = new AccountService();
        string accountId;

        if (!string.IsNullOrEmpty(teacher.StripeAccount))
        {
            accountId = teacher.StripeAccount;
        }
        else
        {
            var account = await accountService.CreateAsync(
                new AccountCreateOptions
                {
                    Type = "express",
                    Email = user.Email,
                    Capabilities = new AccountCapabilitiesOptions
                    {
                        Transfers = new AccountCapabilitiesTransfersOptions
                        {
                            Requested = true,
                        },
                    },
                }
            );

            accountId = account.Id;

            // Persist the acct_... so the webhook can match this account back to the teacher.
            teacher.StripeAccount = accountId;
            await context.SaveChangesAsync();
        }

        // 2. Create a one-time onboarding link for that account.
        var linkService = new AccountLinkService();
        var link = await linkService.CreateAsync(
            new AccountLinkCreateOptions
            {
                Account = accountId,
                // User comes back here if the link expired or they need to restart.
                RefreshUrl =
                    $"{EnvironmentVariables.API_FRONT_URL}/payment/connect/refresh?account_id={accountId}",
                // User lands here when onboarding finishes (status is confirmed via the webhook, not this redirect).
                ReturnUrl =
                    $"{EnvironmentVariables.API_FRONT_URL}/payment/connect/return?account_id={accountId}",
                Type = "account_onboarding",
            }
        );

        return new Response<object>
        {
            Status = 200,
            Message = "Compte connecté créé avec succès",
            Data = new { accountId, onboardingUrl = link.Url },
        };
    }

    /// <summary>
    /// Transfers funds from the platform balance to a connected account. Money only —
    /// the caller owns the DB side (create the <see cref="TeacherPayout"/>, stamp the
    /// settled wallet entries, write the debit line) so the whole payout commits
    /// atomically in one SaveChanges after the money is confirmed sent. Idempotent via
    /// <see cref="TransferRequest.IdempotencyKey"/>. Returns the Stripe transfer id (tr_...) on
    /// success — the caller persists it for reconciliation and for a later reversal — or a
    /// non-200 Response so the caller aborts before recording a phantom payout.
    /// </summary>
    public async Task<Response<string>> CreateTransferAsync(TransferRequest request)
    {
        try
        {
            var transferService = new TransferService();
            var transfer = await transferService.CreateAsync(
                new TransferCreateOptions
                {
                    Amount = request.Amount, // smallest currency unit, e.g. cents
                    Currency = request.Currency,
                    Destination = request.DestinationAccountId,
                    Description = request.Description,
                },
                request.IdempotencyKey is null
                    ? null
                    : new RequestOptions { IdempotencyKey = request.IdempotencyKey }
            );

            return new Response<string>
            {
                Status = 200,
                Message = "Transfert effectué avec succès",
                Data = transfer.Id,
            };
        }
        catch (Exception ex)
        {
            return new Response<string>
            {
                Status = 500,
                Message = $"Erreur lors du transfert: {ex.Message}",
                Data = null,
            };
        }
    }

    /// <summary>
    /// <c>account.updated</c> : le compte Connect d'un professeur est prêt à recevoir des transferts
    /// quand ses informations sont soumises et que les versements sont ouverts.
    /// </summary>
    public async Task HandleAccountUpdatedAsync(Event stripeEvent, string evt)
    {
        logger.LogInformation("[Webhook {Event}] Step 4 - handling AccountUpdated.", evt);
        // Fired as the connected account progresses through onboarding.
        // This is how you know an account is ready to receive transfers.
        var account = stripeEvent.Data.Object as Account;
        logger.LogInformation(
            "[Webhook {Event}] Step 4.1 - account details: accountId={AccountId}, detailsSubmitted={DetailsSubmitted}, chargesEnabled={ChargesEnabled}, payoutsEnabled={PayoutsEnabled}.",
            evt,
            account?.Id,
            account?.DetailsSubmitted,
            account?.ChargesEnabled,
            account?.PayoutsEnabled
        );

        if (account is not null)
        {
            logger.LogInformation(
                "[Webhook {Event}] Step 4.2 - looking up teacher for connected account {AccountId}.",
                evt,
                account.Id
            );
            // Match the connected account back to the teacher that started onboarding.
            var teacher = await context.Teachers.FirstOrDefaultAsync(t =>
                t.StripeAccount == account.Id && t.ArchivedAt == null
            );

            if (teacher is null)
            {
                logger.LogWarning(
                    "[Webhook {Event}] Step 4.3 - no teacher found for connected account {AccountId}.",
                    evt,
                    account.Id
                );
            }
            else
            {
                // Onboarding is complete once the user submitted their details and the
                // account can actually receive payouts.
                var isReady = account.DetailsSubmitted && account.PayoutsEnabled;
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.3 - teacher {TeacherId} found for account {AccountId}: isReady={IsReady}, alreadyConfirmed={AlreadyConfirmed}.",
                    evt,
                    teacher.Id,
                    account.Id,
                    isReady,
                    teacher.IsStripeConfirmed
                );

                if (isReady && !teacher.IsStripeConfirmed)
                {
                    teacher.IsStripeConfirmed = true;
                    await context.SaveChangesAsync();
                    logger.LogInformation(
                        "[Webhook {Event}] Step 4.4 - teacher {TeacherId} confirmed via connected account {AccountId}.",
                        evt,
                        teacher.Id,
                        account.Id
                    );
                }
                else
                {
                    logger.LogInformation(
                        "[Webhook {Event}] Step 4.4 - teacher {TeacherId} not confirmed yet: isReady={IsReady}, alreadyConfirmed={AlreadyConfirmed}.",
                        evt,
                        teacher.Id,
                        isReady,
                        teacher.IsStripeConfirmed
                    );
                }
            }
        }
    }
}
