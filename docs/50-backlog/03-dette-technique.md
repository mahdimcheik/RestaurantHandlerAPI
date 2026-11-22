# Dette technique

> Statut : **Stable** — inventaire de ce qui est à corriger **dans ce dépôt**, relevé au
> 2026-09-16. Chaque entrée porte un identifiant `D-xx` stable, cité par la
> [feuille de route](01-roadmap-refactoring.md) ; une entrée corrigée est barrée avec la date
> et la contribution qui la corrige, jamais supprimée.
>
> Il reprend l'ancien `TODO.md` et l'inventaire « dette héritée » des specs de NewBonProf, qui
> décrivait ce même code, et les complète de l'état des lieux fait pour ces specs.

## Échelle de gravité

| Gravité | Sens |
|---|---|
| **Critique** | exploitable aujourd'hui, perte d'argent ou prise de contrôle de compte |
| **Élevée** | fuite de données d'autrui, ou règle métier contournable |
| **Moyenne** | défaut réel, sans exploitation directe |
| **Faible** | hygiène, lisibilité |

## 1. Sécurité

### ~~D-01 — Transfert manuel anonyme~~

**Gravité : critique — perte d'argent.**

`POST /Payment/connect/transfer` est `[AllowAnonymous]` et appelle
`PaymentService.CreateTransferAsync` avec **le compte de destination et le montant fournis
par l'appelant** ([`PaymentController`](../../RestaurantHandler/Controllers/PaymentController.cs),
[`StripeConnectService`](../../RestaurantHandler/Services/Payments/StripeConnectService.cs)). N'importe qui peut transférer
le solde Stripe de la plateforme vers un compte connecté de son choix.

