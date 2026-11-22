# ADR-0002 — Session par cookie `httpOnly`

**Statut :** Accepté — à appliquer (repris du socle)
**Date :** 2026-11-22

## Contexte

Le socle émet un JWT d'accès porté par l'en-tête `Authorization` (donc lisible par le
JavaScript de la page) et un jeton de renouvellement en cookie `httpOnly`. Un jeton lisible
par le script est exfiltrable en cas d'injection ; il n'est pas révocable avant expiration.
La PWA affiche du contenu saisi (notes client, libellés) et sera installée sur des appareils
partagés du restaurant.

## Décision

À terme, la session est portée par un **cookie `httpOnly`, `Secure`, `SameSite=Lax`**,
invalidable côté serveur, avec rotation à chaque renouvellement. En V1, le JWT d'accès reste
**court** si la bascule n'est pas faite avant l'ouverture (cf. feuille de route).

## Conséquences

- Protection anti-falsification (CSRF) obligatoire sur les requêtes mutantes.
- `SameSite=Lax` à vérifier sur le retour de la page de paiement SumUp.
- SignalR utilise la même authentification.
- Les appareils du restaurant restent connectés grâce au renouvellement, et un Manager peut
  révoquer toutes ses sessions.
