# ADR-0012 — Remplacements au prix de référence de l'ingrédient, suppléments en groupe d'options

**Statut :** Accepté
**Date :** 2026-11-23

## Contexte

Le modèle initial demandait au Manager, pour chaque produit, la liste des remplaçants de chaque
ingrédient (`IngredientSubstitution`, avec surcoût) et la liste des suppléments
(`ProductSupplement`, prix et quantité max). Pour un restaurant dont les ingrédients sont dans la
même fourchette de prix et où le cuisinier ajuste les quantités à la main (125 g de mozzarella
→ 100 g de poulet), cette saisie est trop lourde. Seuls les écarts extrêmes (mozzarella →
saumon) doivent coûter plus cher.

## Décision

- Chaque ingrédient a un **prix de référence** (`Ingredient.PriceCents`, 0 par défaut) et une
  case **« proposé en remplacement »** (`IsOfferedAsReplacement`, vrai par défaut ; faux pour la
  pâte, le pain…).
- Un ingrédient **retirable** de la composition peut être remplacé par **n'importe quel**
  ingrédient disponible et proposé en remplacement, au surcoût
  `max(0, prix du remplaçant − prix du remplacé)`. Le prix ne baisse jamais.
- Les **suppléments** passent par un **groupe d'options facultatif** (« Suppléments pizza » :
  minimum 0, choix offerts 0, un choix payant par ingrédient, plusieurs fois si besoin), partagé
  entre produits.
- `IngredientSubstitution` et `ProductSupplement` sont supprimés. `ModificationType.Supplement`
  reste pour les commandes passées ; le panier le refuse.

## Conséquences

- Fiche produit réduite à la liste des ingrédients (retirable ou non) et aux groupes d'options.
- Un ingrédient porte deux prix indépendants : sa référence pour les remplacements et, s'il sert
  de supplément, le surcoût de son choix dans le groupe (pré-rempli depuis la référence).
- Le client peut remplacer un ingrédient par un autre de même prix sans surcoût, y compris par
  un ingrédient déjà présent : accepté, la cuisine ajuste.
- La carte publique expose une seule liste `replacements` (ingrédients proposés et leur prix) ;
  le surcoût affiché est indicatif, le serveur le recalcule (R2).
- Statistiques : les choix d'options payants liés à un ingrédient comptent comme suppléments.
