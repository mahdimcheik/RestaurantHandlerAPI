# ADR-0005 — Un seul restaurant, pas de multi-tenant

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

L'application est destinée à **un seul établissement** de restauration rapide à emporter. Un
modèle multi-restaurants imposerait un identifiant d'établissement sur chaque table, un
filtrage systématique, une gestion de comptes marchands SumUp par restaurant et une
administration de plateforme.

## Décision

**Une instance = un restaurant.** Aucune notion d'établissement dans le modèle ; les
informations du restaurant (identité légale, horaires, paramètres) sont une configuration
unique (`RestaurantSettings`).

## Conséquences

- Modèle et requêtes simples ; aucun risque de fuite entre restaurants.
- Réutiliser le produit pour un autre restaurant = déployer une autre instance (autre base,
  autre configuration).
- Passer plus tard au multi-tenant serait une refonte : à ne reconsidérer que sur un besoin
  commercial avéré.
