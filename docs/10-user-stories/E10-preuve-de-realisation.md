# E10 — Preuve de réalisation

> Statut : **Stable** — le modèle de confiance de la plateforme.

## Le problème

Comment savoir qu'un cours en présentiel a eu lieu avant de payer le professeur ?

**On ne peut pas.** Il n'existe pas de preuve fiable d'un cours en présentiel. Et toute
action de confirmation détenue par une seule partie devient un **droit de veto** pour cette
partie :

- si l'élève doit confirmer pour que le professeur soit payé, l'élève malhonnête refuse et
  obtient un cours gratuit ;
- si le professeur doit fournir un code détenu par l'élève, le veto revient à l'élève.

## La réponse

**Le défaut est « le professeur est payé ».** Le versement est déclenché par le temps, pas
par une confirmation. Pour qu'il n'ait **pas** lieu, l'élève doit poser un acte explicite,
nommé, journalisé sous sa propre identité, qui n'entraîne pas une perte automatique pour le
professeur mais ouvre un arbitrage humain.

L'élève qui se tait ne gagne rien. L'élève qui veut un cours gratuit doit porter une
accusation formelle et fausse, traçable et coûteuse en réputation.

---

## US-E10-01 — Confirmer un cours par code de présence

**En tant que** professeur
**je veux** saisir le code de présence de mon élève à la fin du cours
**afin de** confirmer la réalisation et accélérer mon paiement.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`PresenceService`](../../RestaurantHandler/Services/Reservations/PresenceService.cs)
`POST /Slots/validate-reservation` : code généré au paiement, saisie limitée au flux payant et
à la fenêtre « début du créneau → échéance de contestation », verrouillage (5 essais, 15 min),
passage à `DELIVERED` historisé.

**Critères d'acceptation**

- Un code est généré à la confirmation du paiement et visible par l'**élève** sur sa
  réservation.
- Le professeur le saisit ; en cas de succès la réservation passe **livrée**.
- Le code n'est **jamais une condition de paiement** : ne pas le saisir ne bloque rien.
- L'action n'est possible qu'entre le début du créneau et l'échéance de contestation.
- Après un nombre limité d'essais erronés, la saisie est **verrouillée temporairement** et la
  date de déverrouillage est renvoyée.
- L'horodatage de la saisie est conservé : c'est un élément d'appréciation en cas de litige.

## US-E10-02 — Rendre un crédit versable par le temps

**En tant que** plateforme
**je veux** rendre une réservation versable sans aucune action humaine
**afin que** l'inaction ne prive jamais le professeur de son argent.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`JobChron`](../../RestaurantHandler/Services/JobChron.cs)
`MarkReservationsPayable` pose `PAYABLE` une fois le créneau terminé depuis `DISPUTE_WINDOW`
(flux payant, hors litige) ; le versement ne sélectionne que cet état.

**Critères d'acceptation**

- Une réservation devient versable lorsque le créneau est terminé **et** que la fenêtre de
  contestation est écoulée **et** qu'aucun litige n'est ouvert.
- Le passage par l'état livré n'est **pas** requis.
- Une réservation annulée, refusée ou en litige n'est jamais versable.
- La fenêtre de contestation est bornée par le délai de versement, de sorte qu'un litige ne
  puisse pas survenir après un versement. Voir
  [règles métier](../20-specs-fonctionnelles/01-regles-metier.md#pourquoi-ces-valeurs-et-pas-dautres).

## US-E10-03 — Consulter mon code de présence

**En tant qu'** élève
**je veux** voir le code de ma réservation
**afin de** le communiquer au professeur à la fin du cours.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`ReservationQueryService`](../../RestaurantHandler/Services/Reservations/ReservationQueryService.cs)
Le code n'est renvoyé que par le détail de la réservation, à l'élève propriétaire et à partir
du début du créneau ; les listes et le calendrier ne l'exposent pas. Le texte explicatif relève
du front.

**Critères d'acceptation**

- Le code n'est visible que par l'élève concerné, sur sa réservation, à partir du début du
  créneau.
- L'écran explique en une phrase à quoi il sert, et surtout qu'il **n'est pas obligatoire**
  et ne conditionne aucun remboursement.

## US-E10-04 — Rassembler des éléments d'appréciation

**En tant qu'** administrateur devant arbitrer
**je veux** disposer des signaux disponibles autour d'une réservation
**afin de** trancher avec un minimum de matière.

**Priorité :** Pourrait

**État :** Absente
Aucun dossier d'appréciation n'est assemblé ; l'administrateur consulte séparément
la réservation, son historique et la conversation.

**Critères d'acceptation**

- Le dossier de litige rassemble : la conversation de la réservation, l'horodatage de saisie
  du code s'il a été saisi, l'historique complet des changements de statut, et l'historique
  de litiges des deux parties.
- Aucun de ces éléments n'est présenté comme une preuve ; ils servent le jugement.
- Aucun élément n'est exigé de l'élève pour ouvrir un litige : il n'a rien à prouver.
