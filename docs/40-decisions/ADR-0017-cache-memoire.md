# ADR-0017 — Cache en mémoire des référentiels, sans Redis pour l'instant

**Statut :** Accepté
**Application :** Appliquée (branche `feature/perfs`, 2026-10-04)
**Date :** 2026-10-04

## Contexte

Chaque page du front relit les mêmes référentiels (catégories, niveaux, langues, genres, types
de créneau, d'adresse et de document, statuts de réservation). Ils changent rarement et seulement
par l'admin, mais chaque lecture interroge PostgreSQL. Le géocodage géopf.fr, lui, fait un appel
réseau externe pour une commune déjà géocodée cent fois.

L'API tourne en **une seule instance**. Trois questions :

1. **Où garder le cache** : en mémoire du processus, ou dans Redis ?
2. **Quoi y mettre**, et surtout quoi n'y jamais mettre ?
3. **Comment rester à jour** après une écriture admin ?

## Décision

**Cache en mémoire, via `HybridCache`** (`Microsoft.Extensions.Caching.Hybrid`), enregistré par
`AddHybridCache()` dans [`Program.cs`](../../RestaurantHandler/Program.cs). Avec une seule instance, Redis
n'apporterait que la survie du cache à un redéploiement : un cache vide se remplit à la première
lecture, ce n'est pas un problème. `HybridCache` plutôt qu'`IMemoryCache` pour trois raisons :
l'invalidation par tag, la protection contre la ruée (une seule requête en base quand cinquante
arrivent sur une clé vide), et une montée vers Redis qui se résume à enregistrer un
`IDistributedCache` dans `Program.cs`, sans toucher aux services.

**Le cache ne contient que des copies jetables.** Le vider ne doit jamais rien casser. Liste
blanche, centralisée dans [`CacheKeys`](../../RestaurantHandler/Utilities/CacheKeys.cs) :

| Donnée | Durée | Invalidation |
|---|---|---|
| Catégories, niveaux (liste publique et liste admin avec archivés) | 1 h | tag, à chaque écriture admin |
| Langues, types de créneau, types de document | 1 h | tag, à chaque écriture admin |
| Genres, types d'adresse, statuts de réservation (seed uniquement) | 1 h | aucune, l'expiration suffit |
| Géocodage d'une commune (code postal + ville) | 30 j | aucune ; seules les réussites sont gardées |

La durée d'une heure n'est qu'un filet de sécurité pour une ligne modifiée hors de l'API.

**Jamais en cache :**

- l'argent et les réservations (commandes, paiements, wallet, versements, remboursements,
  créneaux, checkout) : une valeur périmée y vaut une double réservation ou un montant faux ;
- la recherche de professeurs : filtres combinés et disponibilités en temps réel ;
- une réponse portant un lien signé : il expire avant l'entrée ;
- le jeton de renouvellement : c'est l'état de la session, pas une copie. En mémoire, un
  redéploiement déconnecterait tout le monde, et un bannissement pourrait ne pas être vu.

**Invalidation après le `SaveChangesAsync`**, dans le service qui écrit
(`RemoveByTagAsync`). Un tag couvre les deux variantes d'un référentiel, publique et admin.

## Conséquences

**Ce qu'on gagne**

- Les référentiels ne coûtent plus de requête SQL après la première lecture.
- Une commune n'est géocodée qu'une fois par mois au plus.
- Redis s'ajoute sans réécrire de service.

**Ce que ça coûte**

- Toute nouvelle écriture sur un référentiel caché doit invalider son tag, sinon l'admin ne voit
  pas sa modification avant une heure. Les tests de
  [`ReferenceCacheTests`](../../tests/BonProf.IntegrationTests/Caching/ReferenceCacheTests.cs)
  couvrent les catégories.
- Les DTO mis en cache doivent survivre à la sérialisation JSON : constructeur sans paramètre et
  propriétés modifiables. `GenderDetails`, `StatusReservationDetails` et `LanguageDetails` ont été
  adaptés ; le JSON renvoyé est inchangé.

**Ce qu'on surveille**

- Le passage à plusieurs instances : il rendrait Redis nécessaire (sinon une instance garde une
  valeur qu'une autre vient d'invalider), ainsi qu'un backplane SignalR.
- Le widget des profs mis en avant : écarté de ce lot, car `UserDetails` et ses DTO imbriqués ne
  se désérialisent pas. À reprendre si sa requête pèse.

## État dans le code

- [`CacheKeys`](../../RestaurantHandler/Utilities/CacheKeys.cs) : clés, tags et durées ;
- services lus : `CategoryCursusService`, `LevelCursusService`, `LanguagesService`,
  `GendersService`, `TypeSlotsService`, `TypeAddressService`, `StatusReservationService`,
  `TeacherDocumentService.PrivacyDocumentTypesAsync`, `GeocodingService` ;
- invalidation des types de document dans `UserManagementService`.
