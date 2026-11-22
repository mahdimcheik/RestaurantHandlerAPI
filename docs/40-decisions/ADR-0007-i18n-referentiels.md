# ADR-0007 — Libellés de référentiel en colonne JSON sur la ligne

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

Les libellés métier — statuts, niveaux, catégories, types — étaient traduits côté client par
des tuyaux d'affichage codés en dur, en français. Dès qu'un administrateur crée une
**nouvelle** catégorie à l'exécution, le client ne peut pas la connaître au moment de sa
construction : il faudrait modifier le code et redéployer pour chaque ajout.

Une première approche distinguait les listes ouvertes gérées par l'administration, traduites
en base, des énumérations système fermées, laissées au catalogue de traduction du client. Ce
découpage a été abandonné : la frontière bouge — une liste fermée aujourd'hui devient ouverte
demain — et il obligeait à traiter deux mécanismes.

## Décision

**Toutes** les valeurs de référentiel portent un dictionnaire de libellés, stocké en colonne
**JSON sur la ligne**, indexé par code de langue en minuscules.

Trois règles :

1. Le **français est obligatoire** à l'écriture ; une valeur sans libellé français est
   refusée.
2. Le nom technique reste le **repli canonique** et la clé d'unicité ; aucun libellé vide
   n'est jamais renvoyé.
3. Le serveur renvoie le **dictionnaire complet**, sans négocier la langue à partir de
   l'en-tête de requête : le client change de langue sans rappeler le serveur.

## Conséquences

**Ce qu'on gagne**

- Un ajout de catégorie par l'administration est immédiatement correct dans l'interface, sans
  redéploiement.
- Aucun libellé métier codé en dur côté client.
- Pas de jointure : le libellé arrive avec l'entité.
- La configuration se fait **une fois**, par convention sur le type de base, plutôt que
  répétée sur chaque référentiel.

**Ce que ça coûte**

- **On perd les contraintes d'unicité et de longueur par langue** que donnerait une table de
  traductions normalisée.
- **On perd le tri alphabétique sur le libellé traduit côté serveur.** Un tri par libellé se
  fait donc côté client, ou sur le nom technique.
- Un index spécifique sera nécessaire le jour où il faudra chercher dans les libellés.
- Le pilote de base de données doit être configuré pour le typage dynamique du JSON — oubli
  classique qui casse au démarrage, pas à la compilation.

**Ce qu'on surveille**

Le besoin de **rechercher ou trier par libellé traduit en base**. C'est le seul cas où une
table normalisée serait supérieure ; si ce besoin apparaît, l'ADR est à revoir.

## État dans le code

`TranslatableModelOption` ([`Interfaces.cs`](../../RestaurantHandler/Domain/Entities/BaseModel.cs)), colonne
`Translations` en `jsonb` configurée dans [`MainContext`](../../RestaurantHandler/Persistence/MainContext.cs),
validation par [`RequiresDefaultLanguageAttribute`](../../RestaurantHandler/Utilities/RequiresDefaultLanguageAttribute.cs).
