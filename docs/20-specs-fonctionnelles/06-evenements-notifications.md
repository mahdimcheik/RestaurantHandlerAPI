# Événements & notifications

> Statut : **Stable** — catalogue de référence. Toute nouvelle fonctionnalité qui produit un
> événement notifiable doit ajouter sa ligne ici.

## Les trois canaux

| Canal | Usage | Garantie |
|---|---|---|
| **Interne** | trace persistante consultable dans l'application | durable, source de vérité |
| **Temps réel** | apparition immédiate chez un utilisateur connecté | *au mieux* — un message perdu se retrouve en base |
| **E-mail** | ce qui ne peut pas attendre la prochaine connexion | asynchrone, avec réessais |
| **Natif** | notification système de l'appareil, application fermée ([ADR-0021](../40-decisions/ADR-0021-notifications-natives-web-push.md)) | *au mieux*, sur les appareils où l'utilisateur l'a activée |

**Règle :** le temps réel n'est jamais le seul canal d'un événement important. Il accélère,
il ne garantit pas.

## Catalogue

Légende : ✉ e-mail · 🔔 notification interne · ⚡ temps réel

Toute notification interne 🔔 part aussi en notification native, ainsi que chaque message
reçu dans une conversation.

### Compte

| Événement | Élève | Professeur | Admin |
|---|---|---|---|
| Inscription | ✉ confirmation | ✉ confirmation | — |
| E-mail confirmé | 🔔 bienvenue | 🔔 bienvenue | — |
| Mot de passe oublié | ✉ lien | ✉ lien | — |
| Mot de passe modifié | ✉ alerte | ✉ alerte | — |
| Compte activé manuellement | ✉ 🔔 | ✉ 🔔 | — |
| Compte banni | ✉ motif | ✉ motif | — |
| SIRET signalé à l'inscription | — | 🔔 | 🔔 |

### Vérification documentaire

| Événement | Professeur | Admin |
|---|---|---|
| Pièce déposée | — | 🔔 |
| Pièce validée | 🔔 | — |
| Pièce refusée | ✉ 🔔 **avec le motif** | — |
| Document demandé par un administrateur | ✉ 🔔 | — |
| Compte entièrement vérifié | ✉ 🔔 | — |

### Réservation

| Événement | Élève | Professeur | Admin |
|---|---|---|---|
| Réservation créée *(flux payant)* | 🔔 rappel de l'échéance | 🔔 | — |
| Réservation créée *(flux sans paiement)* | 🔔 en attente de réponse | ✉ 🔔 **48 h pour répondre** | — |
| Réservation acceptée par le professeur | ✉ 🔔 ⚡ | — | — |
| Réservation refusée par le professeur | ✉ 🔔 ⚡ | — | — |
| Aucune réponse sous 48 h | ✉ 🔔 | 🔔 | — |
| Commande expirée faute de paiement | 🔔 | — | — |
| Rappel avant le cours | 🔔 | 🔔 | — |
| Cours confirmé par code de présence | 🔔 | 🔔 | — |

### Argent

| Événement | Élève | Professeur | Admin |
|---|---|---|---|
| Paiement confirmé | ✉ 🔔 | ✉ 🔔 **nouvelle réservation payée** | — |
| Paiement échoué | 🔔 | — | — |
| Versement effectué | — | ✉ 🔔 **montant brut, dette déduite, net** | — |
| Versement en échec répété | — | 🔔 | 🔔 |
| Remboursement émis | ✉ 🔔 | 🔔 **crédit annulé** | — |
| Pénalité inscrite | — | ✉ 🔔 **montant et motif** | — |
| Plafond de dette atteint | — | ✉ 🔔 **créneaux suspendus** | 🔔 |
| Compte de paiement confirmé | — | ✉ 🔔 | — |

### Annulation et litige

| Événement | Élève | Professeur | Admin |
|---|---|---|---|
| Annulation par l'élève | 🔔 confirmation | ✉ 🔔 ⚡ | — |
| Annulation par le professeur | ✉ 🔔 ⚡ **remboursement intégral** | 🔔 pénalité | — |
| **Litige ouvert** | 🔔 | ✉ 🔔 ⚡ | ✉ 🔔 |
| Litige accepté par le professeur | ✉ 🔔 | 🔔 | 🔔 |
| Litige arbitré | ✉ 🔔 **issue et motif** | ✉ 🔔 **issue et motif** | — |
| Relance avant résolution automatique | — | 🔔 | ✉ 🔔 |
| Litige résolu automatiquement | 🔔 | 🔔 | 🔔 |

