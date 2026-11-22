# Contrat d'API

> Statut : **Stable** · Autorisations : [rôles & permissions](../20-specs-fonctionnelles/03-roles-permissions.md)
>
> Trois parties : les **points d'accès actuels**, relevés dans le code ; les **conventions et
> points d'accès cibles** ; la **migration** de l'un vers l'autre.

## Points d'accès actuels

Relevé au 2026-09-16 à partir des attributs de
[`RestaurantHandler/Controllers`](../../RestaurantHandler/Controllers/) : 143 actions dans 25 contrôleurs, plus le
concentrateur SignalR et `GET /version` (mis à jour après le lot 2 ; le lot 0 a supprimé
`POST /Payment/connect/transfer`, le lot 2 `FilesController` et `POST /Auth/dev`). La colonne
*Accès* reflète l'attribut effectif (méthode, sinon classe) :

- *anonyme* — `[AllowAnonymous]` ;
- *auth* — `[Authorize]` sans rôle ;
- `Rôle` — `[Authorize(Roles = …)]` ;
- *(propriétaire)* — le service vérifie en plus la propriété ; une ressource d'autrui répond
  `404`.

Depuis le lot 2, une politique de repli exige une session : il n'existe plus d'opération
« sans attribut ». Les lignes *anonyme* forment la liste publique, vérifiée par
`AnonymousRouteSweepTests`. Une URL inconnue répond `401` à un anonyme.

#### `AddressesController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Addresses` | auth | `GetAddressesByUserId` |
| POST | `/Addresses` | auth | `CreateAddress` |
| PUT | `/Addresses` | auth *(propriétaire)* | `UpdateAddress` |
| DELETE | `/Addresses/{id:guid}` | auth *(propriétaire)* | `DeleteAddress` |

#### `ArticlesController`

Articles scientifiques ([ADR-0018](../40-decisions/ADR-0018-articles-scientifiques.md)). Un `Editor` n'agit que sur ses propres articles (404 sinon) et signe de son nom ([ADR-0025](../40-decisions/ADR-0025-role-editeur.md)).

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Articles/published` | anonyme | `GetPublished` (`ArticleQuery` : pagination, `language`, `tag`, `search`) |
| GET | `/Articles/slug/{slug}` | anonyme | `GetBySlug` (404 si brouillon) |
| GET | `/Articles/tags` | anonyme | `GetTags` (`?language=`) |
| GET | `/Articles/sitemap` | anonyme | `GetSitemap` |
| POST | `/Articles/admin/all` | `Admin, SuperAdmin` | `GetAdminList` |
| POST | `/Articles/mine` | `Editor` | `GetMine` (articles de l'appelant, tous statuts) |
| GET | `/Articles/{id:guid}` | `Admin, SuperAdmin, Editor` | `GetById` |
| POST | `/Articles` | `Admin, SuperAdmin, Editor` | `Create` |
| PUT | `/Articles/{id:guid}` | `Admin, SuperAdmin, Editor` | `Update` |
| PATCH | `/Articles/{id:guid}/publish` | `Admin, SuperAdmin, Editor` | `Publish` |
| PATCH | `/Articles/{id:guid}/unpublish` | `Admin, SuperAdmin, Editor` | `Unpublish` |
| DELETE | `/Articles/{id:guid}` | `Admin, SuperAdmin, Editor` | `Archive` |
| POST | `/Articles/images` | `Admin, SuperAdmin, Editor` | `UploadImage` (multipart, `Cover`) |
| POST | `/Articles/{id:guid}/comments/list` | anonyme | `GetComments` |
| POST | `/Articles/{id:guid}/comments` | connecté | `AddComment` |
| DELETE | `/Articles/comments/{commentId:guid}` | auteur ou `Admin, SuperAdmin` | `DeleteComment` |
| PATCH | `/Articles/comments/{commentId:guid}/hide` | `Admin, SuperAdmin` | `HideComment` |

#### `AuthController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Auth/register` | anonyme | `Register` |
| POST | `/Auth/login` | anonyme | `Login` |
| POST | `/Auth/google` | anonyme | `GoogleLogin` |
| GET | `/Auth/email-confirmation` | anonyme | `EmailConfirmation` |
| GET | `/Auth/my-informations` | auth | `GetMyInformations` |
| POST | `/Auth/forgot-password` | anonyme | `ForgotPassword` |
| POST | `/Auth/reset-password` | anonyme | `ChangePassword` |
| GET | `/Auth/refresh-token` | anonyme | `UpdateRefreshToken` |
| GET | `/Auth/logout` | anonyme | `Logout` |
| POST | `/Auth/upload-avatar` | auth | `OnPostUploadAsync` |

#### `CategoryCursusController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/CategoryCursus/all` | anonyme | `GetAllCategoryCursus` |
| GET | `/CategoryCursus/{id:guid}` | anonyme | `GetCategoryCursusById` |
| POST | `/CategoryCursus` | `Admin,SuperAdmin` | `CreateCategoryCursus` |
| PUT | `/CategoryCursus/{id:guid}` | `Admin,SuperAdmin` | `UpdateCategoryCursus` |
| DELETE | `/CategoryCursus/{id:guid}` | `Admin,SuperAdmin` | `DeleteCategoryCursus` |
| PATCH | `/CategoryCursus/{id:guid}/unarchive` | `Admin,SuperAdmin` | `UnarchiveCategoryCursus` |

#### `ChatHub`

_Concentrateur SignalR, monté sur `/chathub` : aucune action HTTP._ Méthodes invocables par le
client depuis le lot 0 : `Ping`, `SendMessageByUserEmail` (type `Writing` seulement, entre les
deux participants d'une réservation) et `SendChatByUserEmail` (participation exigée,
expéditeur lu dans la session).

#### `DevController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Dev/seed-users` | `Admin,SuperAdmin` *(et environnement autorisé)* | `SeedUsers` |
| POST | `/Dev/seed-slots` | `Admin,SuperAdmin` *(et environnement autorisé)* | `SeedSlots` |

#### `DisputeController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Dispute/statuses` | auth | `GetAllDisputeStatuses` |
| GET | `/Dispute/outcomes` | auth | `GetAllDisputeOutcomes` |
| POST | `/Dispute/student/paginated` | `Student` | `GetDisputesByStudent` |
| POST | `/Dispute/teacher/paginated` | `Teacher` | `GetDisputesByTeacher` |
| POST | `/Dispute/admin/paginated` | `Admin, SuperAdmin` | `GetDisputesByAdmin` |
| GET | `/Dispute/{disputeId:guid}` | auth *(participant ou administrateur)* | `GetDisputeById` |
| POST | `/Dispute/student/open` | `Student` | `OpenDispute` |
| POST | `/Dispute/admin/close` | `Admin, SuperAdmin` | `CloseDispute` |

#### `FavoritesController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Favorites/add` | `Student` | `AddFavorite` |
| DELETE | `/Favorites/remove/{teacherId:guid}` | `Student` | `RemoveFavorite` |
| GET | `/Favorites/is-favorite/{teacherId:guid}` | `Student` | `IsFavorite` |

#### `FormationsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Formations/all` | anonyme | `GetAllFormations` |
| GET | `/Formations/{id:guid}` | anonyme | `GetFormationById` |
| GET | `/Formations/teacher/{id:guid}` | anonyme | `GetFormationsByUserId` |
| POST | `/Formations` | `Teacher` | `CreateFormation` |
| PUT | `/Formations` | `Teacher` *(propriétaire)* | `UpdateFormation` |
| DELETE | `/Formations/{id:guid}` | `Teacher` *(propriétaire)* | `DeleteFormation` |

#### `GendersController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Genders/all` | anonyme | `GetAllGenders` |

#### `HelpController`

Centre d'aide ([ADR-0019](../40-decisions/ADR-0019-centre-aide.md)).

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Help/published` | anonyme | `GetPublished` (`?language=`, toutes les fiches publiées) |
| POST | `/Help/search` | anonyme | `Search` (`HelpSearchQuery` : `q`, `language`, `audience`) |
| GET | `/Help/slug/{slug}` | anonyme | `GetBySlug` (404 si brouillon, fiches liées incluses) |
| GET | `/Help/sitemap` | anonyme | `GetSitemap` |
| POST | `/Help/admin/all` | `Admin, SuperAdmin` | `GetAdminList` |
| GET | `/Help/{id:guid}` | `Admin, SuperAdmin` | `GetById` |
| POST | `/Help` | `Admin, SuperAdmin` | `Create` |
| PUT | `/Help/{id:guid}` | `Admin, SuperAdmin` | `Update` |
| PATCH | `/Help/{id:guid}/publish` | `Admin, SuperAdmin` | `Publish` |
| PATCH | `/Help/{id:guid}/unpublish` | `Admin, SuperAdmin` | `Unpublish` |
| DELETE | `/Help/{id:guid}` | `Admin, SuperAdmin` | `Archive` |

