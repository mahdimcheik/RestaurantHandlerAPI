# ADR-0009 — Compte client obligatoire pour commander en ligne

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

Commander en invité maximise la conversion mais complique le suivi, le paiement au retrait
(pas d'identité pour limiter les abus) et l'historique. Le socle fournit déjà inscription,
activation, connexion Google et profil.

## Décision

Toute commande en ligne est rattachée à un **compte `Client`** ; la carte et le panier restent
accessibles sans compte, la connexion est demandée à la validation. La connexion **Google**
est conservée pour réduire la friction. Les commandes comptoir sont sans compte (client de
passage).

## Conséquences

- Suivi, historique et limitation des non-venues reposent sur l'identité du compte.
- Friction à la première commande : atténuée par Google et par la conservation du panier
  pendant la connexion.
- Données personnelles à gérer (RGPD : effacement par anonymisation).
