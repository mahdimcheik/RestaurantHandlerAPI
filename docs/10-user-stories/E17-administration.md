# E17 — Administration

> Statut : **Stable**
>
> Objectif de conception : **l'administrateur ne traite que l'exception.** Tout ce qui peut
> se résoudre seul doit se résoudre seul.

## US-E17-01 — Gérer les utilisateurs

**En tant qu'** administrateur
**je veux** parcourir et rechercher les comptes
**afin de** traiter les demandes de support.

**Priorité :** Doit

**État :** Partielle · **Code :** [`UserAdministrationService`](../../RestaurantHandler/Services/Users/UserAdministrationService.cs), [`UsersController`](../../RestaurantHandler/Controllers/UsersController.cs)
`POST /Users/filter`, `GET /Users/{id}`, création et modification. Filtres : nom ou e-mail,
un ou plusieurs rôles (`RoleIds`), date d'inscription (`CreatedFrom` inclus, `CreatedTo`
exclu). Écarts : ni statut de compte ni vérification ; pas d'historique de statut.

**Critères d'acceptation**

- Liste paginée, triable et filtrable par nom, rôle, statut de compte et état de
  vérification.
- Le détail montre le profil complet, y compris les informations réservées à
  l'administration : SIRET, état bancaire, historique de statut.
- Je peux créer un compte et en modifier un.
- **Aucune donnée bancaire complète** n'est affichée : seuls l'état et l'identifiant du compte
  chez le prestataire.

## US-E17-02 — Traiter les professeurs en attente

**En tant qu'** administrateur
**je veux** une file des professeurs à vérifier
**afin de** débloquer l'offre rapidement.

**Priorité :** Doit · **Dépend de :** US-E15-04

**État :** Partielle · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs)
`POST /UserManagement/teachers`, filtrable par confirmation. Écarts : pas de tri par
ancienneté de dossier ni de synthèse des pièces manquantes dans la liste.

**Critères d'acceptation**

- Une liste dédiée présente les professeurs non vérifiés, triés par ancienneté de dossier.
- Elle indique, par professeur, ce qui manque.
- Depuis cette liste j'accède directement à l'écran de revue documentaire.

## US-E17-03 — Superviser les réservations et les commandes

**En tant qu'** administrateur
**je veux** accéder à toutes les réservations et commandes
**afin de** répondre à une réclamation.

**Priorité :** Doit

**État :** Partielle · **Code :** [`ReservationQueryService`](../../RestaurantHandler/Services/Reservations/ReservationQueryService.cs), [`OrderService`](../../RestaurantHandler/Services/Orders/OrderService.cs)
Listes administrateur des réservations et commandes, détail et historique. Écart :
aucune journalisation des consultations de conversation.

**Critères d'acceptation**

- Listes complètes, avec les colonnes élève **et** professeur.
- Accès en lecture au détail, à l'historique de statut et à la conversation.
- Toute consultation d'une conversation par un administrateur est journalisée.

## US-E17-04 — Administrer les référentiels

**En tant qu'** administrateur
**je veux** gérer les catégories et les niveaux de cursus
**afin d'** adapter la taxonomie sans redéploiement.

**Priorité :** Devrait · **Voir :** [E18](E18-referentiels-i18n.md)

**État :** Partielle · **Code :** [`CategoryCursusService`](../../RestaurantHandler/Services/CategoryCursusService.cs), [`LevelCursus`](../../RestaurantHandler/Services/LevelCursus.cs), [`TypeSlotsService`](../../RestaurantHandler/Services/TypeSlotsService.cs), [`LanguagesService`](../../RestaurantHandler/Services/LanguagesService.cs)
Catégories et niveaux : CRUD administrateur avec désarchivage. Écarts : types de
créneau et langues sans contrôle de rôle ; pas de désarchivage pour eux.

**Critères d'acceptation**

- Création, modification, archivage et **désarchivage**.
- Un élément référencé par des produits n'est jamais détruit : il est archivé et disparaît des
  listes de sélection sans casser l'existant.
- Les libellés sont saisis avec au minimum la langue par défaut.

## US-E17-05 — Répondre aux demandes de contact

**En tant qu'** administrateur
**je veux** voir les demandes adressées à la plateforme
**afin d'** y répondre.

**Priorité :** Pourrait · **Dépend de :** US-E13-06

**État :** Absente
Les demandes de contact ne sont pas persistées.

**Critères d'acceptation**

- Les demandes sont listées avec leur auteur, leur date et leur objet.
- Le traitement d'une demande la marque comme traitée, avec son auteur.

## US-E17-06 — Séparer administration et super-administration

