# E06 — Réservation & commande

> Statut : **Stable** · Conventions : [README](README.md) · États :
> [machines à états](../20-specs-fonctionnelles/02-machines-etats.md)

## US-E06-01 — Réserver un créneau

**En tant qu'** élève
**je veux** réserver un créneau pour un cours donné
**afin de** bloquer ce moment avec le professeur.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`BookingService`](../../RestaurantHandler/Services/Reservations/BookingService.cs)
`POST /Slots/student/book`. Montant figé, blocage du créneau, créneau à venir, occupation
partagée avec la recherche, refus de son propre créneau et d'un professeur non actif.

**Critères d'acceptation**

- Je choisis un produit du professeur, et je peux ajouter un intitulé et une description.
- Le montant est calculé à partir du prix du produit et de la durée, puis **figé** sur la
  réservation avec le taux de commission en vigueur.
- La réservation est créée en attente et **bloque le créneau**.
- Le créneau doit être à venir et libre ; sinon la demande est refusée avec un message
  explicite.
- Je ne peux pas réserver un créneau de mon propre compte professeur.
- Le récapitulatif affiche, **avant** confirmation, le montant et le délai de paiement.

## US-E06-02 — Regrouper mes réservations par professeur

**En tant qu'** élève
**je veux** que mes réservations chez un même professeur soient regroupées
**afin de** les payer en une fois.

**Priorité :** Doit · **Dépend de :** US-E06-01

**État :** Implémentée · **Code :** [`ReservationCartService`](../../RestaurantHandler/Services/Reservations/ReservationCartService.cs), [`HelpersFunctions`](../../RestaurantHandler/Utilities/HelpersFunctions.cs)
`GetActiveOrder` par couple élève-professeur ; numéro `BP_yyyyMMdd_NNN` avec rejeu sur
collision.

**Critères d'acceptation**

- Une réservation rejoint la commande en attente **du professeur concerné**, ou en ouvre une
  nouvelle s'il n'y en a pas.
- Réserver chez un autre professeur ouvre une **commande distincte** : je peux avoir
  plusieurs commandes en attente simultanément, une par professeur. Voir
  [ADR-0003](../40-decisions/ADR-0003-commande-mono-prof.md).
- Le total de la commande est la somme des montants de ses réservations.
- Chaque commande porte un numéro lisible et unique.

## US-E06-03 — Consulter mes commandes en attente

**En tant qu'** élève
**je veux** voir mes paniers en cours avec leur échéance
**afin de** ne pas les laisser expirer.

**Priorité :** Doit

**État :** Partielle · **Code :** [`OrderService`](../../RestaurantHandler/Services/Orders/OrderService.cs), [`ReservationQueryService`](../../RestaurantHandler/Services/Reservations/ReservationQueryService.cs)
`GET /Order/student/active-orders` conforme. Écart : `pending-count` compte les
**réservations** en attente, pas les commandes non vides.

**Critères d'acceptation**

- La liste affiche une commande par professeur, avec son détail ligne à ligne et un
  **compte à rebours** jusqu'à l'échéance.
- Le compte à rebours et l'expiration côté serveur se fondent sur **la même** échéance
  stockée : ils ne peuvent pas diverger.
- Un indicateur dans l'en-tête signale le nombre de commandes en attente — servi par
  `GET /reservations/student/pending-count`, qui compte bien les **commandes** non vides, pas
  les réservations.

## US-E06-04 — Retirer une réservation de ma commande

**En tant qu'** élève
**je veux** enlever une réservation que je n'ai pas encore payée
**afin de** corriger mon panier.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs), [`ReservationCartService`](../../RestaurantHandler/Services/Reservations/ReservationCartService.cs)
`DELETE /Slots/student/remove-reservation`, qui traite aussi l'annulation d'une
réservation payée (voir [E09](E09-annulation-remboursement.md)). Les erreurs sont levées en
`Exception` générique.

**Critères d'acceptation**

- Possible tant que la commande n'est pas payée.
- Le créneau est **immédiatement libéré** et le total recalculé.
- Si la commande devient vide, elle reste disponible pour de futures réservations chez le
  même professeur.
- Retirer une réservation d'une commande payée est refusé : c'est une annulation, avec ses
  règles de remboursement. Voir [E09](E09-annulation-remboursement.md).

## US-E06-05 — Libérer automatiquement les commandes impayées

**En tant que** plateforme
**je veux** libérer les créneaux des commandes non payées à temps
**afin qu'** ils redeviennent réservables.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`JobChron`](../../RestaurantHandler/Services/JobChron.cs)
`CleanStalePendingOrders`, toutes les 5 minutes, fondé sur la base : les réservations passent
annulées, avec historique, et quittent la commande.

