# ADR-0005 — Un versement par réservation, pas par lot

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

Verser un professeur en une seule fois, pour l'ensemble de ses crédits arrivés à maturité,
semble économique : moins d'appels, moins de lignes.

Deux problèmes le disqualifient :

1. **La clé d'idempotence d'un lot n'est pas stable.** La composition du lot change entre deux
   cycles — un cours se termine, une réservation passe en litige. Voir
   [ADR-0004](ADR-0004-idempotence-stripe.md).
2. **Un échec bloque tout.** Un solde de plateforme momentanément insuffisant fait échouer le
   lot entier, donc l'intégralité du solde du professeur, au lieu d'une seule réservation.

## Décision

**Un transfert et un enregistrement de versement par réservation.** La clé d'idempotence est
dérivée de la réservation.

Le professeur reçoit tout de même un **virement bancaire groupé** : c'est le prestataire qui
agrège son solde selon son échéancier. La granularité technique ne se voit donc pas sur son
relevé.

## Conséquences

**Ce qu'on gagne**

- Une clé stable, donc une idempotence réelle.
- Un échec isolé ne bloque que sa réservation, reprise au cycle suivant.
- Chaque versement est traçable jusqu'au cours qui l'a produit — ce qui rend le portefeuille
  lisible et la comptabilité vérifiable.
- Si un renvoi de transfert devait un jour être nécessaire, il serait exact, sans reversement
  partiel à calculer.

**Ce que ça coûte**

- Plus d'appels au prestataire et plus de lignes en base. Le volume reste sans commune mesure
  avec le nombre de réservations : ce n'est pas un sujet de performance.
- Un professeur très actif génère beaucoup de lignes de versement ; l'affichage doit être
  paginé et agrégé pour rester lisible.

**Ce qu'on surveille**

Le nombre d'appels par cycle. S'il devenait un problème, la réponse serait d'espacer les
cycles, pas de regrouper les transferts.

## État dans le code

`JobChron.PayoutTeacherBalances` ([`JobChron`](../../RestaurantHandler/Services/JobChron.cs)) groupe par
réservation et envoie un transfert par réservation.
