# ADR-0004 — Clés d'idempotence dérivées de données stables

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

Tout appel d'argent vers le prestataire peut réussir **sans que la validation locale
aboutisse** : coupure réseau, redémarrage, échec de transaction. Au rejeu, il faut que le
prestataire reconnaisse l'opération et ne la rejoue pas.

Deux contraintes du prestataire déterminent la solution :

1. une clé d'idempotence n'est dédupliquée que **24 heures** ;
2. le prestataire **refuse** la même clé accompagnée de paramètres différents.

La seconde est la plus piégeuse. Elle interdit deux pratiques tentantes :

- une clé nouvellement générée à chaque tentative — elle ne protège de rien ;
- une clé dérivée d'un **lot**. Si le transfert part, que la validation échoue, et qu'entre
  temps une ligne entre ou sort du lot — un autre créneau se termine, une réservation passe
  en litige —, le rejeu envoie une clé différente et le prestataire **verse deux fois**.

## Décision

Toute clé d'idempotence est dérivée d'une **entité métier stable**, et le corps de la requête
est **reproductible à l'identique** lors d'un rejeu.

| Opération | Clé |
|---|---|
| Ouverture de paiement | commande + numéro de tentative |
| Versement | **réservation** |
| Remboursement | **réservation** |

Corollaire imposé : tout paramètre qui entrerait dans la requête est **figé en base avant**
l'appel — échéance de paiement, montant net d'un versement, dette appliquée. Un paramètre
recalculé au rejeu produirait une requête différente, donc un refus.

Ordre imposé : **appeler le prestataire d'abord, valider en base ensuite.** L'inverse produit
une écriture comptable sans argent derrière.

## Conséquences

**Ce qu'on gagne**

- Un rejeu ne produit jamais de double mouvement d'argent.
- Un échec local après un appel réussi se répare tout seul au cycle suivant.
- Les gardes sont à deux niveaux : le rattachement en base empêche de **refaire** le travail,
  la clé protège le cas « l'appel a réussi mais pas l'enregistrement ».

**Ce que ça coûte**

- Le numéro de tentative ne peut **jamais** être remis à zéro, même en vidant un panier.
- Un versement figé en attente doit être **rejoué tel quel**, jamais recalculé — même si les
  données ont changé entre temps.
- L'échéance de paiement est arrondie à la minute supérieure pour être reproductible, ce qui
  n'est pas intuitif à la lecture du code et doit rester commenté.
- La fenêtre de déduplication de 24 heures signifie qu'un rejeu très tardif n'est plus
  protégé côté prestataire : seul le rattachement en base couvre alors.

**Ce qu'on surveille**

Toute modification du corps d'une requête d'argent. C'est le seul moyen de casser cette
propriété sans s'en apercevoir.

## État dans le code

Clés `checkout_{orderId}_{attempt}` et `coupon_{orderId}_{attempt}`
([`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs)), `payout_{reservationId}` et
`refund_{reservationId}` ([`JobChron`](../../RestaurantHandler/Services/JobChron.cs)). `Order.CheckoutAttempt`
et `Order.PaymentDeadline` sont figés avant l'appel ; le `TeacherPayout` est figé en
`PENDING` avant le transfert.
