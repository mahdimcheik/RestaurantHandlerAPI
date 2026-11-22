# ADR-0021 — Notifications natives par Web Push (VAPID), sans application store

**Statut :** Accepté
**Application :** Appliquée (branche `feature/web-push`, 2026-10-22), non vérifiée sur appareil réel
**Date :** 2026-10-22

## Contexte

Une notification n'était visible que dans l'application ouverte (cloche + SignalR). On veut
qu'elle apparaisse aussi comme notification système sur Android et iPhone, application fermée.
Le front est une PWA (service worker Angular) ; il n'existe pas d'application store.

## Décision

- **Web Push standard, clés VAPID**, via la bibliothèque `WebPush` côté API et `SwPush` côté
  front. Pas de Firebase, pas de Capacitor, pas de compte Apple Developer.
- **Un abonnement par appareil** (`WebPushSubscriptions`, endpoint unique). Un navigateur repris
  par un autre compte est réassigné au dernier connecté ; la déconnexion détache l'appareil.
- **Point d'entrée unique** [`NotificationDispatcher`](../../RestaurantHandler/Services/NotificationDispatcher.cs) :
  ligne en base (avec `Link`, l'écran à ouvrir), événement SignalR, puis notification native.
  Les messages de réservation passent par
  [`PushNotificationService.EnqueueChatMessage`](../../RestaurantHandler/Services/PushNotificationService.cs),
  au seul destinataire.
- **Envoi en file Hangfire, identifiants seulement** (id de notification ou de message) : le
  texte est relu en base à l'envoi (règle 3 de
  [événements & notifications](../20-specs-fonctionnelles/06-evenements-notifications.md)).
  Pas de rejeu automatique, pour ne pas doubler un message déjà reçu sur un autre appareil.
  Un abonnement qui répond 404/410 est supprimé.
- **Facultatif** : sans `VAPID_PUBLIC_KEY`/`VAPID_PRIVATE_KEY`, rien n'est envoyé et le front
  masque l'option.

## Conséquences

- **iPhone/iPad : seulement l'application ajoutée à l'écran d'accueil, iOS 16.4 et plus.**
  Limite d'Apple, expliquée dans les paramètres.
- La clé privée VAPID est un secret de déploiement ; la changer invalide tous les abonnements.
- Les libellés des notifications restent en français côté serveur (pas d'i18n serveur).

## Alternatives écartées

- **Capacitor + FCM/APNs** : couverture iOS complète, mais applications natives à publier et à
  maintenir sur deux stores.
