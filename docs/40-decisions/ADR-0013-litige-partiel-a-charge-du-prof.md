# ADR-0013 — Remboursement partiel d'un litige à la charge du professeur

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-17

## Contexte

Un litige se résout par un montant R remboursé à l'élève, entre 0 et le montant de la
réservation. Reste à savoir qui porte ce remboursement quand il est partiel
([Q11](../50-backlog/02-questions-ouvertes.md#q11--remboursement-partiel-dun-litige--qui-porte-la-commission--tranchée)).

Les règles métier retiraient au professeur `min(R, part professeur)`. Le code retirait
`R × (1 − commission)` : la plateforme rendait sa commission au prorata. Deux façons
d'exprimer la même décision donnaient alors deux montants — un remboursement partiel de
29,99 € sur 30 € ne coûtait pas au professeur la même chose qu'un remboursement total.

## Décision

**Le remboursement d'un litige est à la charge du professeur, dans la limite de sa part.** Son
crédit est annulé de `min(R, part professeur)` ; la plateforme conserve sa commission.

Le calcul vit dans une seule politique pure,
[`DisputeResolutionPolicy`](../../RestaurantHandler/Domain/Policies/DisputeResolutionPolicy.cs), qui sert
toutes les voies de résolution : arbitrage aujourd'hui, acceptation par le professeur et
résolution automatique demain.

## Conséquences

**Ce qu'on gagne**

- Une issue partielle portant sur la totalité du montant donne exactement le résultat d'une
  issue « élève ».
- Le plafond garantit qu'une résolution ne crée jamais de dette (I14).
- La plateforme a le même revenu qu'un litige soit tranché en totalité ou en partie : le
  litige n'est pas une source de perte pour elle.

**Ce que ça coûte**

- Un petit remboursement est plus cher pour le professeur qu'avant : 10 € remboursés lui
  retirent 10 €, contre 9,50 € auparavant.
- Les litiges partiels tranchés avant le 2026-09-17 ont été calculés avec l'ancienne formule ;
  ils ne sont pas repris.

**Ce qu'on surveille**

La part des litiges tranchés en partiel. Si elle grandit, l'arbitrage sert peut-être de geste
commercial aux dépens du professeur.

## État dans le code

Appliquée depuis le lot 3 : [`DisputeService.CloseDisputeAsync`](../../RestaurantHandler/Services/DisputeService.cs)
délègue à la politique. Tests : `DisputeResolutionPolicyTests` (unitaires) et
`DisputeResolutionTests.D15_*` (intégration).
