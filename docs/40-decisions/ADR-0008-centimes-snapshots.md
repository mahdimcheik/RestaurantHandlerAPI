# ADR-0008 — Montants en centimes et snapshots dans les commandes

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

Les erreurs d'arrondi sur des flottants et la modification rétroactive d'une commande quand la
carte change sont les deux causes classiques d'écarts de caisse et de litiges.

## Décision

- Tous les montants sont des **entiers en centimes** (`*Cents`) ; la TVA est un `decimal(5,2)`.
- Les prix de la carte sont **TTC** ; HT et TVA sont déduits par taux sur le total.
- À la validation, chaque ligne et chaque personnalisation **copient** nom/libellé, prix et
  taux de TVA ; toute lecture ultérieure utilise ces copies.
- Le total est **toujours recalculé par le serveur**.

## Conséquences

- Une modification de prix ou une archive ne change jamais une commande passée.
- Le catalogue n'est jamais supprimé, seulement archivé (les commandes y pointent).
- Prépare la caisse certifiée V2 (données de vente immuables).
