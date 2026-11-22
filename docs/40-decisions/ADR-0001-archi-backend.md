# ADR-0001 — Contrôleurs + services + EF Core dans un seul projet

**Statut :** Accepté
**Application :** Partielle
**Date :** 2026-09-12

## Contexte

Trois options étaient ouvertes pour l'API : découpage par cas d'usage avec points d'accès
minimalistes, architecture en couches séparées avec séparation des lectures et des écritures,
ou le découpage actuel — contrôleurs, services, contexte de données, dans un seul projet.

Le code actuel utilise la troisième. Elle fonctionne : le produit est en production. Son défaut
n'est pas le découpage mais **l'absence de discipline** — trois services dépassent 1 300
lignes, l'autorisation est tantôt déclarée sur l'opération tantôt enfouie dans un service
tantôt absente, et les règles métier sont dupliquées.

Un découpage en couches séparées n'aurait corrigé aucun de ces trois défauts : on peut écrire
un service de 1 300 lignes dans n'importe quelle architecture.

## Décision

Conserver **contrôleurs → services → EF Core dans un seul projet**, et corriger les défauts
par des règles explicites :

1. Un contrôleur ne contient aucune logique.
2. Un service ne connaît ni le contexte HTTP ni l'identité de l'appelant : elle lui est
   passée.
3. Aucune entité de base ne sort de l'API.
4. Les règles métier vivent dans un dossier dédié, **sans dépendance à la base ni au web**.
5. Un service qui dépasse 800 lignes est scindé.
6. L'autorisation est déclarée **sur l'opération**, avec refus par défaut.

## Conséquences

**Ce qu'on gagne**

- Aucune rupture de compétence : l'équipe connaît ce découpage.
- La remise en ordre du code actuel est un travail de nettoyage, pas de réécriture — et les invariants
  d'argent, la partie la plus risquée, sont conservés avec leur forme.
- Peu de cérémonie : une fonctionnalité simple reste simple à ajouter.
- Le dossier des règles métier est testable sans infrastructure, ce qui rend la partie
  critique couvrable par des tests rapides.

**Ce que ça coûte**

- Rien n'empêche *structurellement* un service de grossir : la règle des 800 lignes est une
  convention, à faire respecter en revue.
- Le contexte de données est accessible depuis tout service : la discipline sur les
  transactions et les filtres relève de la revue, pas du compilateur.

**Ce qu'on surveille**

La taille des services. C'est l'indicateur avancé du défaut que cette décision cherche à
éviter.

## État dans le code

Le découpage est en place : un projet unique [`RestaurantHandler/`](../../RestaurantHandler/BonProf.csproj), des
contrôleurs, des services et [`MainContext`](../../RestaurantHandler/Persistence/MainContext.cs). État des règles :

| Règle | État | Constat |
|---|---|---|
| 1. Contrôleur sans logique | Partielle | [`AuthController`](../../RestaurantHandler/Controllers/AuthController.cs) pose désormais le cookie de session (lot 3), et [`SlotsController`](../../RestaurantHandler/Controllers/SlotsController.cs) garde de la mise en forme ; plusieurs contrôleurs renvoient `Ok(response)` quel que soit `response.Status` (lot 7) |
| 2. Service sans identité HTTP | Tenue depuis le lot 3 | le contrôleur résout l'appelant (`CallerIdentity`, `GetCallerAsync`) ; aucun service ne reçoit `ClaimsPrincipal` ni `HttpContext` |
| 3. Aucune entité exposée | Tenue | les réponses passent par des représentations de `Contracts/` ; vérifié au lot 3, aucun point d'accès ne renvoie une entité |
| 4. Règles métier isolées | Tenue depuis le lot 3 | [`Domain/Policies/`](../../RestaurantHandler/Domain/Policies) : annulation, résolution de litige, transitions, disponibilité, bornes de mois. `ReservationStatusExtensions` et `TeacherVerificationHelper`, qui écrivent en base, restent dans `Utilities/` |
| 5. Service ≤ 800 lignes | Tenue depuis le lot 3 | les cinq services obèses sont scindés par responsabilité ; les plus gros restants : `UserManagementService` 738, `DisputeService` 735, `JobChron` 717 |
| 6. Autorisation sur l'opération, refus par défaut | Tenue depuis le lot 2 | politique de repli exigeant une session ; liste publique vérifiée par `AnonymousRouteSweepTests`. Des contrôles de propriété restent dans les services |

Voir [dette technique](../50-backlog/03-dette-technique.md) et
[feuille de route](../50-backlog/01-roadmap-refactoring.md).