#### `LanguagesController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Languages/all` | anonyme | `GetAllLanguages` |
| GET | `/Languages/user` | auth | `GetLanguagesByUserId` |
| POST | `/Languages/create` | `Admin,SuperAdmin` | `CreateLanguage` |
| PUT | `/Languages/update/{id:guid}` | `Admin,SuperAdmin` | `UpdateLanguage` |
| DELETE | `/Languages/delete/{id:guid}` | `Admin,SuperAdmin` | `DeleteLanguage` |
| POST | `/Languages/teacher/update-languages` | `Teacher` | `UpdateLanguagesForUser` |

#### `LevelCursusController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/LevelCursus/all` | anonyme | `GetAllLevelCursus` |
| GET | `/LevelCursus/{id:guid}` | anonyme | `GetLevelCursusById` |
| POST | `/LevelCursus` | `Admin,SuperAdmin` | `CreateLevelCursus` |
| PUT | `/LevelCursus` | `Admin,SuperAdmin` | `UpdateLevelCursus` |
| DELETE | `/LevelCursus/{id:guid}` | `Admin,SuperAdmin` | `DeleteLevelCursus` |
| PATCH | `/LevelCursus/{id:guid}/unarchive` | `Admin,SuperAdmin` | `UnarchiveLevelCursus` |

#### `NotificationsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Notifications` | auth | `GetNotifications` |
| PUT | `/Notifications/{id:guid}/toggle-seen` | auth | `ToggleSeen` |
| PUT | `/Notifications/all-toggle-seen` | auth | `AllToggleSeen` |
| GET | `/Notifications/push/public-key` | anonyme | `GetPushPublicKey` — clé VAPID, vide si désactivé ([ADR-0021](../40-decisions/ADR-0021-notifications-natives-web-push.md)) |
| POST | `/Notifications/push/subscribe` | auth | `SubscribePush` |
| POST | `/Notifications/push/unsubscribe` | auth | `UnsubscribePush` |

#### `OrderController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Order/student/orders/{orderId:guid}/invoice` | `Student` | `GetOrderInvoice` |
| GET | `/Order/orders/{orderId:guid}` | `Student, Teacher, Admin, SuperAdmin` | `GetOrderById` |
| POST | `/Order/student/orders` | `Student` | `GetOrdersByStudent` |
| POST | `/Order/teacher/orders` | `Teacher` | `GetOrdersByTeacher` |
| GET | `/Order/statuses` | anonyme | `GetAllOrderStatuses` |
| GET | `/Order/student/active-orders` | `Student` | `GetActiveOrdersForStudent` |
| POST | `/Order/admin/orders` | `Admin, SuperAdmin` | `GetOrdersByAdmins` |
| GET | `/Order/admin/orders/{orderId:guid}/accounting` | `Admin, SuperAdmin` | `GetOrderAccounting` |
| POST | `/Order/admin/monthly-report` | `Admin, SuperAdmin` | `GetAdminMonthlyReport` |
| POST | `/Order/admin/monthly-report/export` | `Admin, SuperAdmin` | `ExportAdminMonthlyReport` |
| POST | `/Order/teacher/monthly-report` | `Teacher` | `GetTeacherMonthlyReport` |
| POST | `/Order/teacher/monthly-report/export` | `Teacher` | `ExportTeacherMonthlyReport` |

#### `PaymentController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Payment/checkout` | `Student` *(propriétaire)* | `CreateCheckout` |
| POST | `/Payment/connect/account` | `Teacher` *(professeur de la session)* | `CreateConnectedAccount` |
| POST | `/Payment/webhook` | anonyme | `Webhook` |
| POST | `/Payment/webhook/connect` | anonyme | `ConnectWebhook` |

