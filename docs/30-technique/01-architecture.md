# Architecture

## Vue d'ensemble

```
 PWA Angular (mobile-first)                  VPS (Docker)
 ┌───────────────────────────┐   HTTPS   ┌───────────────────────────────────────┐
 │ Site client  /            │──────────►│ API RestaurantHandler (.NET 10)       │
 │ Back-office  /gestion     │◄──────────│  ├─ contrôleurs → services → EF Core  │
 │ KDS          /cuisine     │ SignalR   │  ├─ hub temps réel                    │
 └───────────────────────────┘           │  ├─ Hangfire (tâches planifiées)      │
                                         │  └─ HybridCache (carte, ouverture)    │
                                         │ PostgreSQL                            │
                                         └──────────┬───────────────┬────────────┘
                                                    │               │
                                     SumUp (paiement en ligne)   Stockage objet S3 (OVH)
                                     Google (connexion)          SMTP (e-mails de compte)

 Hors système : caisse certifiée tierce (encaissement sur place)
```

## Backend — existant réutilisé

| Brique | État | Usage restaurant |
|---|---|---|
| .NET 10, projet unique `RestaurantHandler/` | en place | contrôleurs → services → EF Core ([ADR-0001](../40-decisions/ADR-0001-archi-backend.md)) |
| PostgreSQL + EF Core (Npgsql), migration unique `InitialCreate` | en place | modèle carte + commandes déjà migré |
| ASP.NET Identity, rôles `Client` / `Manager`, Google | en place | comptes ([E01](../10-cahier-des-charges/E01-comptes-authentification.md)) |
| JWT d'accès + jeton de renouvellement en cookie `httpOnly` | en place | cf. [ADR-0002](../40-decisions/ADR-0002-auth-cookies-httponly.md) |
| SignalR (`ChatHub` sur `/chathub`) | en place, nommage hérité | à renommer et structurer en groupes : `order-{id}`, `customer-{id}`, `restaurant` |
| Hangfire + PostgreSQL | en place | expirations de paiement, refus auto, reprise de pause, réconciliation SumUp |
| HybridCache | en place | carte publique, état d'ouverture ([ADR-0004](../40-decisions/ADR-0004-cache-memoire.md)) |
| `StorageService` (S3) | en place | photos de produits et catégories ([ADR-0003](../40-decisions/ADR-0003-stockage-objet.md)) |
| Mail (MailKit + RazorLight) | en place | e-mails de compte uniquement en V1 |
| Rate limiting | en place | validation de commande, auth |
| Notifications en base + Web Push | en place | non utilisés en V1 (cf. questions ouvertes) |
| Centre d'aide, envois groupés, maintenance | en place | **à retirer** en J0 |

## Organisation du code à venir

```
Domain/Entities/{Catalog,Ordering,Restaurant}/   entités (Restaurant = horaires, fermetures, paramètres)
Domain/Policies/                                 règles pures : prix, validation, transitions, créneaux
Contracts/{Catalog,Ordering,Restaurant,Stats}/   DTO d'entrée/sortie
Services/{Catalog,Ordering,Payments,Restaurant,Stats}/
Integrations/SumUp/                              client HTTP SumUp, vérification des notifications
Controllers/                                     sans logique
```

Règles : aucune entité exposée, autorisation sur l'opération, service ≤ 800 lignes, règles
métier testables sans base.

## Temps réel

| Événement | Destinataires |
|---|---|
| `OrderStatusChanged` | groupe `order-{id}` (client propriétaire), groupe `restaurant` |
| `OrderCreated` / `OrderAwaitingApproval` | groupe `restaurant` |
| `AvailabilityChanged` (rupture, pause, ouverture) | tous (y compris anonymes sur la carte) |

Le temps réel **notifie** ; la vérité reste l'API. Tout client resynchronise par une lecture
REST à la (re)connexion.

## Front — PWA Angular mobile-first

- Une seule application, trois zones : client (`/`), back-office (`/gestion`), KDS
  (`/cuisine`), routes protégées par rôle.
- Installable (manifest + service worker) ; le service worker met en cache les ressources
  statiques et la carte, **jamais** les commandes ni les paiements.
- Client API généré depuis le Swagger de l'API.
- Pas de mode hors ligne fonctionnel (cf. [exploitation](03-exploitation.md#mode-dégradé--panne-internet)).
- Français uniquement.

## SumUp

- Paiement en ligne : création d'un *checkout* côté serveur, paiement par le widget ou la page
  hébergée SumUp, confirmation par notification + relecture de l'état. Détails :
  [ADR-0007](../40-decisions/ADR-0007-sumup-psp.md).
- Terminal SumUp au comptoir : utilisé **via la caisse certifiée tierce**, pas piloté par
  l'app en V1.
