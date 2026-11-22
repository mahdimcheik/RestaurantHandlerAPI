# Rôles & permissions

> Statut : **Stable** — la matrice fait foi. Masquer un bouton n'est jamais une protection :
> **toute règle ci-dessous est vérifiée côté serveur.**

## Rôles

| Rôle | Portée |
|---|---|
| *Anonyme* | Visiteur non connecté. |
| `Student` | Achète des cours. |
| `Teacher` | Vend des cours. |
| `Admin` | Exploite la plateforme. |
| `SuperAdmin` | Administre les administrateurs et les paramètres sensibles. |
| `Editor` | Professeur qui rédige ses propres articles, accordé par l'administration ([ADR-0025](../40-decisions/ADR-0025-role-editeur.md)). |

**Cumul :** `Student` + `Teacher` est autorisé (voir [E01-10](../10-user-stories/E01-authentification-comptes.md)).
`Editor` ne se porte qu'avec `Teacher`.
Le cumul avec un rôle d'administration est interdit.

## Deux niveaux de contrôle, toujours

1. **Le rôle** — ai-je le droit d'appeler cette opération ?
2. **La propriété** — cette ressource est-elle la mienne ?

Le second est le plus souvent oublié et le plus dangereux : un professeur authentifié qui
modifie le produit d'un autre professeur passe le premier contrôle. **L'identité utilisée
pour ce contrôle vient toujours de la session, jamais d'un identifiant fourni par le
client.**

## Matrice

Légende : ● autorisé · ○ autorisé sur ses propres ressources · — interdit

### Comptes et profils

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| S'inscrire, se connecter, réinitialiser son mot de passe | ● | — | — | — | — |
| Consulter la fiche publique d'un professeur | ● | ● | ● | ● | ● |
| Consulter le profil d'un élève | — | ○ | ○ *(réservation partagée)* | ● | ● |
| Modifier son profil, son avatar, ses adresses | — | ○ | ○ | ○ | ○ |
| Gérer ses formations et expériences | — | — | ○ | — | — |
| Passer en professionnel (SIRET) | — | — | ○ | — | — |
| Lister et rechercher tous les comptes | — | — | — | ● | ● |
| Créer ou modifier un compte | — | — | — | ● | ● |
| Activer ou bannir un compte | — | — | — | ● | ● |
| Créer ou retirer un compte administrateur | — | — | — | — | ● |

### Offre

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| Consulter les produits d'un professeur | ● | ● | ● | ● | ● |
| Créer, modifier, supprimer un produit | — | — | ○ | — | — |
| Consulter les créneaux disponibles | ● | ● | ● | ● | ● |
| Créer, modifier, supprimer un créneau | — | — | ○ | — | — |
| Rechercher des professeurs | ● | ● | ● | ● | ● |
| Gérer ses favoris | — | ○ | — | — | — |

### Réservations et commandes

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| Réserver un créneau | — | ● | — | — | — |
| Lister ses réservations | — | ○ | ○ | ● *(toutes)* | ● *(toutes)* |
| Consulter le détail d'une réservation | — | ○ | ○ | ● | ● |
| Accepter ou refuser une réservation *(flux sans paiement)* | — | — | ○ | — | — |
| Annuler une réservation | — | ○ | ○ | — | — |
| Prévisualiser une annulation | — | ○ | — | — | — |
| Valider par code de présence | — | — | ○ | ● | ● |
| Lister ses commandes | — | ○ | ○ | ● *(toutes)* | ● *(toutes)* |
| Consulter ses commandes en attente | — | ○ | — | — | — |
| Télécharger la facture d'une commande | — | ○ | — | ● | ● |
| Consulter la comptabilité d'une commande | — | — | — | ● | ● |

### Argent

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| Créer un paiement pour une commande | — | ○ | — | — | — |
| Ouvrir ou reprendre son compte de paiement | — | — | ○ | — | — |
| Consulter son portefeuille et son solde | — | — | ○ | ● | ● |
| Consulter le portefeuille d'un autre professeur | — | — | — | ● | ● |
| Consulter son bilan mensuel | — | — | ○ | — | — |
| Consulter le bilan mensuel global | — | — | — | ● | ● |
| Déclencher un versement ou un remboursement à la main | — | — | — | — | — |

> **Personne ne déclenche un mouvement d'argent à la main.** Versements et remboursements
> sont produits par des tâches récurrentes qui lisent la base. C'est ce qui rend le système
> rejouable et auditable. Voir
> [tâches planifiées](../30-specs-techniques/06-jobs-planifies.md).