#### `ProductController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Product/all` | anonyme | `GetAllProducts` |
| GET | `/Product/{id:guid}` | anonyme | `GetProductById` |
| GET | `/Product/level/{id:guid}` | anonyme | `GetProductsByLevelId` |
| GET | `/Product/teacher/{id:guid}` | anonyme | `GetProductsByTeacher` |
| POST | `/Product` | `Teacher` | `CreateProduct` |
| PUT | `/Product` | `Teacher` *(propriétaire)* | `UpdateProduct` |
| DELETE | `/Product/{id:guid}` | `Teacher` *(propriétaire)* | `DeleteProduct` |

#### `ReviewsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Reviews/add` | `Student` | `AddReview` |
| POST | `/Reviews/teacher/{teacherId:guid}` | anonyme | `GetTeacherReviews` |
| GET | `/Reviews/reservation/{reservationId:guid}/mine` | `Student` | `GetMyReview` |
| PATCH | `/Reviews/{reviewId:guid}/archive` | `Admin, SuperAdmin` | `ArchiveReview` |
| DELETE | `/Reviews/{reviewId:guid}` | `Admin, SuperAdmin` | `DeleteReview` |

#### `RolesController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Roles/all` | anonyme | `GetAllRoles` |

#### `SlotsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Slots/teacher/add` | `Teacher` | `AddSlotByTeacher` |
| PUT | `/Slots/teacher/update` | `Teacher` | `UpdateSlotByTeacher` |
| DELETE | `/Slots/teacher/remove/{slotId:guid}` | `Teacher` | `RemoveSlotByTeacher` |
| POST | `/Slots/teacher/my-slots` | `Teacher` | `GetSlotsByTeacher` |
| POST | `/Slots/teacher/{teacherId:guid}/available-slots` | anonyme | `GetAvailableSlotsByTeacher` |
| POST | `/Slots/student` | `Student` | `GetReservationsByStudent` |
| POST | `/Slots/student/reservations` | `Student` | `GetReservationsByStudentPaginated` |
| POST | `/Slots/teacher/reservations` | `Teacher` | `GetReservationsByTeacherPaginated` |
| POST | `/Slots/admin/reservations` | `Admin, SuperAdmin` | `GetReservationsByAdminPaginated` |
| GET | `/Slots/student/reservations/pending-count` | `Student` | `GetPendingReservationsCountByTeacher` |
| POST | `/Slots/student/book` | `Student` | `BookSlotByStudent` |
| POST | `/Slots/teacher/confirm-reservation` | `Teacher` | `ConfirmBookingByTeacher` |
| POST | `/Slots/validate-reservation` | `Teacher, Admin, SuperAdmin` | `ValidateReservation` |
| GET | `/Slots/reservation/{reservationId:guid}` | auth *(participant ou administrateur)* | `GetReservationById` |
| GET | `/Slots/conversation/{reservationId:Guid}` | auth *(participant ou administrateur)* | `GetConversationByReservationId` |
| POST | `/Slots/conversation/{reservationId:Guid}/attachments` | auth *(participant)* | `SendConversationAttachment` |
| DELETE | `/Slots/teacher/remove-reservation` | `Teacher` | `RemoveReservationByTeacher` |
| GET | `/Slots/student/reservation/{reservationId:guid}/cancellation-preview` | `Student` | `GetCancellationPreview` |
| DELETE | `/Slots/student/remove-reservation` | `Student` | `RemoveReservationByStudent` |

Fichiers du chat ([ADR-0016](../40-decisions/ADR-0016-pieces-jointes-chat.md)) : corps
`multipart/form-data` `ConversationAttachmentUpload { file, content? }` — mêmes types et même
taille que les pièces jointes des cartes, au plus `CHAT_ATTACHMENT_MAX_PER_RESERVATION` (20)
par réservation. Réponse `201` avec le `ConversationDetails` créé ; `400` pour un fichier vide
ou une légende trop longue, `413` au-delà de la taille, `415` pour un type ou une signature
refusés, `409` au-delà du plafond, `404` hors participants. Les deux parties reçoivent
l'événement `Chat`. `ConversationDetails` porte désormais `attachment?`
(`ConversationAttachmentDetails { id, fileName, mimeType, size, url, uploadedAt }`), avec un
lien signé neuf à chaque lecture du fil.

#### `StatusReservationsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/StatusReservations/all` | anonyme | `GetAllStatusReservations` |

