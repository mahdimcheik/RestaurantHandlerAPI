# ADR-0001 — Contrôleurs + services + EF Core dans un seul projet

**Statut :** Accepté (repris du socle)
**Date :** 2026-11-22

## Contexte

Le socle hérité est organisé en contrôleurs, services et contexte EF Core dans un seul projet.
Le projet d'origine a montré que le danger n'est pas ce découpage mais l'absence de discipline :
services obèses, autorisation dispersée, règles métier dupliquées. Le développement est mené en
solo : la cérémonie d'une architecture en couches séparées coûterait sans rien garantir.

## Décision

Conserver **contrôleurs → services → EF Core dans un seul projet**, avec des règles explicites :

1. Un contrôleur ne contient aucune logique.
2. Un service ne connaît ni le contexte HTTP ni l'identité de l'appelant : elle lui est passée.
3. Aucune entité ne sort de l'API (DTO dans `Contracts/`).
4. Les règles métier (prix, validation, transitions, créneaux) vivent dans `Domain/Policies/`,
   **sans dépendance à la base ni au web**, et sont couvertes par des tests unitaires.
5. Un service qui dépasse 800 lignes est scindé.
6. L'autorisation est déclarée **sur l'opération**, avec refus par défaut.

## Conséquences

- Ajout d'une fonctionnalité simple : simple.
- Les règles critiques (argent, états) sont testables sans infrastructure.
- Rien n'empêche structurellement un service de grossir : discipline de revue.
