# E14 — Notifications

> Statut : **Stable** · Catalogue complet des déclencheurs :
> [événements & notifications](../20-specs-fonctionnelles/06-evenements-notifications.md)

## US-E14-01 — Être averti dans l'application

**En tant qu'** utilisateur connecté
**je veux** voir les événements qui me concernent
**afin de** réagir sans surveiller mes e-mails.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`NotificationService`](../../RestaurantHandler/Services/NotificationService.cs), [`NotificationsController`](../../RestaurantHandler/Controllers/NotificationsController.cs)
`POST /Notifications` (liste filtrée) et poussée SignalR `Notification`. Chaque notification
porte `Link`, le chemin de l'écran concerné.

**Critères d'acceptation**

- Un indicateur dans l'en-tête affiche le nombre de notifications non lues.
- Le panneau liste les notifications les plus récentes, filtrables : toutes, lues, non lues,
  avec chargement progressif.
- Chaque notification porte un titre, un message, une catégorie et une date, et **renvoie vers
  l'écran concerné**.
- Une notification reçue pendant que je suis connecté apparaît **sans rechargement**.

## US-E14-02 — Marquer comme lu

**En tant qu'** utilisateur
**je veux** marquer une notification, ou toutes, comme lues
**afin de** faire le tri.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`NotificationService`](../../RestaurantHandler/Services/NotificationService.cs)
Bascule individuelle et « tout marquer comme lu », limitées aux notifications de
l'utilisateur.

**Critères d'acceptation**

- L'action est réversible sur une notification donnée.
- « Tout marquer comme lu » ne touche que **mes** notifications.
- Le compteur est mis à jour immédiatement.

## US-E14-03 — Recevoir les e-mails indispensables

**En tant qu'** utilisateur
**je veux** recevoir par e-mail ce qui ne peut pas attendre ma prochaine connexion
**afin de** ne rien manquer d'important.

**Priorité :** Doit

**État :** Partielle · **Code :** [`MailService`](../../RestaurantHandler/Services/MailService.cs), [`ConfirmAccountTemplate`](../../RestaurantHandler/Templates/ConfirmAccountTemplate.cshtml)
Trois e-mails, en file Hangfire avec 5 réessais, gabarits Razor pour la confirmation
et la réinitialisation. Écarts : **le corps rendu transite par la file** ; l'e-mail de
contact est construit par concaténation ; aucun signalement en supervision.

**Critères d'acceptation**

- E-mails transactionnels au minimum : confirmation de compte, réinitialisation de mot de
  passe, demande de contact reçue.
- L'envoi est **asynchrone** : il ne ralentit ni ne fait échouer l'action de l'utilisateur.
- Un échec d'envoi est réessayé plusieurs fois avec un délai croissant, puis signalé en
  supervision.
- Les e-mails sont rendus depuis des gabarits versionnés, pas construits par concaténation.
- **Le contenu rendu ne transite jamais par la file de tâches** : seuls les éléments
  nécessaires au rendu y sont placés, afin qu'aucun lien sensible ne soit stocké en clair.
  Voir [dette technique](../50-backlog/03-dette-technique.md).

## US-E14-04 — Être notifié d'un litige, des deux côtés

**En tant que** professeur ou administrateur
**je veux** être averti immédiatement de l'ouverture d'un litige
**afin de** pouvoir réagir vite.

**Priorité :** Doit · **Dépend de :** US-E11-01

**État :** Partielle · **Code :** [`DisputeService`](../../RestaurantHandler/Services/DisputeService.cs)
Notification interne et temps réel simultanée au professeur et aux administrateurs.
Écarts : aucun e-mail ; pas de relance (résolution automatique absente).

**Critères d'acceptation**

- Les deux destinataires sont notifiés **au même instant**, en interne et par e-mail.
- La notification renvoie directement au dossier de litige.
- Une relance est envoyée à l'administration avant l'échéance de résolution automatique.

## US-E14-05 — Catégoriser les notifications

**En tant qu'** utilisateur
**je veux** distinguer d'un coup d'œil la nature d'une notification
**afin de** prioriser.

**Priorité :** Pourrait

**État :** Implémentée · **Code :** [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs), [`Notification`](../../RestaurantHandler/Domain/Entities/Notification/Notification.cs)
`NotificationTypeEnum`, en ajout seul, avec `Document` ajouté en dernier.

**Critères d'acceptation**

- Catégories : compte, réservation, paiement, message, produit, document.
- Chaque catégorie a une icône et une couleur propres, issues du référentiel.
- La liste des catégories est en **ajout seul** : sa valeur est persistée.

## US-E14-06 — Recevoir les notifications sur mon téléphone

**En tant qu'** utilisateur
**je veux** recevoir mes notifications et mes messages comme notifications système
**afin d'** être prévenu même quand BonProf est fermé.

**Priorité :** Devrait

**État :** Implémentée, non vérifiée sur appareil réel · **Code :**
[`PushNotificationService`](../../RestaurantHandler/Services/PushNotificationService.cs),
[`NotificationDispatcher`](../../RestaurantHandler/Services/NotificationDispatcher.cs) ·
[ADR-0021](../40-decisions/ADR-0021-notifications-natives-web-push.md)
`GET /Notifications/push/public-key` (anonyme), `POST /Notifications/push/subscribe`,
`POST /Notifications/push/unsubscribe`.

**Critères d'acceptation**

- L'activation se fait par appareil, depuis les paramètres, après l'autorisation du navigateur.
- Toute notification interne part aussi en notification native ; un message de conversation
  notifie le seul destinataire.
- Le clic ouvre l'écran concerné.
- Sur iPhone, l'application explique qu'il faut d'abord l'ajouter à l'écran d'accueil.
- Après déconnexion, l'appareil ne reçoit plus rien pour le compte sorti.
- Le texte des notifications ne transite pas par la file de tâches.
