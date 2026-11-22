# Machine à états de la commande

## Proposition V1

Le statut décrit **l'avancement de la commande** ; le fait d'être **payée** est une
information distincte (R9 des [règles de commande](01-regles-commande.md)), parce qu'une
commande « payer au retrait » est préparée **avant** d'être payée.

```
                  ┌──────────────── (paiement en ligne réussi) ────────────────┐
                  │                                                            ▼
 [en ligne, paie en ligne] ──► EnAttentePaiement ──(expiration / annulation)──► Annulée
                                                                               ▲
 [en ligne, paie au retrait] ─► AValider ──(Manager accepte)──► Confirmée ─────┤
                                   │                                │          │
                                   └─(refus / délai)─► Refusée       │          │
 [comptoir] ───────────────────────────────────────────────────────► Confirmée  │
                                                                    │          │
                                                       EnPréparation ◄┘          │
                                                            │ (annulation Manager)
                                                          Prête
                                                     ┌──────┴──────┐
                                                  Retirée        NonVenu
```

| Statut | Signification | Visible au KDS |
|---|---|---|
| `EnAttentePaiement` (`PendingPayment`) | Commande en ligne en attente de la confirmation SumUp | non |
| `AValider` (`AwaitingApproval`) | Commande à payer au retrait en attente du Manager | non |
| `Confirmée` (`Confirmed`) | Acceptée, à préparer | oui |
| `EnPréparation` (`InPreparation`) | En cours en cuisine | oui |
| `Prête` (`Ready`) | Prête à remettre | non (back-office « à remettre ») |
| `Retirée` (`Completed`) | Remise au client, payée | non |
| `NonVenu` (`NoShow`) | Prête mais jamais retirée | non |
| `Refusée` (`Refused`) | Refusée par le Manager ou faute de validation à temps | non |
| `Annulée` (`Cancelled`) | Annulée par le client, le Manager ou l'expiration du paiement | non |

## Transitions

| De | Vers | Déclencheur | Conditions / effets |
|---|---|---|---|
| — | `EnAttentePaiement` | Client valide, paiement en ligne | crée le checkout SumUp |
| — | `AValider` | Client valide, paiement au retrait | client non bloqué, une seule en cours |
| — | `Confirmée` | Manager valide une commande comptoir | — |
| `EnAttentePaiement` | `Confirmée` | Paiement confirmé (webhook + relecture SumUp) | montant = total ; `PaidAt` renseigné |
| `EnAttentePaiement` | `Annulée` | Client, ou expiration (Hangfire) | — |
| `AValider` | `Confirmée` | Manager accepte | — |
| `AValider` | `Refusée` | Manager refuse (motif) ou délai dépassé (Hangfire) | — |
| `Confirmée` | `EnPréparation` | Cuisine | — |
| `Confirmée` | `Annulée` | Client ou Manager | remboursement si payée en ligne |
| `EnPréparation` | `Prête` | Cuisine | `ReadyAt` renseigné |
| `EnPréparation` / `Prête` | `Annulée` | Manager uniquement (motif) | remboursement si payée en ligne |
| `Prête` | `Retirée` | Manager | **commande payée** ; `CompletedAt` |
| `Prête` | `NonVenu` | Manager, après heure de retrait + délai de grâce | compteur de non-venues du client +1 |
| `EnPréparation` | `Confirmée` | Cuisine, annulation de geste (≤ 10 s) | idem `Prête` → `EnPréparation` |

Règles :
- Toute transition non listée est **refusée** (409).
- Chaque transition écrit une ligne `OrderStatusHistory` (statut, auteur, horodatage, motif).
- Chaque transition est diffusée en temps réel au client propriétaire, au back-office et au KDS.
- Les états finaux (`Retirée`, `NonVenu`, `Refusée`, `Annulée`) sont **immuables**.

## Écart avec le modèle actuel

`OrderStatus` contient aujourd'hui `PendingPayment, Paid, InPreparation, Ready, Completed,
Cancelled`. À faire en J2 :
- remplacer `Paid` par `Confirmed` (le paiement devient l'information `PaidAt` + encaissements) ;
- ajouter `AwaitingApproval`, `Refused`, `NoShow` ;
- ajouter le motif (`Reason`) à `OrderStatusHistory`.

Voir [modèle de données](../30-technique/02-modele-donnees.md#écarts-à-combler).
