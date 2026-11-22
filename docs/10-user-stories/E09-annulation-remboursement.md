# E09 — Annulation & remboursement

> Statut : **Stable** · Barème et exemples chiffrés :
> [politique d'annulation](../20-specs-fonctionnelles/05-politique-annulation.md)
>
> **Principe : le coût de l'annulation retombe sur celui qui annule.**

## US-E09-01 — Savoir ce que coûte mon annulation avant d'annuler

**En tant qu'** élève
**je veux** voir exactement ce qui me sera remboursé
**afin de** décider en connaissance de cause.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`CancellationPolicy`](../../RestaurantHandler/Domain/Policies/CancellationPolicy.cs), [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs)
`GET /Slots/student/reservation/{id}/cancellation-preview`, même calcul que
l'exécution.

**Critères d'acceptation**

- Avant confirmation, l'écran affiche le montant remboursé, le montant retenu, le caractère
  tardif ou non de l'annulation et l'heure limite au-delà de laquelle elle le devient.
- La prévisualisation et l'exécution utilisent **le même calcul, au même endroit** : il est
  impossible d'annoncer autre chose que ce qui sera appliqué.
- Si l'annulation n'est pas possible, l'écran l'indique et explique pourquoi.

## US-E09-02 — Annuler une réservation

**En tant qu'** élève
**je veux** annuler une réservation payée
**afin de** récupérer mon argent quand je ne peux plus venir.

**Priorité :** Doit · **Dépend de :** US-E09-01

**État :** Implémentée · **Code :** [`CancellationPolicy`](../../RestaurantHandler/Domain/Policies/CancellationPolicy.cs), [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs)
Barème, refus après le début du cours, transition avec auteur et motif, `Refund` par
réservation. Le refus est levé en `Exception` générique plutôt qu'en `409`.

**Critères d'acceptation**

- Annulation **au moins 48 h avant** le cours : remboursement intégral.
- Annulation **moins de 48 h avant** : remboursement du montant diminué de la commission,
  que la plateforme conserve.
- **Une fois le cours commencé, l'annulation est refusée** : la seule voie est le litige.
  Voir [E11](E11-litiges.md).
- Dans tous les cas, le crédit du professeur est **annulé** : il ne touche rien.
- La réservation passe annulée, le créneau est libéré, et l'historique conserve l'auteur et
  le motif.
- Le remboursement porte sur **une réservation**, jamais sur la commande entière : une
  commande payée est immuable. Voir
  [invariant I10](../20-specs-fonctionnelles/01-regles-metier.md#i10--une-commande-payée-est-immuable).

## US-E09-03 — Annuler une réservation en tant que professeur

**En tant que** professeur
**je veux** annuler un cours que je ne peux pas assurer
**afin de** prévenir l'élève plutôt que de ne pas venir.

**Priorité :** Doit

**État :** Partielle · **Code :** [`CancellationPolicy`](../../RestaurantHandler/Domain/Policies/CancellationPolicy.cs), [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs)
`DELETE /Slots/teacher/remove-reservation` : remboursement intégral et pénalité
conformes. Écart : aucune prévisualisation de l'impact sur le solde du professeur.

**Critères d'acceptation**

- Possible **à tout moment avant la fin du créneau**, sans seuil de délai : un professeur qui
  annule reste responsable.
- L'élève est **intégralement** remboursé.
- Le crédit du professeur est annulé **et** une pénalité de 5 % du montant est inscrite à sa
  charge.
- L'écran affiche l'impact exact sur son solde **avant** confirmation, y compris lorsque cela
  le rend négatif.
- L'élève est notifié immédiatement.

## US-E09-04 — Porter une pénalité en dette

**En tant que** plateforme
**je veux** pouvoir imputer une pénalité à un professeur au solde vide
**afin de** ne pas renoncer à la créance.

**Priorité :** Doit · **Dépend de :** US-E09-03

**État :** Implémentée · **Code :** [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs), [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs)
Écriture `PENALTY` négative, recouvrée par `PayoutTeacherBalances`.

**Critères d'acceptation**

- La pénalité est écrite comme une écriture **négative de type pénalité**, ce qui la rend
  recouvrable ; une écriture de remboursement, elle, n'est jamais une dette.
- Le solde du professeur peut devenir négatif.
- Le recouvrement se fait par compensation sur les versements suivants. Voir US-E08-05.

## US-E09-05 — Exécuter les remboursements en différé

**En tant que** plateforme
**je veux** exécuter les remboursements par une tâche récurrente plutôt qu'en direct
**afin que** l'échec d'un appel externe ne casse pas le parcours de l'utilisateur.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`JobChron`](../../RestaurantHandler/Services/JobChron.cs)
`ProcessPendingRefunds`, toutes les 15 minutes, clé `refund_{reservationId}`, après
`DISPUTE_WINDOW`.

**Critères d'acceptation**

- L'annulation enregistre une ligne de remboursement **en attente** et rend la main
  immédiatement.
- Une tâche récurrente exécute les remboursements en attente et les marque réglés.
- La clé d'idempotence est dérivée de la réservation : un rejeu ne rembourse jamais deux fois.
- Un échec est réessayé au cycle suivant sans intervention.
- L'élève voit l'état de son remboursement sur le détail de sa commande.

## US-E09-06 — Récupérer un crédit déjà versé

**En tant que** plateforme
**je veux** récupérer un montant remboursé après que le professeur a été payé
**afin de** ne pas supporter la perte.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`StripeRefundService`](../../RestaurantHandler/Services/Payments/StripeRefundService.cs)
Webhook `charge.refunded` : écriture `CLAWBACK` si le crédit était déjà versé ; un
`Refund` en attente correspondant est rapproché au lieu d'être dupliqué.

**Critères d'acceptation**

- Le cas est **exceptionnel** par construction : la fenêtre de litige est bornée par le délai
  de versement. Il ne survient que sur un remboursement décidé hors parcours applicatif.
- Une écriture négative de type **récupération** est créée ; elle constitue une dette,
  recouvrée par la même compensation.
- Aucun renvoi d'argent depuis le compte du professeur n'est tenté : la compensation suffit
  tant qu'il continue d'enseigner. Voir
  [ADR-0006](../40-decisions/ADR-0006-dette-prof-compensation.md).
- Un remboursement décidé chez le prestataire est reconnu à sa notification et n'est **pas**
  compté deux fois avec un remboursement émis par la plateforme.

## US-E09-07 — Suivre l'état de remboursement d'une commande

**En tant qu'** élève ou administrateur
**je veux** voir ce qui a été remboursé sur une commande
**afin de** vérifier les montants.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`OrderDTO`](../../RestaurantHandler/Contracts/Order/OrderDTO.cs), [`StripeRefundService`](../../RestaurantHandler/Services/Payments/StripeRefundService.cs)
`OrderDetails.Refunds` et total calculé par somme ; statut de commande dérivé dans
`PaymentService` et `DisputeService` (calcul dupliqué).

**Critères d'acceptation**

- Le détail de la commande liste les remboursements avec montant, motif, date et état.
- Le statut de la commande reflète le rapport entre remboursements et total : partiellement
  ou intégralement remboursée.
- Le total remboursé est **la somme des lignes**, jamais un champ recopié.