> **Le litige notifie le professeur et l'administrateur au même instant.** C'est une
> décision de conception : pas de phase séquentielle. Voir
> [E11](../10-user-stories/E11-litiges.md).

### Communication et avis

| Événement | Élève | Professeur | Admin |
|---|---|---|---|
| Message reçu dans une conversation | 🔔 ⚡ | 🔔 ⚡ | — |
| Demande de contact depuis une fiche | — | ✉ 🔔 | — |
| Avis déposé | — | 🔔 | — |
| Avis retiré par la modération | — | 🔔 | — |

### Suivi de cours

Aucun événement en première version : créer, modifier ou déplacer une carte ne notifie
personne. Le suivi se consulte, il ne sollicite pas. Voir
[Q13](../50-backlog/02-questions-ouvertes.md#q13--notifier-lélève-dune-nouvelle-carte-de-suivi-).

## Règles d'émission

1. **Une notification interne est créée dans la même transaction que l'événement métier.**
   Si l'événement est annulé, la notification l'est aussi.
2. **L'e-mail est mis en file, jamais envoyé en ligne.** Un prestataire d'envoi indisponible
   ne doit pas faire échouer un paiement.
3. **Seuls les éléments nécessaires au rendu transitent par la file** — destinataire, type,
   identifiants métier. Jamais le corps rendu, jamais un lien sensible : les arguments d'une
   tâche sont persistés en clair et consultables dans le tableau de bord technique. Voir
   [dette technique](../50-backlog/03-dette-technique.md).
4. **Le temps réel est émis après validation en base**, jamais avant.
5. **Un échec de notification n'annule jamais l'action métier.** Il est journalisé et
   réessayé.
6. **Pas de doublon de canal sans raison.** L'e-mail est réservé à ce qui appelle une action
   ou porte une conséquence financière.

## Catégories

Compte · Réservation · Paiement · Message · Produit · Document.

Chaque catégorie porte une icône et une couleur issues du référentiel. La liste est en
**ajout seul** : sa valeur est persistée, réordonner corromprait les données existantes.

## État dans le code

**E-mails** — trois seulement, mis en file Hangfire par
[`MailService`](../../RestaurantHandler/Services/MailService.cs) :

| E-mail | Émetteur | Écart |
|---|---|---|
| Confirmation de compte | `AuthService` | le corps HTML rendu et le lien transitent **dans les arguments de la tâche** (règle 3 violée) |
| Réinitialisation du mot de passe | `AuthService` | idem |
| Demande de contact depuis une fiche | `UserService` | aucun plafond (`CONTACT_MAX_PER_WINDOW` absent) |

`MailService.SendReminder` et `Templates/ReminderTemplate.cshtml` existent mais ne sont
appelés nulle part (code mort, reste d'un autre projet). **Aucun** e-mail n'est émis pour
l'argent (paiement, versement, remboursement, pénalité) ni pour les litiges.

**Notifications internes, temps réel et natives** — émises par
[`NotificationDispatcher`](../../RestaurantHandler/Services/NotificationDispatcher.cs) depuis les services
de réservation, `DisputeService`, `UserManagementService`, `TeacherDocumentService` et
`CursusService` : ligne en base avec le lien de l'écran concerné, événement SignalR, puis
notification native en file Hangfire
([`PushNotificationService`](../../RestaurantHandler/Services/PushNotificationService.cs)).

| Règle | État |
|---|---|
| 1. Même transaction que l'événement | à vérifier site par site |
| 2. E-mail en file | tenue |
| 3. Pas de contenu rendu dans la file | **non tenue** |
| 4. Temps réel après validation en base | à vérifier ; les appels `SendMessageByUserEmail` ne sont pas attendus (`await` absent) |
| 5. Un échec n'annule pas l'action | tenue pour l'e-mail (réessais Hangfire) |

Les lignes du catalogue sans émetteur dans le code sont à considérer comme **absentes**.
Leur état est suivi dans [E14](../10-user-stories/E14-notifications.md).
