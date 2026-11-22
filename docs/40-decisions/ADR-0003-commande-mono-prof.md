# ADR-0003 — Une commande porte sur un seul professeur

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

La première version du panier autorisait plusieurs professeurs dans une même commande. C'est
le comportement naturel d'un panier de commerce en ligne — mais ici l'argent se répartit
entre plusieurs vendeurs.

Les conséquences apparaissaient partout :

- un encaissement unique devait être **réparti** entre plusieurs professeurs, donc la relation
  encaissement ↔ commande n'était plus 1:1 ;
- un remboursement partiel exigeait de retrouver quelle part de l'encaissement revenait à
  quel professeur ;
- l'annulation d'une réservation touchait une commande dont les autres lignes concernaient
  d'autres vendeurs ;
- la facture — émise **au nom du professeur** — n'avait pas d'émetteur unique.

## Décision

Une commande porte sur **un seul professeur**. L'identité du professeur, qui vient du créneau,
est dénormalisée sur la commande pour garantir l'invariant et permettre de retrouver le
panier en attente d'un couple élève-professeur en une lecture.

Un élève qui réserve chez plusieurs professeurs a **plusieurs commandes en attente
simultanées**, une par professeur, payées séparément.

## Conséquences

**Ce qu'on gagne**

- L'encaissement reste **1:1** avec la commande : aucune répartition à calculer.
- Chaque paiement est attribuable à un seul vendeur sans jointure.
- La facture a un émetteur unique.
- Un remboursement ne touche qu'un seul professeur.
- La clé d'idempotence du paiement se dérive naturellement de la commande.

**Ce que ça coûte**

- **L'élève paie en plusieurs fois** s'il réserve chez plusieurs professeurs. C'est le vrai
  coût, et il est assumé : en pratique un élève réserve chez un professeur à la fois, et
  l'alternative — une répartition à calculer sur chaque mouvement d'argent — coûterait bien
  plus cher.
- L'interface doit présenter clairement **plusieurs paniers actifs**, ce qui n'est pas
  l'habitude des utilisateurs.
- Chaque commande a sa propre échéance de 30 minutes ; l'élève peut en perdre une pendant
  qu'il paie l'autre.

**Ce qu'on surveille**

Le taux d'abandon quand plusieurs commandes sont actives. S'il est élevé, la réponse n'est pas
de revenir au panier multi-professeurs mais d'améliorer la présentation.

## État dans le code

`Order.TeacherId` ([`Order.cs`](../../RestaurantHandler/Domain/Entities/Order/Order.cs)) ; la commande en attente d'un
couple élève-professeur est retrouvée ou créée par `GetActiveOrder` dans
[`ReservationCartService`](../../RestaurantHandler/Services/Reservations/ReservationCartService.cs).