**Correction :** supprimer le point d'accès. Aucun mouvement d'argent ne se déclenche à la
main ([flux d'argent](../20-specs-fonctionnelles/04-flux-argent.md#7-ce-qui-est-intentionnellement-absent)).
`CreateTransferAsync` reste utilisé par `JobChron`.

> **Corrigé le 2026-09-16 (lot 0)** : route supprimée.

### ~~D-02 — Onboarding Stripe anonyme, professeur désigné par la requête~~

**Gravité : critique — détournement de versements.**

`POST /Payment/connect/account` est anonyme et retrouve le professeur par l'adresse e-mail
**fournie dans le corps**. Un tiers peut obtenir le lien d'onboarding d'un professeur pas
encore confirmé et y saisir ses propres coordonnées bancaires : les versements de ce
professeur lui reviendraient.

**Correction :** `[Authorize(Roles = "Teacher")]`, professeur lu dans la session, corps de
requête vide.

> **Corrigé le 2026-09-16 (lot 0)** : réservé au rôle professeur, professeur lu dans la session ; le champ `Email` du corps est conservé pour le client généré mais ignoré.

### ~~D-03 — Création de session de paiement anonyme~~

**Gravité : élevée.**

`POST /Payment/checkout` est anonyme et désigne la commande par son identifiant seul.
Un tiers peut expirer la session d'un élève et en émettre une nouvelle.

**Correction :** `[Authorize(Roles = "Student")]` et contrôle que la commande appartient à
l'appelant.

> **Corrigé le 2026-09-16 (lot 0)** : réservé au rôle élève ; la commande d'un autre élève répond `404`, avant tout appel au prestataire.

### ~~D-04 — Tableau de bord Hangfire public, liens sensibles dans la file~~

**Gravité : critique — prise de contrôle de compte.**

`app.UseHangfireDashboard("/hangfire")` est monté **avant** `UseRouting`,
`UseAuthentication` et `UseAuthorization` ([`Program.cs`](../../RestaurantHandler/Program.cs)) : aucun
filtre ne s'applique. Depuis le passage de Kafka à MailKit, les e-mails transitent par des
tâches dont les arguments sont persistés en clair dans `hangfire.job` et affichés dans le
tableau de bord : adresses, sujets et **corps HTML complets**, donc les **liens de
confirmation et de réinitialisation de mot de passe**.

**Correction :**

1. un `IDashboardAuthorizationFilter` exigeant un utilisateur authentifié et le rôle
   `Admin`, passé par `DashboardOptions.Authorization` ;
2. **et** `UseHangfireDashboard` déplacé après `UseAuthentication` / `UseAuthorization` —
   sinon `HttpContext.User` est encore vide quand le filtre s'exécute ;
3. *(D-04 bis)* ne plus faire transiter le corps rendu : passer le destinataire, le type
   d'e-mail et les identifiants métier, et faire le rendu RazorLight **dans**
   `MailService.DeliverAsync`. Cela allège aussi `hangfire.job` — indispensable le jour où
   une facture PDF partirait en pièce jointe.

> **Corrigé en partie le 2026-09-16 (lot 0)** : points 1 et 2 faits (`HangfireAdminAuthorizationFilter`, montage après l'authentification). Le tableau de bord n'est plus joignable depuis un navigateur tant que la session par cookie du [lot 7](01-roadmap-refactoring.md#lot-7--contrat-dapi) n'existe pas. **Le point 3 (D-04 bis) reste ouvert** et relève du lot 6.

### ~~D-05 — Secrets avec valeurs de repli en dur~~

**Gravité : critique.**

[`EnvironmentVariables`](../../RestaurantHandler/Utilities/EnvironmentVariables.cs) fournit une valeur
par défaut à `JWT_KEY`, `DB_PASSWORD`, `MINIO_ACCESSKEY` / `MINIO_SECRETKEY`,
`ADMIN_PASSWORD` et `SUPER_ADMIN_PASSWORD`. Une configuration incomplète démarre
silencieusement avec des secrets publiés dans le dépôt — une clé JWT connue permet de forger
un jeton administrateur.

**Correction :** aucune valeur de repli sur un secret ; contrôle au démarrage qui nomme la
variable manquante.

> **Corrigé le 2026-09-16 (lot 0)** : `EnvironmentVariables.ValidateRequired` arrête le démarrage en nommant chaque variable manquante ; les clés MinIO n'ont plus de repli et sont exigées au premier usage. Les fichiers Docker Compose ne transmettaient **pas** `ADMIN_EMAIL`, `ADMIN_PASSWORD` ni `SUPER_ADMIN_PASSWORD` : les comptes d'administration d'un environnement déployé ont vraisemblablement été créés avec les mots de passe de repli publiés. L'alimentation ne crée un compte que s'il est absent : **changer ces mots de passe à la main** sur chaque environnement.

### ~~D-06 — Comptes bannis ou non activés acceptés à la connexion~~

**Gravité : critique.**

`AuthService.Login` et `AuthService.UpdateRefreshToken` ne vérifient ni `StatusId` ni
`EmailConfirmed` ([`SignInService`](../../RestaurantHandler/Services/Auth/SignInService.cs)). Seule la connexion
Google refuse un compte banni. Le bannissement (`SetUserAccountStatusAsync`) n'invalide
aucun jeton de renouvellement.

**Correction :** contrôle commun à toutes les voies d'émission de session ; révocation des
jetons au bannissement.

> **Corrigé le 2026-09-16 (lot 0)** : garde commune `AuthService.RefuseSignIn` sur la connexion, la connexion Google et le renouvellement ; le bannissement supprime les jetons de renouvellement. Le jeton d'accès déjà émis reste valable jusqu'à son expiration (révocation immédiate : lot 6).

### ~~D-07 — Réservations et conversations lisibles et modifiables par autrui~~

**Gravité : élevée.**

- `GET /Slots/reservation/{id}` renvoie le détail à tout utilisateur connecté ; le code de
  présence n'est masqué que pour le rôle professeur.
- `GET /Slots/conversation/{id}` : `ConversationService.GetConversationByReservationIdAsync`
  ne reçoit pas l'identité de l'appelant.
- `ChatHub.SendChatByUserEmail` → `ConversationService.SendMessageAsync` enregistre un
  message dont l'expéditeur (`SenderId`) vient **de la requête**, sans contrôle de
  participation.

**Correction :** contrôle de participation (élève ou professeur de la réservation, ou
administrateur en lecture journalisée) ; expéditeur lu dans la session ; `404` pour un
non-participant.

> **Corrigé le 2026-09-16 (lot 0)** : `ReservationAccess.IsParticipantAsync` ; lecture administrateur journalisée ; le code de présence n'est plus rendu qu'à l'élève.

### ~~D-08 — Modification du créneau d'un autre professeur~~

**Gravité : élevée.**

`SlotsService` recherche le créneau à modifier par son identifiant seul : ni propriété ni
réservation vivante ne sont vérifiées ([`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs)).

**Correction :** mêmes gardes que la suppression, qui les applique déjà.

> **Corrigé le 2026-09-16 (lot 0)** : gardes de la suppression appliquées, créneau d'autrui → `404`. Écart voisin corrigé en même temps : `SlotUpdate.UpdateSlot` recopiait le `TeacherId` de la requête, ce qui permettait de réaffecter un créneau à un autre professeur.

### ~~D-09 — Verrouillage de compte inopérant~~

**Gravité : élevée.**

Le verrouillage est configuré (5 essais, 2 minutes), mais `Login` appelle
`UserManager.CheckPasswordAsync`, qui ne compte pas les échecs. S'y ajoutent une énumération
de comptes (« L'utilisateur n'existe pas » en `404` à la connexion et à la réinitialisation,
réponse différente au mot de passe oublié) et une comparaison sensible à la casse de
l'adresse.

**Correction :** `SignInManager.CheckPasswordSignInAsync(…, lockoutOnFailure: true)`, message
unique, normalisation de l'adresse.

> **Corrigé le 2026-09-16 (lot 0)** : verrouillage effectif, adresse normalisée, message unique à la connexion, au mot de passe oublié et à la réinitialisation.

### ~~D-10 — Comptes d'administration en collision~~

**Gravité : élevée.**

`SUPER_ADMIN_EMAIL` lit la variable `ADMIN_EMAIL`. Dès qu'elle est définie, les deux comptes
créés au démarrage partagent la même adresse.

**Correction :** variable `SUPER_ADMIN_EMAIL` distincte, vérifiée au démarrage.

> **Corrigé le 2026-09-16 (lot 0)** : `SUPER_ADMIN_EMAIL` lit sa propre variable ; deux adresses identiques empêchent le démarrage.

### ~~D-11 — Méthodes du hub SignalR appelables par tout client~~

**Gravité : élevée.**

Toutes les méthodes publiques de [`ChatHub`](../../RestaurantHandler/Controllers/ChatHub.cs) sont
invocables par un client connecté : `SendMessageByUserEmail` (événement arbitraire vers un
utilisateur désigné par son e-mail), `SendMessageToAll` (diffusion), `SendMessageToGroup`,
`AddToGroup` (adhésion libre). Un utilisateur peut fabriquer de fausses notifications chez un
autre.

**Correction :** ces envois passent par `IHubContext` côté serveur ; le hub n'expose plus
que l'indicateur de saisie, avec contrôle de participation.

> **Corrigé le 2026-09-16 (lot 0)** : diffusion et groupes supprimés. `SendMessageByUserEmail` (indicateur de saisie) et `SendChatByUserEmail`, encore appelés par le front, sont conservés mais ne relaient qu'entre les deux participants de la réservation, avec l'expéditeur de la session.

### ~~D-12 — Portefeuille lisible par autrui~~

**Gravité : élevée** — expose le chiffre d'affaires de n'importe quel professeur.

`POST /TeacherWallets/teacher/{teacherId}/transactions` est anonyme ; le solde et le détail
d'une écriture sont ouverts à tout utilisateur connecté
([`TeacherWalletService`](../../RestaurantHandler/Services/TeacherWalletService.cs)).

**Correction :** réservé au professeur concerné (lu dans la session) et aux administrateurs.

> **Corrigé le 2026-09-16 (lot 2)** : rôle `Teacher` ou administration exigé ; le portefeuille d'un autre professeur répond `404` (solde, écritures, détail d'une écriture).

### ~~D-13 — Opérations sans contrôle de rôle~~

**Gravité : moyenne à élevée selon le service.**

Aucun attribut d'autorisation sur `ProductController`, `AddressesController`,
`TypeSlotsController`, `LanguagesController`, `FileController`, ni sur la modification et la
suppression dans `FormationsController`. Il n'existe **aucune politique de repli** : une
opération sans attribut est ouverte. Le contrôle est parfois fait dans le service, parfois
non (types de créneau, langues). `POST /Auth/upload-avatar` est `[AllowAnonymous]` alors que
le service exige un utilisateur. `POST /Auth/dev` est un point d'accès vide.

**Correction :** politique de repli, attributs sur chaque opération, suppression de
`POST /Auth/dev`. Détail : [rôles & permissions](../20-specs-fonctionnelles/03-roles-permissions.md#écarts-dans-le-code-actuel).

> **Corrigé le 2026-09-16 (lot 2)** : politique de repli exigeant une session ; `[AllowAnonymous]` sur la seule liste publique, vérifiée par un test qui parcourt toutes les routes ; rôles posés sur chaque écriture ; `FileController`, `IFileService` et `POST /Auth/dev` supprimés. Les types que le front générait grâce à `POST /Auth/dev` (`GenApi`) restent publiés dans le schéma OpenAPI par `SharedSchemasDocumentFilter`. `POST /Slots/student` était anonyme : réservé à `Student`. Les routes `Dev/seed-*` exigent en plus un administrateur. Les refus de propriété répondent `404` au lieu de `403` (produit, créneau, validation de réservation, litige, avis). Effet de bord voulu : une URL inconnue répond `401` à un anonyme, ce qui ne révèle plus quelles routes existent.

### ~~D-34 — Suppression de formation et modification d'adresse sans propriété~~

**Gravité : élevée.**

`FormationsService.DeleteFormationAsync` archivait la formation désignée par son seul
identifiant, et `AddressesService.UpdateAddressAsync` modifiait l'adresse désignée sans
vérifier son titulaire. Tout utilisateur connecté pouvait effacer les formations d'un
professeur ou réécrire l'adresse d'un autre compte.

**Correction :** ressource recherchée parmi celles de l'utilisateur de la session.

> **Corrigé le 2026-09-16 (lot 2)** : relevé en cours de lot ; une ressource d'autrui répond `404` et reste intacte. Le profil élève (`GET /Students/profile/{id}`, `GET /Students/user/{id}`), lisible par tout compte connecté, n'est plus lisible que par l'élève, un professeur qui a une réservation avec lui et l'administration.

### D-22 — Injection HTML dans l'e-mail de contact

**Gravité : moyenne.**

`UserService.SendMessageToTeacherAsync` insère le HTML saisi par l'élève **tel quel** dans un
e-mail envoyé depuis l'adresse de la plateforme, sans limite de fréquence. Plus
généralement, aucun texte enrichi n'est nettoyé (descriptions, messages, avis). Le suivi de
cours ([E20](../10-user-stories/E20-suivi-de-cours.md)) ne doit pas l'aggraver : ses
descriptions de cartes sont nettoyées dès leur première version
([ADR-0014](../40-decisions/ADR-0014-tableaux-de-suivi.md)), avec la bibliothèque qui servira
ensuite aux autres champs. Depuis le lot 9, cette bibliothèque existe :
[`RichTextSanitizer`](../../RestaurantHandler/Utilities/RichTextSanitizer.cs). Les descriptions de
formation et de cours (produits) y passent aussi, plafonnées respectivement à
`FORMATION_DESCRIPTION_MAX_LENGTH` et `PRODUCT_DESCRIPTION_MAX_LENGTH` caractères de texte ;
reste à l'appliquer aux descriptions de profil, aux messages et à l'e-mail de contact.

**Correction :** nettoyage à l'écriture par une bibliothèque dédiée, gabarit Razor pour
l'e-mail, plafond par couple élève-professeur.

### D-23 — Surface exposée

**Gravité : moyenne.**

- Description OpenAPI publiée dans tous les environnements ; interface Swagger publiée aussi
  quand `ALLOW_DEMO_SEED=true`.
- Origines CORS de développement et du réseau local autorisées avec identifiants ; entrées
  mal formées (`$"api.${…}"`) ([`CorsHelper`](../../RestaurantHandler/Utilities/CorsHelper.cs)).
- Aucune limitation de débit, aucun en-tête de sécurité applicatif.
- `UseForwardedHeaders` sans proxy connu : derrière un proxy en conteneur, l'adresse et le
  schéma d'origine sont perdus.
- Plafond de requête à 200 Mo pour toute l'API.

### ~~D-33 — Jeton de réinitialisation renvoyé par le mot de passe oublié~~

**Gravité : critique — prise de contrôle de compte.**

`POST /Auth/forgot-password`, anonyme, renvoyait dans sa réponse le `ResetToken` généré pour
l'adresse fournie, avec l'identifiant du compte. N'importe qui pouvait donc réinitialiser le
mot de passe de n'importe quel compte, administrateurs compris, sans accès à sa messagerie. Le
front n'utilisait pas ce champ : il lit le jeton dans le lien reçu par e-mail.

**Correction :** le jeton ne quitte le serveur que par l'e-mail ; réponse identique que
l'adresse existe ou non.

> **Corrigé le 2026-09-16 (lot 0)** : `Data` vaut toujours `null` ; un échec d'envoi est
> journalisé, pas exposé.

## 2. Règles métier non tenues

> **Corrigé le 2026-09-16 (lot 4)** : D-14, D-16, D-18, D-19 et D-25, ainsi que la partie
> « flux sans paiement » et « unicité » de D-24. Reste de D-24 : le motif en liste (lot 6).
>
> **Corrigé le 2026-09-17 (lot 3)** : D-15, par
> [`DisputeResolutionPolicy`](../../RestaurantHandler/Domain/Policies/DisputeResolutionPolicy.cs) et
> [ADR-0013](../40-decisions/ADR-0013-litige-partiel-a-charge-du-prof.md).

| Id | Gravité | Écart | Où | Référence |
|---|---|---|---|---|
| ~~D-14~~ | élevée | litige pro professeur ou partiel → `DONE` au lieu de `PAYABLE` ; `PAYABLE` jamais posé ; versement sélectionné par liste d'exclusion | `DisputeService`, `JobChron` | [machines à états](../20-specs-fonctionnelles/02-machines-etats.md#état-dans-le-code) |
| ~~D-15~~ | moyenne | calcul de l'issue partielle différent de la spec, et non isolé en politique | `DisputeService` | [Q11](02-questions-ouvertes.md#q11--remboursement-partiel-dun-litige--qui-porte-la-commission-) |
| ~~D-16~~ | moyenne | expirations par **suppression physique** des réservations, sans historique ; `CleanSlots` supprime physiquement des créneaux porteurs d'une réservation en attente, compare le statut par son nom, en heure locale | `JobChron`, `Scheduler.cs` | [tâches planifiées](../30-specs-techniques/06-jobs-planifies.md#état-dans-le-code) |
| ~~D-18~~ | moyenne | réservation : créneau non vérifié « à venir », occupation limitée à deux statuts, réservation de son propre créneau possible, contrôle « compte actif » commenté | `ReservationService` | [R1, R5](../20-specs-fonctionnelles/01-regles-metier.md#4-règles-de-disponibilité-et-de-réservation) |
| ~~D-19~~ | élevée | avis : liste d'exclusion au lieu d'exiger `DONE`, avec `ACCOUNT_PENDING` à la place de `RESERVATION_PENDING` | `ReviewService` | [I12](../20-specs-fonctionnelles/01-regles-metier.md#i12--un-avis-est-adossé-à-une-réservation-réellement-passée) |
| D-24 | faible | ~~litige ouvrable sur une réservation du flux sans paiement ; pas d'unicité en base~~ ; motif en texte libre | `DisputeService`, `MainContext` | [US-E11-01](../10-user-stories/E11-litiges.md) |
| ~~D-25~~ | moyenne | code de présence généré à la réservation, sans fenêtre de saisie ; constantes en dur | `ReservationService` | [ADR-0012](../40-decisions/ADR-0012-code-de-presence.md) |
| D-26 | faible | compteur d'en-tête : réservations au lieu de commandes | `ReservationService` | [US-E06-03](../10-user-stories/E06-reservation-commande.md) |
| D-27 | faible | constantes absentes : `MESSAGE_MAX_LENGTH`, `CONTACT_*`, `DISPUTE_RESOLUTION_WINDOW`, `DISPUTE_ESCALATION_DELAY`, `REVIEW_COMMENT_MAX_LENGTH` | `HardCode` | [règles métier](../20-specs-fonctionnelles/01-regles-metier.md#1-constantes-de-plateforme) |

## 3. Architecture

### ~~D-17 — Gestion des erreurs~~

**Gravité : moyenne.**

Aucun gestionnaire global ni `ProblemDetails`. Les services mélangent `Response<T>` avec
statut, `throw new Exception(…)` générique (annulation, litige, retrait de réservation) et
`catch` qui renvoient `$"Erreur: {ex.Message}"` au client. Un statut `40` (au lieu de `400`)
est renvoyé par l'envoi d'avatar.

**Correction :** exceptions métier typées traduites en `409` / `400`, gestionnaire global,
aucun message d'exception en réponse.

> **Corrigé le 2026-09-17 (lot 3)** : gestionnaire global `ProblemDetails`
> ([`Infrastructure/Errors`](../../RestaurantHandler/Infrastructure/Errors)), exceptions métier typées
> (`400` / `404` / `409`), plus aucun `catch` qui renvoie le message d'une exception. Le statut
> `40` de l'envoi d'avatar avait déjà disparu au lot 2. Reste, pour le lot 7 : des contrôleurs
> renvoient `Ok(response)` quel que soit `response.Status`.

### ~~Services obèses~~

`UserService` 1 611 lignes, `ReservationService` 1 454, `PaymentService` 1 333,
`OrderService` 1 308. Intestables, impossibles à relire, point d'entrée de la duplication.
Les services reçoivent `ClaimsPrincipal`, ce qui les lie au web.

**Correction :** [lot 3](01-roadmap-refactoring.md#lot-3--structure), règle des 800 lignes
([ADR-0001](../40-decisions/ADR-0001-archi-backend.md)).

> **Corrigé le 2026-09-17 (lot 3)** : services scindés par responsabilité
> ([architecture](../30-specs-techniques/01-architecture.md#taille-des-services)) ; aucun ne
> reçoit plus la session HTTP.

### Temps réel limité à une seule instance

`ConnectionManager` associe connexion et e-mail **en mémoire du processus**. L'API ne peut pas
tourner sur plusieurs instances sans perdre des poussées. Le JWT n'est pas lu depuis la
chaîne de requête, ce qui rend le transport WebSocket depuis un navigateur douteux.

**Statut : ouverte, sciemment** — le temps réel n'est jamais le seul canal. Adresser par
identifiant de compte (lot 7) rend le passage à un relais trivial le jour venu.

### ~~Trois piles de stockage~~

`S3StorageService`, `MinioService`, `CloudflareR2Service` coexistent, souvent deux dans le
même service. Voir [ADR-0008](../40-decisions/ADR-0008-stockage-objet-unifie.md) et le
[lot 5](01-roadmap-refactoring.md#lot-5--stockage-unifié).

> **Corrigé le 2026-09-17 (lot 5)** : une seule abstraction, `IStorageService`, sur deux
> compartiments R2 (public / privé). Factures servies par lien signé ; aucune reprise des
> anciens fichiers.
>
> **Révisé le 2026-09-28** : MinIO seul, un compartiment privé, tout (avatars compris) servi
> par lien signé, derrière une seule classe `StorageService` (ADR-0008, révision).

### Aucune stratégie de test

Aucun projet de test. Voir [stratégie de tests](../30-specs-techniques/09-strategie-tests.md).

## 4. Exploitation et configuration

| Id | Gravité | Défaut | Correction |
|---|---|---|---|
| D-20 | élevée | intégration continue sans compilation séparée, tests ni analyse ; déploiement de `latest` au lieu de l'étiquette construite | chaîne bloquante, déploiement par étiquette |
| D-28 | moyenne | migrations appliquées au démarrage, en production aussi | étape de déploiement distincte |
| D-29 | moyenne | journalisation par `Console.WriteLine` mêlée à `ILogger`, sans corrélation ; aucun point de santé | journalisation structurée, `/health/live` et `/health/ready` |
| D-30 | faible | `SecureSocketOptions.StartTls` strict fait échouer un Mailpit sans certificat, de façon indirecte (5 réessais puis échec) | **documenter** Mailpit avec certificat ([exploitation](../30-specs-techniques/08-exploitation.md#serveur-de-mails-de-test)) ; **ne pas** passer à `StartTlsWhenAvailable`. Alternative : `SMTP_ALLOW_INSECURE`, faux par défaut, avertissement bruyant au démarrage |
| D-31 | faible | `.env.test.example` laisse `SMTP_*` vides alors que l'API envoie désormais réellement les e-mails | exemple complété |
| D-32 | faible | compositions Docker rangées dans `RestaurantHandler/Utilities/`, sans stockage objet ni serveur d'e-mails | déplacées à la racine, complétées |

## 5. Code mort et hygiène

### D-21 — À supprimer ou corriger

> **Corrigé le 2026-09-17 (lot 3)**, sauf les deux lignes non barrées.

| Élément | État |
|---|---|
| ~~`Services/TeacherService.cs`~~ | supprimé |
| ~~`Controllers/FileController.cs`~~ | supprimé au lot 2 |
| ~~`MailService.SendReminder` et `Templates/ReminderTemplate.cshtml`~~ | supprimés |
| ~~`POST /Auth/dev`~~ | supprimé au lot 2. `GenApi.cs` est conservé : il publie les énumérations partagées dans le document OpenAPI |
| ~~`DbSet<Profile>` commenté dans `MainContext`~~ | supprimé |
| ~~Caractères corrompus~~ | accents retapés dans 15 fichiers |
| ~~`STATUS_ORDER_REFUNDED_PARTIALY`~~ | renommée `STATUS_ORDER_REFUNDED_PARTIALLY` |
| ~~`DEBT_TRANSACTION_TYPES`~~ | construit depuis `TYPE_TEACHER_TRANSACTION_*` |
| ~~`BonProf.csproj`~~ | chemins Windows et `Newtonsoft.Json` retirés |
| `Services/Interfaces/` | deux interfaces seulement ; les autres services sont concrets (`ITokenService`, inutilisée, supprimée) |
| ~~`DateTimeOffset.Now` / `DateTime.Now`~~ | remplacés par `UtcNow` |
| `notes.md` | commande de restauration visant la base d'un autre projet (reprise, généralisée, dans [exploitation](../30-specs-techniques/08-exploitation.md#restaurer-une-sauvegarde)) |

### Documentation

| Élément | Décision |
|---|---|
| `userStory.md`, `TODO.md` | intégrés dans ces specs, supprimés |
| Six fichiers vides de `RestaurantHandler/Docs/` | supprimés |
| `RestaurantHandler/Docs/GendersAndRoles*.md` | conservés, à relire : antérieurs au passage en `TranslatableModelOption` |
| `.claude/CLAUDE.md` | à réaligner : il cite `ScheduleJobOrderTable`, retiré depuis, et des numéros de ligne périmés |

## 6. Ce qui est bon et doit le rester

Ces briques encodent des invariants durement acquis. Le refactoring les déplace sans les
réécrire, avec leurs commentaires qui expliquent *pourquoi* :

| Brique | Ce qu'elle encode |
|---|---|
| [`CancellationPolicy`](../../RestaurantHandler/Domain/Policies/CancellationPolicy.cs) | source unique de « qui paie quoi », partagée exécution et prévisualisation |
| [`ReservationStatusExtensions`](../../RestaurantHandler/Utilities/ReservationStatusExtensions.cs) | seule voie de changement d'état, avec journal d'audit |
| [`TeacherVerificationHelper`](../../RestaurantHandler/Utilities/TeacherVerificationHelper.cs) | l'état vérifié n'est jamais posé à la main |
| [`TeacherAvailability`](../../RestaurantHandler/Domain/Policies/TeacherAvailability.cs) | une seule définition pour le filtre, le tri et le compteur |
| [`TableStateUtilities`](../../RestaurantHandler/Utilities/TableStateUtilities.cs) | contrat unique de pagination, tri et filtre |
| [`MonthRange`](../../RestaurantHandler/Domain/Policies/MonthRange.cs) | périodes en heure de plateforme |
| [`Csv`](../../RestaurantHandler/Utilities/Csv.cs) | format réellement lisible par un tableur français |
| [`SiretAttribute`](../../RestaurantHandler/Utilities/SiretAttribute.cs) | clé de Luhn et exception La Poste |
| [`HeoHelper`](../../RestaurantHandler/Utilities/HeoHelper.cs) | boîte englobante puis distance exacte |
| [`StorageService`](../../RestaurantHandler/Integrations/Storage/StorageService.cs) | validation par signature, clés déterministes, tout par lien signé |

Ainsi que **toutes les décisions d'argent** : commande mono-professeur, idempotence dérivée de
données stables, versement par réservation, figement avant appel, dette par compensation,
fenêtre de litige bornée par le délai de versement. Elles ont été payées en incidents ; elles
restent.
