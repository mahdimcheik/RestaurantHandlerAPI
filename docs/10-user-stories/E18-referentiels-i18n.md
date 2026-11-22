# E18 — Référentiels & internationalisation

> Statut : **Stable**
>
> Le problème résolu ici : quand un administrateur crée une **nouvelle** catégorie à
> l'exécution, le front ne peut pas la connaître au moment de sa construction. Traduire les
> libellés dans le code client ne passe pas à l'échelle.

## US-E18-01 — Traduire les libellés administrables

**En tant qu'** administrateur
**je veux** saisir le libellé d'une valeur de référentiel dans plusieurs langues
**afin que** l'interface soit correcte sans intervention d'un développeur.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`Interfaces`](../../RestaurantHandler/Domain/Entities/BaseModel.cs), [`MainContext`](../../RestaurantHandler/Persistence/MainContext.cs), [`RequiresDefaultLanguageAttribute`](../../RestaurantHandler/Utilities/RequiresDefaultLanguageAttribute.cs)
Tous les référentiels dérivent de `TranslatableModelOption` ; le français est exigé
sur les DTO d'écriture existants (catégories, niveaux, langues, statuts de réservation, types
de créneau, types de pièce).

**Critères d'acceptation**

- Toute valeur de référentiel porte un dictionnaire de libellés indexé par code de langue en
  minuscules.
- Le **français est obligatoire** à l'écriture : un enregistrement sans libellé français est
  refusé.
- Le nom technique reste le **repli canonique** et l'identifiant d'unicité ; jamais de libellé
  vide affiché, même si une langue manque.
- Référentiels concernés : genre, statut de compte, type d'adresse, langue, niveau, catégorie,
  type de créneau, statut de réservation, statut de commande, statut de transaction, type de
  transaction professeur, moyen de paiement, statut et issue de litige, type de pièce
  justificative, type de notification.

## US-E18-02 — Laisser le client choisir la langue d'affichage

**En tant qu'** application cliente
**je veux** recevoir **tous** les libellés d'une valeur
**afin de** changer de langue sans rappeler le serveur.

**Priorité :** Devrait · **Dépend de :** US-E18-01

**État :** Implémentée · **Code :** [`Interfaces`](../../RestaurantHandler/Domain/Entities/BaseModel.cs)
Dictionnaire complet renvoyé, aucune négociation par en-tête. Le choix côté client
relève du front.

**Critères d'acceptation**

- Le serveur renvoie le **dictionnaire complet**, sans négocier la langue à partir de l'en-tête
  de requête.
- Le client choisit l'entrée correspondant à sa langue, et retombe sur le français si elle
  manque.
- Aucun libellé métier n'est codé en dur côté client.

## US-E18-03 — Administrer les catégories et les niveaux

**En tant qu'** administrateur
**je veux** gérer la taxonomie des cours
**afin de** suivre l'évolution de l'offre.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`CategoryCursusService`](../../RestaurantHandler/Services/CategoryCursusService.cs), [`LevelCursus`](../../RestaurantHandler/Services/LevelCursus.cs)
CRUD administrateur avec désarchivage (`PATCH …/{id}/unarchive`).

**Critères d'acceptation**

- Création, modification, archivage et désarchivage, avec libellé, couleur et icône.
- Une valeur archivée disparaît des listes de sélection mais reste lisible sur les produits
  et réservations qui la référencent.
- La suppression définitive d'une valeur référencée est refusée.

## US-E18-04 — Consulter les référentiels

**En tant qu'** application cliente
**je veux** récupérer chaque référentiel en un appel
**afin d'** alimenter les listes déroulantes et les filtres.

**Priorité :** Doit

**État :** Partielle · **Code :** [`GendersController`](../../RestaurantHandler/Controllers/GendersController.cs), [`LanguagesController`](../../RestaurantHandler/Controllers/LanguagesController.cs)
Un point d'accès par référentiel, en lecture anonyme. Écarts : routes hétérogènes
(`GET …/all`, `POST /Languages/all`, `GET /Order/statuses`, `GET /Dispute/statuses`
authentifié) ; pas de référentiel des statuts de compte.

**Critères d'acceptation**

- Un point d'accès par référentiel, renvoyant les valeurs non archivées, ordonnées.
- Les référentiels publics sont accessibles sans authentification, puisqu'ils alimentent la
  recherche publique.
- Les réponses sont mises en cache côté client pendant la session : ces listes changent
  rarement.

## US-E18-05 — Préparer l'ajout d'une langue

**En tant qu'** équipe
**je veux** que l'ajout d'une deuxième langue soit mécanique
**afin de** ne pas refaire l'architecture le jour venu.

**Priorité :** Devrait

**État :** Hors API
Relève du front. Côté API, rien ne fige la langue : le dictionnaire complet est
renvoyé.

**Critères d'acceptation**

- **La v1 ne livre que le français.**
- Aucune chaîne visible par l'utilisateur n'est écrite en dur dans un composant : tout passe
  par un catalogue de traduction, même avec une seule langue.
- Le changement de langue ne doit **pas** exiger un rechargement complet de la page :
  l'architecture évite de figer la locale au démarrage. C'est une limite connue du front actuel.
- Les pages publiques prévoient une URL par langue, pour le référencement le jour où une
  seconde langue arrive.