**En tant que** super-administrateur
**je veux** disposer de droits distincts de ceux d'un administrateur
**afin de** protéger les paramètres sensibles.

**Priorité :** Devrait · **À arbitrer** — voir
[questions ouvertes](../50-backlog/02-questions-ouvertes.md)

**État :** Absente
`SuperAdmin` existe comme rôle, mais les opérations sont ouvertes à
`Admin,SuperAdmin` indistinctement ; aucun paramètre de plateforme n'est modifiable à
l'exécution.

**Critères d'acceptation**

- Réservé au super-administrateur : création et retrait des comptes administrateurs, gestion
  des types de pièces justificatives, modification des paramètres de plateforme (taux,
  fenêtres, plafonds).
- L'administrateur conserve l'exploitation quotidienne : revue documentaire, arbitrage,
  gestion des utilisateurs, référentiels de cursus, bilans.
- La séparation est appliquée **côté serveur** ; masquer un bouton ne suffit pas.
- Toute action de super-administration est journalisée.

## US-E17-07 — Accéder aux outils techniques en sécurité

**En tant qu'** exploitant
**je veux** que les interfaces techniques exigent une authentification d'administrateur
**afin de** ne pas exposer de données sensibles.

**Priorité :** Doit

**État :** Absente · **Code :** [`Program`](../../RestaurantHandler/Program.cs), [`MailService`](../../RestaurantHandler/Services/MailService.cs)
Tableau de bord Hangfire anonyme, corps d'e-mail rendu dans les arguments, description
OpenAPI publiée partout. Voir la [dette technique](../50-backlog/03-dette-technique.md).

**Critères d'acceptation**

- Le tableau de bord des tâches de fond exige un administrateur authentifié et est monté
  **après** l'intergiciel d'authentification.
- Aucun argument de tâche ne contient de contenu sensible : pas de corps d'e-mail rendu, pas
  de lien de réinitialisation, pas de données de carte.
- La documentation d'API interactive n'est pas exposée en production.
- Voir [dette technique](../50-backlog/03-dette-technique.md).

## US-E17-08 — Écrire par e-mail à un groupe d'utilisateurs

**En tant qu'** administrateur ou super-administrateur
**je veux** envoyer un e-mail aux professeurs, aux élèves, aux administrateurs ou à tout le monde
**afin d'** annoncer une maintenance, une nouveauté ou une information de service.

**Priorité :** Devrait · **Décision :** [ADR-0023](../40-decisions/ADR-0023-envois-mail-groupes.md)

**État :** Implémentée · **Code :** [`MailBroadcastService`](../../RestaurantHandler/Services/Mailing/MailBroadcastService.cs), [`MailBroadcastsController`](../../RestaurantHandler/Controllers/MailBroadcastsController.cs)
`POST /mail-broadcasts/admin`, `POST /mail-broadcasts/admin/count`, historique par `POST /mail-broadcasts/search`.

**Critères d'acceptation**

- Je coche un ou plusieurs publics ; le nombre de destinataires s'affiche avant l'envoi.
- Seuls les comptes actifs et non archivés sont visés ; je ne reçois pas mon propre message ;
  un compte à plusieurs rôles le reçoit une seule fois.
- Chaque destinataire reçoit son propre e-mail ; « Répondre » m'écrit directement.
- Au plus 10 envois par heure et par administrateur.
- L'historique liste tous les envois, avec leur auteur, leur public et leur nombre de
  destinataires.

## US-E17-09 — Accorder le rôle éditeur à un professeur

**En tant qu'** administrateur ou super-administrateur
**je veux** autoriser un professeur à rédiger des articles, puis pouvoir le lui retirer
**afin d'** ouvrir le blog à des profs de confiance sans leur donner la main sur le reste.

**Priorité :** Devrait · **Décision :** [ADR-0025](../40-decisions/ADR-0025-role-editeur.md)

**État :** Implémentée · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs), [`ArticleService`](../../RestaurantHandler/Services/ArticleService.cs)
`PATCH /usermanagement/users/{id}/editor?enabled=true|false`, liste de l'éditeur par `POST /articles/mine`.

**Critères d'acceptation**

- Depuis la liste des utilisateurs, j'active ou je retire le rôle « Éditeur » d'un professeur ;
  un élève ne peut pas le recevoir.
- Le professeur éditeur voit « Mes articles » dans son espace ; il crée, modifie, publie et
  archive ses articles, signés de son nom, sans pouvoir toucher à ceux des autres.
- Sur la page publique, le nom de l'auteur mène à son profil.
- Si je retire le rôle, il ne peut plus écrire, et ses articles restent en ligne.
