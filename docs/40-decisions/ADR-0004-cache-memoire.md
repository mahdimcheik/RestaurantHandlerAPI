# ADR-0004 — Cache en mémoire, jamais pour l'argent ni les commandes

**Statut :** Accepté (repris du socle)
**Date :** 2026-11-22

## Contexte

La carte publique et l'état d'ouverture sont lus à chaque visite, surtout en heure de pointe,
et changent rarement (sauf ruptures et pause). L'API tourne en **une seule instance**.

## Décision

`HybridCache` en mémoire (déjà enregistré dans `Program.cs`), clés centralisées dans
`CacheKeys`.

| En cache | Invalidation |
|---|---|
| Carte publique (catégories, produits, menus, disponibilité) | par tag, à **chaque** écriture Manager et à chaque rupture |
| Horaires, fermetures, paramètres publics | par tag à chaque écriture |

**Jamais en cache :** commandes, paiements, capacité des créneaux, état de pause (lu en base
ou invalidé immédiatement), jetons de session, réponses portant un lien signé.

## Conséquences

- La carte ne coûte plus de requête SQL après la première lecture.
- Toute nouvelle écriture sur la carte **doit** invalider le tag, sinon le client voit un
  produit en rupture comme disponible — la validation serveur le refusera, mais l'expérience
  est dégradée. Un test d'intégration le couvre.
- Plusieurs instances imposeraient Redis et un backplane SignalR.