#### `StudentsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/Students/my-profile` | auth | `GetMyProfile` |
| GET | `/Students/profile/{userId:guid}` | auth *(l'élève, un professeur d'une réservation partagée, l'administration)* | `GetStudentProfile` |
| GET | `/Students/user/{userId:guid}` | auth *(idem)* | `GetStudentByUserId` |
| PUT | `/Students/update-profile` | `Student` | `UpdateStudent` |
| POST | `/Students/contact-teacher` | `Student` | `ContactTeacher` |

#### `TeacherWalletsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/TeacherWallets/teacher/{teacherId:guid}/transactions` | `Teacher,Admin,SuperAdmin` *(propriétaire)* | `GetTransactionsByTeacher` |
| GET | `/TeacherWallets/teacher/{teacherId:guid}/balance` | `Teacher,Admin,SuperAdmin` *(propriétaire)* | `GetBalance` |
| GET | `/TeacherWallets/transactions/{id:guid}` | `Teacher,Admin,SuperAdmin` *(propriétaire)* | `GetTransactionById` |

#### `TeachersController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Teachers/all` | anonyme | `GetAllTeacherProfiles` |
| GET | `/Teachers/featured` | anonyme | `GetFeaturedTeachers` |
| GET | `/Teachers/my-profile` | auth | `GetMyTeacherProfile` |
| GET | `/Teachers/user/{userId:guid}` | anonyme | `GetTeacherProfileByUserId` |
| PUT | `/Teachers/update-profile` | `Teacher` | `UpdateTeacherProfile` |
| PUT | `/Teachers/switch-to-professional` | `Teacher` | `SwitchToProfessional` |
| GET | `/Teachers/get-documents` | `Teacher` | `GetPrivacyDocuments` |
| POST | `/Teachers/upload-document` | `Teacher` | `UploadDocument` |
| DELETE | `/Teachers/delete-document/{documentId:guid}` | `Teacher` | `DeleteDocument` |
| GET | `/Teachers/document-types` | anonyme | `GetPrivacyTypes` |
| GET | `/Teachers/document-requests` | `Teacher` | `GetDocumentRequests` |

#### `TypeAddressesController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/TypeAddresses/all` | anonyme | `GetAllTypeAddresses` |

#### `TypeSlotsController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| GET | `/TypeSlots/all` | anonyme | `GetAllTypeSlots` |
| GET | `/TypeSlots/{id:guid}` | anonyme | `GetTypeSlotById` |
| POST | `/TypeSlots` | `Admin,SuperAdmin` | `CreateTypeSlot` |
| PUT | `/TypeSlots` | `Admin,SuperAdmin` | `UpdateTypeSlot` |
| DELETE | `/TypeSlots/{id:guid}` | `Admin,SuperAdmin` | `DeleteTypeSlot` |

#### `UserManagementController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/UserManagement/teachers` | `Admin,SuperAdmin` | `GetTeachers` |
| PATCH | `/UserManagement/teachers/{userId:guid}/confirmation` | `Admin,SuperAdmin` | `SetTeacherConfirmation` |
| GET | `/UserManagement/teachers/{userId:guid}/siret-verification` | `Admin,SuperAdmin` | `GetTeacherSiretVerification` |
| PATCH | `/UserManagement/users/{userId:guid}/status` | `Admin,SuperAdmin` | `SetUserAccountStatus` |
| PATCH | `/UserManagement/users/{userId:guid}/editor` | `Admin,SuperAdmin` | `SetEditorRole` (`?enabled=`, professeur seulement, renvoie les rôles) |
| GET | `/UserManagement/teachers/{userId:guid}/documents` | `Admin,SuperAdmin` | `GetTeacherDocuments` |
| PATCH | `/UserManagement/documents/{documentId:guid}/review` | `Admin,SuperAdmin` | `ReviewDocument` |
| POST | `/UserManagement/teachers/{userId:guid}/document-requests` | `Admin,SuperAdmin` | `RequestDocument` |
| DELETE | `/UserManagement/document-requests/{requestId:guid}` | `Admin,SuperAdmin` | `CancelDocumentRequest` |
| POST | `/UserManagement/document-types` | `Admin,SuperAdmin` | `CreateDocumentType` |
| PUT | `/UserManagement/document-types/{id:guid}` | `Admin,SuperAdmin` | `UpdateDocumentType` |
| DELETE | `/UserManagement/document-types/{id:guid}` | `Admin,SuperAdmin` | `DeleteDocumentType` |

#### `UsersController`

| Verbe | Route | Accès | Action |
|---|---|---|---|
| POST | `/Users/filter` | `Admin,SuperAdmin` | `GetUsers` (`FilterUsers` : `name`, `roleIds`, `createdFrom` inclus, `createdTo` exclu, pagination) |
| GET | `/Users/{id:guid}` | `Admin,SuperAdmin` | `GetUserById` |
| POST | `/Users` | `Admin,SuperAdmin` | `CreateUser` |
| PUT | `/Users/{id:guid}` | `Admin,SuperAdmin` | `UpdateUser` |

---

## Conventions cibles

| Convention | Existant |
|---|---|
| Nommage des routes | `[controller]` en PascalCase (`/TeacherWallets`), verbes dans les URL (`/add`, `/remove`, `/all`) |
| Enveloppe de réponse | `Response<T>` (`Message`, `Status`, `Data`, `Count`) — **conforme** |
| Erreurs d'infrastructure | `ProblemDetails` depuis le lot 3 : gestionnaire global ([`BusinessExceptionHandler`](../../RestaurantHandler/Infrastructure/Errors/BusinessExceptionHandler.cs)), refus métier en `400` / `404` / `409` avec leur message, `500` sans détail hors développement. Un champ `message` reprend le titre, pour le front actuel |
| Ressource non possédée | `404` depuis le lot 2 — **conforme** |
| Pagination, tri, filtres | `CustomTableState` et [`TableStateUtilities`](../../RestaurantHandler/Utilities/TableStateUtilities.cs) — **conforme** |
| Authentification | JWT en en-tête ; pas de jeton anti-falsification |
| Énumérations | sérialisées par nom (`JsonStringEnumConverter`) — **conforme** |
| Temps réel | `/chathub` ([`ChatHub`](../../RestaurantHandler/Controllers/ChatHub.cs)), authentifié ; depuis le lot 0, le client n'invoque plus que le chat et l'indicateur de saisie, bornés aux participants de la réservation. Adressage encore par e-mail |
| Santé | seul `GET /version` existe |

### Nommage

- Routes en **minuscules**, segments au pluriel : `/teachers`, `/reservations`.
- Le verbe HTTP porte l'intention ; on évite les verbes dans l'URL sauf pour une action
  métier qui n'est pas un CRUD (`/reservations/{id}/validate`).
- Identifiants en `Guid` contraints dans la route.

### Réponses

Enveloppe uniforme pour les réponses applicatives :

```json
{ "message": "…", "status": 200, "data": { }, "count": 42 }
```

`count` n'est présent que sur les réponses paginées et porte le **total avant pagination**.

Les erreurs d'infrastructure — non authentifié, interdit, introuvable, erreur serveur —
utilisent le format normalisé `ProblemDetails`, sans trace d'exécution en production.

| Situation | Code |
|---|---|
| Succès | `200` |
| Création | `201` avec l'emplacement de la ressource |
| Entrée invalide | `400` avec le détail par champ |
| Non authentifié | `401` |
| Authentifié mais interdit | `403` |
| Ressource inexistante **ou non possédée** | `404` |
| Conflit d'état métier | `409` |
| Trop de requêtes | `429` |

> **Une ressource qui existe mais ne m'appartient pas répond `404`, pas `403`.** Un `403`
> confirmerait son existence.

### Pagination, tri, filtres

Un contrat unique, en corps de requête `POST` pour les listes complexes :

```json
{
  "first": 0,
  "rows": 25,
  "sorts": [{ "field": "dateFrom", "order": -1 }],
  "filters": { "statusId": { "value": "…", "matchMode": "equals" } },
  "search": "texte libre"
}
```

Les champs imbriqués s'expriment par chemin (`teacher/firstName`). La liste des champs
filtrables et triables est **déclarée par point d'accès** : aucun filtre arbitraire n'est
accepté sur un champ non exposé.

### Authentification

Session par **cookie `httpOnly`, `Secure`, `SameSite`**. Aucun jeton dans l'URL ni dans le
stockage du navigateur. Les requêtes mutantes portent l'en-tête **`X-XSRF-TOKEN`**, dont la
valeur est lue dans le cookie `XSRF-TOKEN` déposé sur chaque lecture — et réémis à la
connexion comme à la déconnexion, le jeton étant lié à l'identité courante. Voir
[sécurité](04-securite.md).

### Dates et montants

Dates en ISO 8601 avec décalage explicite, interprétées en UTC. Montants en nombres
décimaux à deux décimales, jamais en flottants.

### Description OpenAPI

La description est générée par le cadre et sert de source au client TypeScript. Les
énumérations sont sérialisées **par leur nom**, pas par leur valeur numérique, pour que le
client reste lisible et résistant aux réordonnancements.

### Temps réel

Un **unique concentrateur**, monté sur `/realtime`, authentifié par **la même session** que
les appels classiques : pas de second mécanisme, aucun jeton dans l'URL. Une négociation
anonyme reçoit `401`.

**Il n'y a pas de groupes.** Le destinataire d'une poussée est désigné par son identifiant de
compte, lu dans la session. C'est la forme la plus stricte de la règle « l'appartenance est
recalculée côté serveur, jamais déclarée par le client » : il n'y a rien que le client puisse
déclarer.

Événements poussés par le serveur :

| Événement | Charge utile | Destinataire |
|---|---|---|
| `message` | le message, tel que `GET /reservations/{id}/conversation` le renvoie | l'autre partie de la réservation |
| `notification` | la notification, telle que `POST /notifications/search` la renvoie | son destinataire |
| `typing` | `{ reservationId, fromUserId }` | l'autre partie de la réservation |

Seule méthode appelable par le client :

| Méthode | Argument | Effet |
|---|---|---|
| `Typing` | `reservationId` | relaie `typing` à l'autre partie. La participation est **vérifiée en base** à chaque appel ; un appelant étranger à la réservation ne produit rien, et ne l'apprend pas. |

Le temps réel **n'est jamais le seul canal** : il n'a pas d'historique, ne rejoue rien à la
reconnexion, et tout ce qu'il annonce est déjà écrit en base. Voir
[événements & notifications](../20-specs-fonctionnelles/06-evenements-notifications.md).

---

## Points d'accès cibles

Légende des accès : *anon* · `Student` · `Teacher` · `Admin` (inclut `SuperAdmin`) ·
*auth* (tout utilisateur connecté)

### Authentification — `/auth`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/auth/register` | anon | inscription élève ou professeur |
| POST | `/auth/login` | anon | connexion, ouvre la session |
| POST | `/auth/google` | anon | connexion via fournisseur externe |
| GET | `/auth/email-confirmation` | anon | activation depuis le lien reçu |
| POST | `/auth/email-confirmation` | anon | renvoi d'un lien de confirmation |
| POST | `/auth/forgot-password` | anon | envoi du lien de réinitialisation |
| POST | `/auth/reset-password` | anon | application du nouveau mot de passe |
| POST | `/auth/change-password` | auth | changement avec l'ancien mot de passe |
| GET | `/auth/me` | auth | identité, rôles et états de la session |
| POST | `/auth/refresh` | anon *(cookie)* | renouvellement de session |
| POST | `/auth/logout` | auth | invalidation côté serveur |
| POST | `/auth/avatar` | **auth** | téléversement de l'avatar |

### Professeurs — `/teachers`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/teachers/search` | anon | recherche paginée et filtrée |
| GET | `/teachers/featured` | anon | sélection mise en avant |
| GET | `/teachers/{userId}` | anon | fiche publique |
| GET | `/teachers/me` | `Teacher` | profil complet |
| PUT | `/teachers/me` | `Teacher` | mise à jour du profil |
| PUT | `/teachers/me/professional` | `Teacher` | passage en professionnel par SIRET |
| GET | `/teachers/me/documents` | `Teacher` | mes pièces justificatives |
| POST | `/teachers/me/documents` | `Teacher` | dépôt d'une pièce |
| DELETE | `/teachers/me/documents/{id}` | `Teacher` | retrait d'une pièce |
| GET | `/teachers/me/document-requests` | `Teacher` | pièces attendues |

### Élèves — `/students`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET | `/students/me` | `Student` | profil complet |
| PUT | `/students/me` | `Student` | mise à jour |
| GET | `/students/{userId}` | `Teacher` *(réservation partagée)*, `Admin` | profil restreint |
| POST | `/students/contact-teacher` | `Student` | message depuis une fiche |

### Produits — `/products`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET | `/products/{id}` | anon | détail |
| GET | `/products/teacher/{teacherId}` | anon | catalogue d'un professeur |
| GET | `/products/level/{levelId}` | anon | filtrage par niveau |
| POST | `/products` | `Teacher` | création |
| PUT | `/products/{id}` | `Teacher` *(propriétaire)* | modification |
| DELETE | `/products/{id}` | `Teacher` *(propriétaire)* | suppression logique |

### Créneaux — `/slots`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/slots/teacher/search` | `Teacher` | mes créneaux sur une période |
| POST | `/slots` | `Teacher` | création |
| PUT | `/slots/{id}` | `Teacher` *(propriétaire)* | modification |
| DELETE | `/slots/{id}` | `Teacher` *(propriétaire)* | suppression |
| POST | `/slots/teacher/{teacherId}/available` | anon | créneaux réservables |

### Réservations — `/reservations`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/reservations` | `Student` | réserver un créneau |
| POST | `/reservations/student/search` | `Student` | mes réservations |
| POST | `/reservations/teacher/search` | `Teacher` | mes réservations |
| POST | `/reservations/admin/search` | `Admin` | toutes |
| GET | `/reservations/student/pending-count` | `Student` | indicateur d'en-tête |
| GET | `/reservations/{id}` | auth *(partie prenante)* | détail et historique |
| POST | `/reservations/{id}/confirm` | `Teacher` | acceptation en flux sans paiement |
| POST | `/reservations/{id}/reject` | `Teacher` | refus en flux sans paiement |
| POST | `/reservations/{id}/validate` | `Teacher`, `Admin` | code de présence |
| GET | `/reservations/{id}/cancellation-preview` | `Student` | prévisualisation du remboursement |
| DELETE | `/reservations/{id}/student` | `Student` | annulation par l'élève |
| DELETE | `/reservations/{id}/teacher` | `Teacher` | annulation par le professeur |
| GET | `/reservations/{id}/conversation` | auth *(partie prenante)* | messages |
| POST | `/reservations/{id}/conversation` | auth *(partie prenante)* | envoi d'un message |

### Commandes — `/orders`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET | `/orders/{id}` | auth *(partie prenante)* | détail |
| POST | `/orders/student/search` | `Student` | mes achats |
| POST | `/orders/teacher/search` | `Teacher` | mes ventes |
| POST | `/orders/admin/search` | `Admin` | toutes |
| GET | `/orders/student/active` | `Student` | paniers en attente, un par professeur |
| GET | `/orders/{id}/invoice` | `Student` *(propriétaire)*, `Admin` | facture PDF |
| GET | `/orders/{id}/accounting` | `Admin` | décomposition financière |
| POST | `/orders/admin/monthly-report` | `Admin` | bilan mensuel global |
| POST | `/orders/admin/monthly-report/export` | `Admin` | export tabulaire |
| POST | `/orders/teacher/monthly-report` | `Teacher` | bilan mensuel |
| POST | `/orders/teacher/monthly-report/export` | `Teacher` | export tabulaire |

### Paiement — `/payments`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/payments/checkout` | **`Student` *(propriétaire)*** | ouvrir un paiement |
| POST | `/payments/connect/account` | **`Teacher`** | ouvrir ou reprendre l'onboarding |
| POST | `/payments/webhooks/platform` | anon *(signature)* | notifications plateforme |
| POST | `/payments/webhooks/connect` | anon *(signature)* | notifications comptes connectés |

> Le transfert manuel actuel est **supprimé** : aucun mouvement d'argent ne se déclenche à la
> main.

### Portefeuille — `/teacher-wallets`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET | `/teacher-wallets/me/balance` | `Teacher` | disponible, dette, solde, en cours |
| POST | `/teacher-wallets/me/transactions` | `Teacher` | mes écritures, paginées |
| GET | `/teacher-wallets/transactions/{id}` | `Teacher` *(propriétaire)*, `Admin` | détail |
| GET | `/teacher-wallets/{teacherId}/balance` | **`Admin`** | solde d'un professeur |

### Litiges — `/disputes`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/disputes` | `Student` | ouverture avec motif |
| POST | `/disputes/student/search` | `Student` | les miens |
| POST | `/disputes/teacher/search` | `Teacher` | les miens |
| POST | `/disputes/admin/search` | `Admin` | tous |
| GET | `/disputes/{id}` | auth *(partie prenante)* | détail |
| POST | `/disputes/{id}/accept` | `Teacher` | acceptation, remboursement immédiat |
| POST | `/disputes/{id}/resolve` | `Admin` | arbitrage avec issue et note |
| GET | `/disputes/statuses` · `/disputes/outcomes` | auth | référentiels |

### Avis — `/reviews`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/reviews` | `Student` | déposer un avis |
| POST | `/reviews/teacher/{teacherId}` | anon | avis d'un professeur, paginés |
| GET | `/reviews/reservation/{id}/mine` | `Student` | mon avis sur une réservation |
| PATCH | `/reviews/{id}/archive` | `Admin` | masquer |
| DELETE | `/reviews/{id}` | `Admin` | supprimer |

### Favoris — `/favorites`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET | `/favorites` | `Student` | mes favoris |
| POST | `/favorites/{teacherId}` | `Student` | ajouter |
| DELETE | `/favorites/{teacherId}` | `Student` | retirer |
| GET | `/favorites/{teacherId}/exists` | `Student` | état pour l'affichage |

### Profil complémentaire

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET / POST / PUT / DELETE | `/addresses[/{id}]` | **auth** *(propriétaire)* | mes adresses |
| GET | `/formations/teacher/{id}` · `/formations/{id}` | anon | consultation |
| POST / PUT / DELETE | `/formations[/{id}]` | auth *(propriétaire)* | gestion |
| GET | `/experiences/teacher/{id}` | anon | consultation |
| GET / POST / PUT / DELETE | `/experiences[/{id}]` | `Teacher` *(propriétaire)* | gestion |
| GET | `/reference/languages` | anon | référentiel |
| GET / PUT | `/languages/me` | auth | mes langues parlées |

### Notifications — `/notifications`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/notifications/search` | auth | mes notifications, filtrées |
| PUT | `/notifications/{id}/seen` | auth *(destinataire)* | basculer l'état |
| PUT | `/notifications/seen` | auth | tout marquer comme lu |

### Suivi de cours — `/boards`

Stories : [E20](../10-user-stories/E20-suivi-de-cours.md) · décision :
[ADR-0014](../40-decisions/ADR-0014-tableaux-de-suivi.md), pièces jointes :
[ADR-0015](../40-decisions/ADR-0015-pieces-jointes-cartes.md) · contrôleurs : `BoardsController`, `MyStudentsController` (routes
cibles dès leur création, avant le lot 7).

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/teachers/me/students/search` | `Teacher` | mes élèves (réservation partagée), avec le nombre de tableaux actifs |
| POST | `/boards/search` | `Teacher`, `Student` | mes tableaux — ceux que j'ai ouverts, ou ceux ouverts pour moi |
| POST | `/boards` | `Teacher` | création pour un élève, avec les trois listes par défaut |
| GET | `/boards/{id}` | `Teacher` *(propriétaire)*, `Student` *(élève du tableau)* | le tableau, ses listes et ses cartes actives, dans l'ordre |
| GET | `/boards/{id}/cards/{cardId}` | `Teacher` *(propriétaire)*, `Student` *(élève du tableau)* | une carte complète, description comprise |
| PUT | `/boards/{id}` | `Teacher` *(propriétaire)* | renommage |
| PATCH | `/boards/{id}/archive` · `/unarchive` | `Teacher` *(propriétaire)* | archivage, désarchivage |
| POST | `/boards/{id}/lists` | `Teacher` *(propriétaire)* | ajout d'une liste, en dernière position |
| PUT | `/boards/{id}/lists/{listId}` | `Teacher` *(propriétaire)* | renommage |
| PUT | `/boards/{id}/lists/order` | `Teacher` *(propriétaire)* | nouvel ordre complet des listes |
| DELETE | `/boards/{id}/lists/{listId}` | `Teacher` *(propriétaire)* | suppression d'une liste **vide** — `409` sinon |
| POST | `/boards/{id}/cards/search` | `Teacher` *(propriétaire)*, `Student` *(élève du tableau)* | cartes filtrées (importance, produit, archivées) |
| POST | `/boards/{id}/cards` | `Teacher` *(propriétaire)* | création d'une carte |
| PUT | `/boards/{id}/cards/{cardId}` | `Teacher` *(propriétaire)* | modification |
| POST | `/boards/{id}/cards/{cardId}/move` | `Teacher` *(propriétaire)* | `{ boardListId, position }` — déplacement atomique |
| PATCH | `/boards/{id}/cards/{cardId}/archive` · `/unarchive` | `Teacher` *(propriétaire)* | archivage, restauration |
| POST | `/boards/{id}/cards/{cardId}/attachments` | `Teacher` *(propriétaire)* | `multipart/form-data`, un fichier — ajout d'une pièce jointe |
| DELETE | `/boards/{id}/cards/{cardId}/attachments/{attachmentId}` | `Teacher` *(propriétaire)* | suppression définitive d'une pièce jointe |

- Toutes les ressources sont adressées **sous leur tableau** : une liste ou une carte d'un
  autre tableau répond `404`, comme un tableau d'un autre professeur.
- Un produit qui n'appartient pas au professeur, ou une réservation hors du couple du tableau,
  répond `400` avec le champ en cause ; un tableau archivé refuse toute écriture par `409`.
- `GET /boards/{id}` renvoie le tableau entier en une réponse : un tableau est borné par
  `LIST_MAX_PER_BOARD` et ses cartes actives tiennent dans un écran, la pagination n'aurait
  pas de sens. Les cartes y portent un **extrait** de la description, pas le HTML complet,
  lu par `GET /boards/{id}/cards/{cardId}`.
- Corps : `BoardCreate { studentId, title }`, `BoardUpdate` et listes `{ title }`,
  `BoardListOrder { listIds }`, `BoardCardCreate { boardListId, title, importance?, productId?,
  reservationId?, description? }`, `BoardCardUpdate` idem sans `boardListId` — remplacement
  complet : un produit ou une séance absents sont retirés. Réponses : `BoardSummary`,
  `BoardDetails` (listes et `BoardCardTile`), `BoardCardDetails`, `MyStudentDetails`.
- Pièces jointes : JPEG, PNG, WebP ou PDF reconnus par signature, au plus
  `ATTACHMENT_MAX_SIZE_BYTES` (10 Mo) et `CARD_ATTACHMENT_MAX_PER_CARD` (10) par carte.
  L'ajout répond `201` avec un `BoardCardAttachmentDetails { id, fileName, mimeType, size,
  url, uploadedAt }` ; `400` pour un fichier vide, `413` au-delà de la taille, `415` pour un
  type ou une signature refusés, `409` si la carte en porte déjà dix ou si le tableau est
  archivé ; une carte archivée répond `404` : elle est en lecture seule. La suppression
  répond `200`, comme celle d'une liste. Il n'y a pas de route de lecture propre :
  `BoardCardDetails.attachments` les liste, avec un `url` **signé** à chaque lecture
  (`SIGNED_URL_MINUTES`), et `BoardCardTile.attachmentCount` en donne le nombre sur le tableau.
- Filtres déclarés : `/teachers/me/students/search` — recherche sur le nom, tri `lastName`,
  `firstName`, `activeBoardCount` ; `/boards/search` — `archived` (professeur), `studentId`,
  `teacherId`, recherche sur le titre et les noms, tri par dernière modification ;
  `/boards/{id}/cards/search` — `importance` (liste de valeurs), `productId`, `archived`
  (professeur), recherche sur le titre.

### Administration — `/admin`

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| POST | `/admin/users/search` | `Admin` | liste des comptes |
| GET | `/admin/users/{id}` | `Admin` | détail administratif |
| POST | `/admin/users` · PUT `/admin/users/{id}` | `Admin` | création, modification |
| PATCH | `/admin/users/{id}/status` | `Admin` | activer ou bannir |
| POST | `/admin/teachers/search` | `Admin` | file des professeurs |
| GET | `/admin/teachers/{id}/certification` | `Admin` | vue d'ensemble du dossier |
| GET | `/admin/teachers/{id}/siret` | `Admin` | résultat de vérification |
| PATCH | `/admin/teachers/{id}/confirmation` | `Admin` | confirmation manuelle |
| PATCH | `/admin/documents/{id}/review` | `Admin` | valider ou refuser |
| POST | `/admin/teachers/{id}/document-requests` | `Admin` | demander une pièce |
| DELETE | `/admin/document-requests/{id}` | `Admin` | annuler une demande |
| POST / PUT / DELETE | `/admin/document-types[/{id}]` | **`SuperAdmin`** | catalogue des pièces |

### Référentiels — lecture publique

| Verbe | Route | Accès |
|---|---|---|
| GET | `/reference/genders` · `/roles` · `/account-statuses` | anon |
| GET | `/reference/reservation-statuses` · `/order-statuses` | anon |
| GET | `/reference/address-types` · `/slot-types` | anon |
| GET | `/reference/levels` · `/categories` | anon |
| POST / PUT / DELETE | `/reference/levels[/{id}]`, `/reference/categories[/{id}]`, `/reference/slot-types[/{id}]` | `Admin` |
| PATCH | `/reference/{referentiel}/{id}/unarchive` | `Admin` — désarchivage d'une valeur |

### Service

| Verbe | Route | Accès | Objet |
|---|---|---|---|
| GET | `/health/live` · `/health/ready` | anon | supervision |
| GET | `/version` | anon | version déployée |
| GET | `/sitemap.xml` · `/sitemap-teachers.xml` | anon | plan de site |

---

## Migration des routes

Chaque route actuelle et son devenir dans la cible. **Aucune ne disparaît sans décision.**
Le renommage se fait domaine par domaine ; le client front est régénéré dans la même
livraison.

| Route actuelle | Devenir |
|---|---|
| `POST /auth/register`, `/auth/login`, `/auth/google`, `/auth/forgot-password`, `/auth/reset-password` | conservées |
| `GET /auth/email-confirmation`, `/auth/refresh-token`, `/auth/logout` | conservées ; renouvellement et déconnexion passent en `POST` |
| `GET /auth/my-informations` | devient `GET /auth/me` |
| `POST /auth/upload-avatar` | devient `POST /auth/avatar`, **authentification exigée** |
| `POST /auth/dev` | **supprimée** — artefact de génération de client, remplacé par la description OpenAPI |
| `POST /teachers/all` | devient `POST /teachers/search` |
| `GET /teachers/featured`, `/teachers/user/{id}` | conservées, renommées |
| `GET /teachers/my-profile`, `PUT /teachers/update-profile`, `/teachers/switch-to-professional` | conservées sous `/teachers/me` |
| `GET /teachers/get-documents`, `POST /teachers/upload-document`, `DELETE /teachers/delete-document/{id}`, `GET /teachers/document-requests`, `/teachers/document-types` | conservées sous `/teachers/me/documents` et `/reference` |
| `GET /students/my-profile`, `/students/profile/{id}`, `/students/user/{id}`, `PUT /students/update-profile`, `POST /students/contact-teacher` | conservées ; les deux lectures de profil sont **fusionnées** en une seule, à visibilité restreinte |
| `GET /product/all` | **supprimée** — une liste globale de produits sans filtre n'a aucun usage |
| `GET /product/{id}`, `/product/level/{id}`, `/product/teacher/{id}`, `POST`/`PUT`/`DELETE /product` | conservées sous `/products`, **avec contrôle de rôle** |
| `POST /slots/teacher/add`, `PUT /slots/teacher/update`, `DELETE /slots/teacher/remove/{id}`, `POST /slots/teacher/my-slots` | conservées sous `/slots` |
| `POST /slots/teacher/{id}/available-slots` | devient `POST /slots/teacher/{id}/available` |
| `POST /slots/student` | **supprimée** — doublon anonyme de la recherche de créneaux |
| `POST /slots/student/reservations`, `/slots/teacher/reservations`, `/slots/admin/reservations` | déplacées sous `/reservations/*/search` |
| `GET /slots/student/reservations/pending-count` | déplacée sous `/reservations` |
| `POST /slots/student/book` | devient `POST /reservations` |
| `POST /slots/teacher/confirm-reservation` | scindée en `/reservations/{id}/confirm` et `/reject` |
| `POST /slots/validate-reservation` | devient `POST /reservations/{id}/validate` |
| `GET /slots/reservation/{id}`, `/slots/conversation/{id}` | déplacées sous `/reservations/{id}` |
| `GET /slots/student/reservation/{id}/cancellation-preview` | déplacée sous `/reservations/{id}` |
| `DELETE /slots/student/remove-reservation`, `/slots/teacher/remove-reservation` | deviennent `DELETE /reservations/{id}/student` et `/teacher` |
| `GET /order/orders/{id}`, `POST /order/student/orders`, `/order/teacher/orders`, `/order/admin/orders` | conservées sous `/orders` |
| `GET /order/student/active-orders` | devient `GET /orders/student/active` |
| `GET /order/student/orders/{id}/invoice`, `/order/admin/orders/{id}/accounting` | conservées sous `/orders/{id}` |
| `POST /order/admin/monthly-report[/export]`, `/order/teacher/monthly-report[/export]` | conservées |
| `GET /order/statuses` | déplacée sous `/reference/order-statuses` |
| `POST /payment/checkout` | conservée, **authentification et propriété exigées** |
| `POST /payment/connect/account` | conservée, **authentification exigée** |
| `POST /payment/connect/transfer` | **supprimée** (lot 0) — aucun transfert manuel |
| `POST /payment/webhook`, `/payment/webhook/connect` | conservées sous `/payments/webhooks/*` |
| `POST /teacherwallets/teacher/{id}/transactions` | scindée : `/me/transactions` pour le professeur, variante administrateur ; **l'accès anonyme est supprimé** |
| `GET /teacherwallets/teacher/{id}/balance`, `/transactions/{id}` | conservées avec contrôle de propriété |
| `POST /dispute/student/open` | devient `POST /disputes` |
| `POST /dispute/admin/close` | devient `POST /disputes/{id}/resolve` |
| `POST /dispute/{student,teacher,admin}/paginated`, `GET /dispute/{id}`, `/dispute/statuses`, `/dispute/outcomes` | conservées sous `/disputes` |
| — | **ajoutée** : `POST /disputes/{id}/accept` — acceptation par le professeur |
| `POST /reviews/add`, `/reviews/teacher/{id}`, `GET /reviews/reservation/{id}/mine`, `PATCH /reviews/{id}/archive`, `DELETE /reviews/{id}` | conservées |
| `POST /favorites/add`, `DELETE /favorites/remove/{id}`, `GET /favorites/is-favorite/{id}` | conservées, routes normalisées ; **ajout** de la liste des favoris |
| `GET/POST/PUT/DELETE /addresses` | conservées, **avec contrôle de rôle** |
| `GET /formations/all`, `/formations/{id}`, `/formations/teacher/{id}`, `POST`/`PUT`/`DELETE` | conservées ; la liste globale est supprimée |
| `POST /languages/all`, `GET /languages/user`, `POST /languages/create`, `PUT /languages/update/{id}`, `DELETE /languages/delete/{id}`, `POST /languages/teacher/update-languages` | consolidées : `/reference/languages` pour le référentiel, `/languages/me` pour les langues parlées |
| `GET /categorycursus/all`, `/{id}`, `POST`, `PUT`, `DELETE`, `PATCH /{id}/unarchive` — idem `/levelcursus/*` et `/typeslots/*` | déplacées sous `/reference/*`, **désarchivage conservé**, avec contrôle de rôle ajouté sur les types de créneau |
| `GET /genders/all`, `/roles/all`, `/statusreservations/all`, `/typeaddresses/all` | déplacées sous `/reference/*` |
| `POST /notifications`, `PUT /notifications/{id}/toggle-seen`, `/notifications/all-toggle-seen` | conservées, routes normalisées |
| `POST /users/filter`, `GET /users/{id}`, `POST /users`, `PUT /users/{id}` | déplacées sous `/admin/users` |
| `POST /usermanagement/teachers`, `PATCH /usermanagement/teachers/{id}/confirmation`, `GET /usermanagement/teachers/{id}/siret-verification`, `PATCH /usermanagement/users/{id}/status`, `GET /usermanagement/teachers/{id}/documents`, `PATCH /usermanagement/documents/{id}/review`, `POST /usermanagement/teachers/{id}/document-requests`, `DELETE /usermanagement/document-requests/{id}`, `POST`/`PUT`/`DELETE /usermanagement/document-types` | déplacées sous `/admin/*` ; le catalogue des types passe en super-administration |
| `POST /file/upload`, `GET /file/download`, `DELETE /file` | **supprimées** — le service dont elles dépendent n'est pas enregistré, ces routes échouent à l'exécution |
| `POST /dev/seed-users`, `/dev/seed-slots` | conservées, **strictement hors production** |
| `GET /version` | conservée ; **ajout** des points de santé |
| — | **ajoutée** : `POST /teachers/me/students/search` — mes élèves ([E20](../10-user-stories/E20-suivi-de-cours.md)) |
| — | **ajoutées** : toutes les routes `/boards/*` — suivi de cours ([E20](../10-user-stories/E20-suivi-de-cours.md)) |
