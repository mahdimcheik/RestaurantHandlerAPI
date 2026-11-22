# E04 — Recherche & découverte

> Statut : **Stable** · Conventions : [README](README.md)

## US-E04-01 — Rechercher un professeur

**En tant que** visiteur
**je veux** filtrer les professeurs sur plusieurs critères
**afin de** trouver celui qui correspond à mon besoin.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`TeacherSearchService`](../../RestaurantHandler/Services/Users/TeacherSearchService.cs), [`FilterTeacher`](../../RestaurantHandler/Contracts/Filters/FilterTeacher.cs), [`TeacherAvailability`](../../RestaurantHandler/Domain/Policies/TeacherAvailability.cs)
`POST /Teachers/all`, anonyme. Le filtre par niveau n'apparaît pas dans
`FilterTeacher` ; la conservation de l'état de recherche relève du front.

**Critères d'acceptation**

- Filtres disponibles : nom, intitulé de produit, ville, code postal, **rayon autour d'un
  point** (10 km par défaut), catégories, niveaux, plage de dates, « disponibles
  uniquement », « mes favoris uniquement ».
- Le résultat est paginé et l'état de la recherche est conservé d'une visite à l'autre.
- Chaque résultat affiche : avatar, nom, titre, note moyenne, nombre d'avis, prix indicatif,
  ville, badges, **nombre de créneaux disponibles** et **date du prochain créneau**.
- Le tri par défaut privilégie les professeurs ayant un créneau proche.
- Le filtre « mes favoris » n'est proposé qu'aux utilisateurs connectés.
- La recherche est accessible **sans être connecté**.

## US-E04-02 — Chercher autour d'une ville

**En tant qu'** élève
**je veux** saisir une ville et un rayon
**afin de** ne voir que les professeurs à distance raisonnable.

**Priorité :** Doit · **Dépend de :** US-E04-01

**État :** Implémentée · **Code :** [`TeacherSearchService`](../../RestaurantHandler/Services/Users/TeacherSearchService.cs), [`HeoHelper`](../../RestaurantHandler/Utilities/HeoHelper.cs)
Rayon et coordonnées dans `FilterTeacher`. L'autocomplétion des communes relève du
front.

**Critères d'acceptation**

- La saisie de ville propose une autocomplétion sur les communes françaises.
- Le rayon est réglable et le filtrage utilise une distance géographique réelle.
- Un professeur sans coordonnées n'apparaît pas dans une recherche par rayon.
- La recherche par ville ou code postal **sans** rayon reste possible.

## US-E04-03 — Ne voir que les professeurs disponibles

**En tant qu'** élève
**je veux** masquer les professeurs sans créneau libre sur la période qui m'intéresse
**afin de** ne pas perdre de temps.

**Priorité :** Doit · **Dépend de :** US-E04-01

**État :** Implémentée · **Code :** [`TeacherAvailability`](../../RestaurantHandler/Domain/Policies/TeacherAvailability.cs)
Définition unique pour le filtre, le tri et le compteur de la recherche. Voir l'écart
R1 côté réservation dans les [règles métier](../20-specs-fonctionnelles/01-regles-metier.md#4-règles-de-disponibilité-et-de-réservation).

**Critères d'acceptation**

- La définition de « créneau disponible » est **unique** et partagée par le filtre, le tri et
  le compteur affiché. Voir
  [règle R1](../20-specs-fonctionnelles/01-regles-metier.md#4-règles-de-disponibilité-et-de-réservation).
- La plage de dates se navigue semaine par semaine.
- Un créneau bloqué par une commande en attente n'est **pas** compté comme disponible.

## US-E04-04 — Gérer mes favoris

**En tant qu'** élève
**je veux** mettre un professeur en favori et l'en retirer
**afin de** retrouver ceux qui m'intéressent.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`FavoriteService`](../../RestaurantHandler/Services/FavoriteService.cs), [`FavoritesController`](../../RestaurantHandler/Controllers/FavoritesController.cs)
Ajout, retrait et état conformes ; unicité en base. Écart : aucun point d'accès ne
liste les favoris (seul le filtre de recherche `InFavorites` les exploite).

**Critères d'acceptation**

- L'action est disponible depuis la carte de résultat et depuis la fiche.
- Un même professeur ne peut être mis en favori qu'une fois (unicité garantie en base).
- La liste des favoris est privée.
- L'action est refusée si je ne suis pas connecté, avec une invitation à me connecter.

## US-E04-05 — Découvrir des professeurs mis en avant

**En tant que** visiteur arrivant sur l'accueil
**je veux** voir une sélection de professeurs
**afin de** comprendre l'offre sans avoir à chercher.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`TeacherSearchService`](../../RestaurantHandler/Services/Users/TeacherSearchService.cs)
`GET /Teachers/featured` : tirage parmi les mieux notés conforme. Écarts : aucun
filtre sur `IsVerified` ni sur l'existence d'un créneau à venir (seuls les professeurs ayant
au moins un avis sont retenus).

**Critères d'acceptation**

- Le bandeau affiche 10 professeurs tirés d'un vivier de 30 parmi les mieux notés, de sorte
  que la sélection tourne au lieu d'épingler toujours les mêmes.
- Seuls des professeurs **vérifiés** et ayant au moins un créneau à venir y figurent.
- Le bloc est rendu côté serveur.

## US-E04-06 — Rechercher depuis l'accueil

**En tant que** visiteur
**je veux** lancer une recherche par matière et par ville dès la page d'accueil
**afin d'** entrer directement dans le produit.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`TeacherSearchService`](../../RestaurantHandler/Services/Users/TeacherSearchService.cs)
Réutilise la recherche ; le bloc d'accueil relève du front.

**Critères d'acceptation**

- Le bloc principal propose une sélection de matières et une saisie de ville.
- La validation ouvre la page de recherche avec les filtres **déjà appliqués**.
- Les matières populaires sont cliquables et déclenchent la même recherche pré-filtrée.
