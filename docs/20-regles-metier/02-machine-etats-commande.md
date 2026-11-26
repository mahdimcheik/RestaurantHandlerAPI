# Machine à états de la commande

## Machine V1 (validée le 2026-11-22, question 4 ; simplifiée le 2026-11-26, ADR-0013)

Toute commande est passée en ligne et payée par carte via SumUp
([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)). Le statut décrit
**l'avancement** de la commande. Le fait d'être **payée** reste une information distincte
(R9 des [règles de commande](01-regles-commande.md)) : un paiement peut arriver en anomalie, ou
après une annulation. En pratique, une commande n'entre en cuisine qu'une fois payée.

```
 [en ligne] ──► EnAttentePaiement ──(paiement confirmé)──► Confirmée ──► EnPréparation ──► Prête
                      │                                       │              │            │
                      └─(expiration / Manager)─► Annulée ◄────┴──(Manager)───┴────────────┤
                                                                                    ┌─────┴─────┐
                                                                                 Retirée    NonVenu
```

| Statut | Signification | Visible au KDS |
|---|---|---|
| `EnAttentePaiement` (`PendingPayment`) | Commande en ligne en attente de la confirmation SumUp | non |
| `Confirmée` (`Confirmed`) | Payée, à préparer | oui |
| `EnPréparation` (`InPreparation`) | En cours en cuisine | oui |
| `Prête` (`Ready`) | Prête à remettre | colonne « Prêtes » |
| `Retirée` (`Completed`) | Remise au client | non |
| `NonVenu` (`NoShow`) | Prête mais jamais retirée | non |
| `Annulée` (`Cancelled`) | Annulée par le Manager (à la demande du client ou sur imprévu) ou par l'expiration du paiement | non |

Les statuts `AValider` (`AwaitingApproval`) et `Refusée` (`Refused`) du paiement au retrait
ont été supprimés le 2026-11-26. La migration `RemoveOnSiteAndCounter` a passé les commandes
restantes à ces statuts en `Annulée`.

## Transitions

| De | Vers | Déclencheur | Conditions / effets |
|---|---|---|---|
| — | `EnAttentePaiement` | Client valide son panier | paiement en ligne disponible ; crée le checkout SumUp |
| `EnAttentePaiement` | `Confirmée` | Paiement confirmé (webhook + relecture SumUp) | montant = total ; `PaidAt` renseigné |
| `EnAttentePaiement` | `Annulée` | Expiration (Hangfire), ou Manager (motif) | checkout SumUp désactivé ; paiement arrivé entre-temps remboursé |
| `Confirmée` | `EnPréparation` | Cuisine | — |
| `Confirmée` | `Annulée` | Manager (motif) | remboursement du paiement en ligne |
| `EnPréparation` | `Prête` | Cuisine | `ReadyAt` renseigné |
| `EnPréparation` / `Prête` | `Annulée` | Manager (motif) | remboursement du paiement en ligne |
| `Prête` | `Retirée` | Manager | commande payée (toujours le cas) ; `CompletedAt` |
| `Prête` | `NonVenu` | Manager, après heure de retrait + délai de grâce | pas de remboursement automatique ; ni compteur ni sanction |
| `EnPréparation` | `Confirmée` | Cuisine, annulation de geste (≤ 10 s) | idem `Prête` → `EnPréparation` |

Règles :
- Toute transition non listée est **refusée** (409).
- **Le client n'a aucune transition** : pour annuler, il appelle le restaurant ou le demande sur
  place ; c'est le Manager qui annule.
- Chaque transition écrit une ligne `OrderStatusHistory` (statut, auteur, horodatage, motif).
- Chaque transition est diffusée en temps réel au client propriétaire, au back-office et au KDS.
- Les états finaux (`Retirée`, `NonVenu`, `Annulée`) sont **immuables**.

## Mise en œuvre

La règle pure `Domain/Policies/OrderTransitionPolicy.cs` (acteurs `Manager` et `System`) est
testée transition par transition et appliquée par `OrderWorkflowService`. L'expiration d'une
commande non payée est faite par la tâche Hangfire de réconciliation SumUp, après
`OnlinePaymentExpiryMinutes`. Deux gestes simultanés sur une même commande : le second reçoit
409 (jeton de concurrence `xmin`).