### Confiance

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| Ouvrir un litige | — | ○ | — | — | — |
| Accepter un litige | — | — | ○ | — | — |
| Arbitrer un litige | — | — | — | ● | ● |
| Lister les litiges | — | ○ | ○ | ● *(tous)* | ● *(tous)* |
| Déposer un avis | — | ○ *(réservation terminée)* | — | — | — |
| Lire les avis d'un professeur | ● | ● | ● | ● | ● |
| Archiver ou supprimer un avis | — | — | — | ● | ● |
| Déposer ou supprimer une pièce justificative | — | — | ○ | — | — |
| Valider, refuser ou demander une pièce | — | — | — | ● | ● |
| Gérer le catalogue des types de pièces | — | — | — | — | ● |

### Communication et référentiels

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| Lire et écrire dans la conversation d'une réservation | — | ○ | ○ | ● *(lecture, journalisée)* | ● |
| Envoyer un fichier dans la conversation d'une réservation | — | ○ | ○ | — | — |
| Contacter un professeur depuis sa fiche | — | ● | — | — | — |
| Lister ses notifications, les marquer lues | — | ○ | ○ | ○ | ○ |
| Consulter les référentiels publics | ● | ● | ● | ● | ● |
| Gérer catégories et niveaux | — | — | — | ● | ● |
| Modifier les paramètres de plateforme | — | — | — | — | ● |
| Accéder au tableau de bord des tâches de fond | — | — | — | ● | ● |

### Articles

| Opération | Anon. | Student | Teacher | Editor | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Lire les articles publiés | ● | ● | ● | ● | ● | ● |
| Commenter un article publié | — | ● | ● | ● | ● | ● |
| Créer, modifier, publier, archiver un article | — | — | — | ○ *(signé de son nom)* | ● | ● |
| Masquer un commentaire | — | — | — | — | ● | ● |
| Consulter les statistiques des articles | — | — | — | — | — | ● |
| Accorder ou retirer le rôle `Editor` | — | — | — | — | ● | ● |

### Suivi de cours

| Opération | Anon. | Student | Teacher | Admin | SuperAdmin |
|---|:--:|:--:|:--:|:--:|:--:|
| Lister ses élèves | — | — | ○ *(réservation partagée)* | — | — |
| Créer un tableau de suivi | — | — | ○ *(réservation partagée)* | — | — |
| Lister et consulter ses tableaux | — | ○ *(lecture seule)* | ○ | — | — |
| Renommer, archiver un tableau | — | — | ○ | — | — |
| Gérer les listes d'un tableau | — | — | ○ | — | — |
| Créer, modifier, déplacer, archiver une carte | — | — | ○ | — | — |

> L'administration n'a **pas** accès aux tableaux de suivi : ce sont des notes de travail
> entre un professeur et un élève, sans enjeu d'argent ni d'arbitrage. Un litige s'appuie sur
> la réservation et sa conversation, pas sur le suivi.

## Règles de propriété

| Ressource | Propriétaire |
|---|---|
| `Product`, `Slot`, `Experience` | le professeur porté par la ressource |
| `Formation`, `Address` | l'utilisateur porté par la ressource |
| `Reservation` | l'élève **et** le professeur du créneau |
| `Order`, `Payment`, `Refund` | l'élève **et** le professeur de la commande |
| `TeacherWalletTransaction`, `TeacherPayout` | le professeur |
| `Dispute` | l'élève à l'origine **et** le professeur concerné |
| `PrivacyDocument` | le professeur déposant |
| `Review` | l'élève auteur, en lecture par tous |
| `Conversation` | les deux parties de la réservation |
| `Notification` | son destinataire |
| `Article` | son auteur (`AuthorId`) s'il porte encore le rôle `Editor` ; l'administration sur tous |
| `Board` | le professeur en écriture, l'élève du tableau en lecture |
| `BoardList`, `BoardCard` | ceux de leur tableau |
| `BoardCardAttachment` | ceux de leur carte : le professeur ajoute et supprime, l'élève lit et télécharge |

## Écarts dans le code actuel

Relevé au 2026-09-16 en lisant les attributs des contrôleurs. Au départ, il n'existait
**aucune politique d'autorisation de repli** dans [`Program.cs`](../../RestaurantHandler/Program.cs) :
une opération sans attribut était ouverte à tous. **Depuis le lot 2**, la politique de repli
exige une session, et les seules opérations anonymes sont la liste publique vérifiée par
`AnonymousRouteSweepTests`.