**Critères d'acceptation**

- Une tâche récurrente traite les commandes en attente dont l'échéance est passée.
- La session de paiement éventuellement ouverte est expirée, les réservations retirées et
  les créneaux libérés.
- Le traitement s'appuie **uniquement sur la base de données** : un redémarrage ou un
  déploiement ne peut pas faire perdre une expiration.
- Le traitement est rejouable sans effet de bord.

## US-E06-06 — Réserver chez un professeur pas encore payable

**En tant qu'** élève
**je veux** pouvoir demander un cours à un professeur non encore vérifié ou non onboardé
**afin de** ne pas être bloqué par l'état administratif du professeur.

**Priorité :** Devrait · **Dépend de :** US-E06-01

**État :** Implémentée · **Code :** [`BookingService`](../../RestaurantHandler/Services/Reservations/BookingService.cs), [`JobChron`](../../RestaurantHandler/Services/JobChron.cs)
`Order.PaymentRequired`, `TEACHER_RESPONSE_WINDOW`. Même écart que ci-dessus : une
réservation sans réponse est supprimée plutôt que refusée.

**Critères d'acceptation**

- À la création de la commande, l'exigence de paiement est **figée** selon l'état du
  professeur : vérifié **et** confirmé côté paiement.
- Si le paiement n'est pas exigé, **aucun argent ne circule** : pas de page de paiement, pas
  de compte à rebours, pas d'encaissement.
- Le professeur dispose de **48 heures** pour accepter ou refuser ; le délai est affiché aux
  deux parties.
- Sans réponse dans ce délai, la réservation est automatiquement refusée et le créneau
  libéré.
- L'interface indique clairement à l'élève que la réservation est soumise à l'accord du
  professeur et qu'aucun paiement n'est demandé.

## US-E06-07 — Accepter ou refuser une réservation

**En tant que** professeur en flux sans paiement
**je veux** accepter ou refuser une demande de réservation
**afin de** garder la main sur mon planning.

**Priorité :** Devrait · **Dépend de :** US-E06-06

**État :** Implémentée · **Code :** [`BookingService`](../../RestaurantHandler/Services/Reservations/BookingService.cs), [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs)
`POST /Slots/teacher/confirm-reservation` (`UpdateReservationStatusAsync`), refusé sur
une commande payante.

**Critères d'acceptation**

- L'action n'est proposée que sur les réservations en attente relevant du flux sans
  paiement.
- L'acceptation confirme la réservation ; le refus libère le créneau.
- L'élève est notifié dans les deux cas.
- Sur une réservation payée, ces actions ne sont pas proposées : le paiement vaut
  confirmation.

## US-E06-08 — Consulter mes réservations

**En tant qu'** élève, professeur ou administrateur
**je veux** parcourir mes réservations avec filtres et tri
**afin de** retrouver rapidement celle qui m'intéresse.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`ReservationQueryService`](../../RestaurantHandler/Services/Reservations/ReservationQueryService.cs), [`TableStateUtilities`](../../RestaurantHandler/Utilities/TableStateUtilities.cs)
`POST /Slots/{student,teacher,admin}/reservations`, filtrés par rôle.

**Critères d'acceptation**

- La liste est paginée, triable et filtrable côté serveur : par statut, par date de début,
  par date de fin, et par recherche libre.
- Les colonnes s'adaptent au rôle : l'élève voit le professeur, le professeur voit l'élève,
  l'administrateur voit les deux.
- Chaque rôle ne voit **que** ses propres réservations ; l'administrateur les voit toutes.
- L'état de la liste (page, tri, filtres) est conservé d'une visite à l'autre.

## US-E06-09 — Consulter le détail d'une réservation

**En tant que** partie prenante d'une réservation
**je veux** en voir le détail complet
**afin de** savoir où elle en est et ce que je peux faire.

**Priorité :** Doit

**État :** Partielle · **Code :** [`ReservationQueryService`](../../RestaurantHandler/Services/Reservations/ReservationQueryService.cs)
**Écart de sécurité :** `GET /Slots/reservation/{id}` renvoie le détail à **tout
utilisateur connecté**, sans contrôle de participation ; le code de présence n'est masqué que
pour le rôle professeur, donc visible d'un autre élève. Historique des statuts conforme.

**Critères d'acceptation**

- Le détail montre : créneau, produit, montant, statut courant, **historique des
  changements de statut** avec auteur et motif, et l'autre partie.
- Les actions proposées dépendent du statut et du rôle : annuler, valider par code, ouvrir
  un litige, déposer un avis, payer.
- L'accès est refusé à qui n'est pas partie prenante, sauf administrateur.
