# ADR-0013 — Commande en ligne payée par carte uniquement, sans annulation par le client

**Statut :** Accepté
**Date :** 2026-11-26

## Contexte

La V1 prévoyait trois façons de commander et de payer :
- en ligne avec paiement par carte (SumUp) ;
- en ligne avec paiement au retrait, après validation d'un Manager ([ADR-0010](ADR-0010-paiement-retrait-validation-manager.md)) ;
- saisie au comptoir par le personnel, avec encaissement sur place déclaré dans l'application
  ([ADR-0006](ADR-0006-caisse-certifiee-tierce-v1.md)).

Le client pouvait aussi annuler lui-même sa commande avant la préparation.

Le paiement au retrait a un coût : file « À valider » à surveiller pendant le service, risque
de non-venue, règles de blocage. Il en va de même pour le comptoir et les encaissements
déclarés : double saisie avec la caisse tierce, paiement mixte, « reste à encaisser ».
L'annulation en ligne laisse en outre un client annuler une commande déjà lancée dans
l'organisation de la cuisine.

## Décision

- Une commande se passe **uniquement en ligne**, par un client connecté
  ([ADR-0009](ADR-0009-compte-client-obligatoire.md)). Elle se paie **uniquement par carte
  bancaire, en ligne, via SumUp** ([ADR-0007](ADR-0007-sumup-psp.md)).
- **Plus de paiement au retrait.** Le statut `AValider`, la validation Manager, le refus
  automatique, le compteur de non-venues et son blocage sont supprimés. Le statut `Refusée`,
  qui n'existait que pour eux, disparaît aussi.
- **Plus de saisie au comptoir ni d'encaissement sur place dans l'application.** Les ventes
  au comptoir du restaurant, s'il en fait, passent par sa propre caisse, hors de
  l'application.
- **Le client n'annule plus lui-même**, même avant le paiement. Sa page de suivi affiche :
  « Pour annuler, appelez le restaurant par téléphone ou demandez directement sur place »,
  avec le numéro du restaurant s'il est renseigné. Une commande non payée expire seule, au
  bout du délai de paiement.
- Seul un **Manager** annule, avec un motif. Un paiement en ligne est alors remboursé
  automatiquement.
- Une commande payée mais jamais retirée peut toujours être déclarée **non venue**, sans
  compteur ni sanction.

## Conséquences

- Remplace [ADR-0010](ADR-0010-paiement-retrait-validation-manager.md).
- Remplace [ADR-0006](ADR-0006-caisse-certifiee-tierce-v1.md) : l'application n'enregistre
  plus aucun paiement sur place. Il n'y a donc plus de double saisie ni de « reste à
  encaisser ». L'application n'est toujours pas une caisse, et ses statistiques restent des
  indicateurs de pilotage. Le traitement fiscal des ventes en ligne reste à valider avec
  l'expert-comptable (question ouverte 2).
- Machine à états simplifiée :
  - les commandes partent de `EnAttentePaiement` et n'entrent en cuisine (`Confirmée`)
    qu'après la confirmation SumUp ;
  - `Retirée` ne demande plus d'encaissement ;
  - il n'y a plus d'acteur « client » dans les transitions.
- API retirée :
  - `POST /orders/{id}/cancel` et `POST /orders/{id}/pay-on-site` ;
  - `POST /manage/orders/counter`, `/accept`, `/refuse` et `/payments` ;
  - `POST /manage/customers/{id}/no-shows/reset` ;
  - l'événement temps réel `OrderAwaitingApproval`.
- Données : migration `RemoveOnSiteAndCounter`.
  - Elle supprime les commandes d'essai au comptoir ou à payer au retrait.
  - Elle passe en `Annulée` les éventuelles `AValider` et `Refusée`.
  - Elle retire les colonnes `Channel`, `PaymentChoice`, `CustomerFirstName`,
    `DeclaredByUserId`, `NoShowCount`, `ApprovalTimeoutMinutes` et `NoShowThreshold`, ainsi
    que la table `NoShowResets`.
- Pendant une panne SumUp, aucune commande ne peut être prise en ligne : la validation est
  refusée avec un message clair.
- Les CGV doivent décrire la procédure d'annulation (téléphone ou sur place) et le
  remboursement par le restaurant.