| Opération | Contrôleur | État actuel | Cible |
|---|---|---|---|
| Créer une session de paiement (`POST /Payment/checkout`) | [`PaymentController`](../../RestaurantHandler/Controllers/PaymentController.cs) | ~~`[AllowAnonymous]`~~ **corrigé au lot 0** | `Student` + propriété de la commande |
| Ouvrir un compte de paiement (`POST /Payment/connect/account`) | idem | ~~`[AllowAnonymous]`~~ **corrigé au lot 0** | `Teacher` + propriété |
| Déclencher un transfert (`POST /Payment/connect/transfer`) | idem | ~~`[AllowAnonymous]`~~ **supprimé au lot 0** | **supprimé** : aucun mouvement d'argent à la main |
| Téléverser un avatar (`POST /Auth/upload-avatar`) | [`AuthController`](../../RestaurantHandler/Controllers/AuthController.cs) | ~~`[AllowAnonymous]`~~ **corrigé au lot 2** | authentifié, sur son propre compte |
| Lister les écritures d'un professeur (`POST /TeacherWallets/teacher/{id}/transactions`) | [`TeacherWalletsController`](../../RestaurantHandler/Controllers/TeacherWalletsController.cs) | ~~`[AllowAnonymous]`~~ **corrigé au lot 2** | le professeur ou un administrateur |
| Créer, modifier, supprimer un produit | [`ProductController`](../../RestaurantHandler/Controllers/ProductController.cs) | ~~aucun attribut~~ **corrigé au lot 2** | `Teacher` + propriété |
| Gérer ses adresses | [`AddressesController`](../../RestaurantHandler/Controllers/AddressesController.cs) | ~~aucun attribut ; modification sans propriété~~ **corrigé au lot 2** | authentifié + propriété |
| Modifier, supprimer une formation | [`FormationsController`](../../RestaurantHandler/Controllers/FormationsController.cs) | ~~seul `POST` porte `[Authorize]` ; suppression sans propriété~~ **corrigé au lot 2** | `Teacher` + propriété |
| Créer, modifier, supprimer un type de créneau | [`TypeSlotsController`](../../RestaurantHandler/Controllers/TypeSlotsController.cs) | ~~aucun attribut~~ **corrigé au lot 2** | `Admin` |
| Créer, modifier, supprimer une langue ; modifier les langues d'un professeur | [`LanguagesController`](../../RestaurantHandler/Controllers/LanguagesController.cs) | ~~aucun attribut~~ **corrigé au lot 2** | `Admin` ; `Teacher` + propriété |
| Téléverser, télécharger, supprimer un fichier | `FileController` | ~~aucun attribut ; `IFileService` jamais enregistré~~ **supprimé au lot 2** | supprimé au profit du stockage unifié ([ADR-0008](../40-decisions/ADR-0008-stockage-objet-unifie.md)) |
| Point d'accès vide `POST /Auth/dev` | [`AuthController`](../../RestaurantHandler/Controllers/AuthController.cs) | ~~authentifié, sans effet~~ **supprimé au lot 2** | supprimé |
| Tableau de bord des tâches de fond `/hangfire` | [`Program.cs`](../../RestaurantHandler/Program.cs) | ~~monté **avant** l'authentification : anonyme~~ **corrigé au lot 0** | `Admin` authentifié |
| Connexion d'un compte banni ou non activé | [`SignInService`](../../RestaurantHandler/Services/Auth/SignInService.cs) | ~~acceptée (mot de passe, renouvellement)~~ **corrigé au lot 0** | refusée |
| Lire le profil d'un élève (`GET /Students/profile/{id}`, `GET /Students/user/{id}`) | [`StudentsController`](../../RestaurantHandler/Controllers/StudentsController.cs) | ~~tout compte connecté ; `user/{id}` sans attribut~~ **corrigé au lot 2** | l'élève, un professeur d'une réservation partagée, l'administration |
| Lister ses réservations sur une période (`POST /Slots/student`) | [`SlotsController`](../../RestaurantHandler/Controllers/SlotsController.cs) | ~~`[AllowAnonymous]`~~ **corrigé au lot 2** | `Student` |
| Alimentation de démonstration (`POST /Dev/seed-*`) | [`DevController`](../../RestaurantHandler/Controllers/DevController.cs) | ~~aucun attribut, garde d'environnement seul~~ **corrigé au lot 2** | `Admin` et environnement autorisé |

Les référentiels en lecture seule (`Genders`, `Roles`, `StatusReservations`,
`TypeAddresses`, `CategoryCursus`, `LevelCursus`, `TypeSlots`, `Languages`), l'offre
publique et les webhooks Stripe portent `[AllowAnonymous]` explicitement.

**Règle de conception retenue :** l'autorisation est déclarée **sur l'opération**, jamais
uniquement enfouie dans un service. Le refus par défaut est la position de repli : une
opération sans déclaration explicite exige une session. Une ressource qui n'appartient pas à
l'appelant répond `404`. Voir
[ADR-0001](../40-decisions/ADR-0001-archi-backend.md).
