# ADR-0011 — Français uniquement, carte sans traductions

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

Le socle sait stocker des traductions en `jsonb` (`TranslatableModelOption.Translations`,
utilisé par `StatusAccount` et `Notification`). Le restaurant n'a qu'une clientèle
francophone visée.

## Décision

Interface et carte **en français uniquement**. Les entités de la carte ont des libellés simples
(`Name`, `Description`) sans colonne `Translations`. Le mécanisme existant reste en place pour
les référentiels du socle.

## Conséquences

- Saisie de carte simple pour le Manager.
- Ajouter une langue plus tard demandera une migration de la carte (colonne `Translations` sur
  le modèle des référentiels existants) et l'i18n du front.
