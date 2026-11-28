# Architecture

## Vue d'ensemble

```
 PWA Angular (mobile-first)                  VPS (Docker)
 ┌───────────────────────────┐   HTTPS   ┌───────────────────────────────────────┐
 │ Site client  /            │──────────►│ API RestaurantHandler (.NET 10)       │
 │ Back-office  /gestion     │◄──────────│  ├─ contrôleurs → services → EF Core  │
 │ Service      /service     │ SignalR   │  ├─ hub temps réel                    │
 └───────────────────────────┘           │  ├─ Hangfire (tâches planifiées)      │
                                         │  └─ HybridCache (carte, ouverture)    │
                                         │ PostgreSQL                            │
                                         └──────────┬───────────────┬────────────┘
                                                    │               │
                                     SumUp (paiement en ligne)   Fichiers : disque ou S3
                                     Google (connexion)          SMTP (e-mails de compte)

 Hors système : caisse du restaurant (ventes au comptoir, sans lien avec l'app)
```

## Backend — existant réutilisé

| Brique | État | Usage restaurant |
|---|---|---|
| .NET 10, projet unique `RestaurantHandler/` | en place | contrôleurs → services → EF Core ([ADR-0001](../40-decisions/ADR-0001-archi-backend.md)) |
| PostgreSQL + EF Core (Npgsql), migration unique `InitialCreate` | en place | modèle carte + commandes déjà migré |
| ASP.NET Identity, rôles `Client` / `Manager`, Google | en place | comptes ([E01](../10-cahier-des-charges/E01-comptes-authentification.md)) |
| JWT d'accès + jeton de renouvellement en cookie `httpOnly` | en place | cf. [ADR-0002](../40-decisions/ADR-0002-auth-cookies-httponly.md) |
| SignalR (`RealtimeHub` sur `/hubs/realtime`, émission via `RealtimeService`) | en place (J0) | groupe `restaurant` (Managers, attribué à la connexion), envoi par utilisateur (claim `NameIdentifier`) : le suivi client passe par l'envoi au propriétaire, sans groupe par commande |
| Hangfire + PostgreSQL | en place | expirations de paiement, refus auto, reprise de pause, réconciliation SumUp |
| HybridCache | en place | carte publique, état d'ouverture ([ADR-0004](../40-decisions/ADR-0004-cache-memoire.md)) |
| `StorageService` (disque local ou S3, `STORAGE_PROVIDER`) | en place | photos de produits et catégories ([ADR-0003](../40-decisions/ADR-0003-stockage-objet.md)) |
| Mail (MailKit + RazorLight) | en place | e-mails de compte uniquement en V1 |
| Rate limiting | en place | validation de commande, auth |
| Notifications en base (cloche) | en place | disponibles |
| Web Push (VAPID) | en place, **dormant** | branché en V2 sur « Prête » (US-E08-4) |

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
| `OrderStatusChanged` | client propriétaire (`SendToUserAsync`), groupe `restaurant` |
| `OrderCreated` | groupe `restaurant` |
| `PaymentAnomaly` (montant SumUp ≠ total, remboursement en échec) | groupe `restaurant` |
| `AvailabilityChanged` (rupture, pause, ouverture) | tous (y compris anonymes sur la carte) |

Le temps réel **notifie** ; la vérité reste l'API. Tout client resynchronise par une lecture
REST à la (re)connexion.

Authentification : le hub accepte les connexions **anonymes** (depuis J1) pour que la carte
publique reçoive `AvailabilityChanged` (portée `catalog` ou `opening`) en moins de 5 secondes ;
un anonyme ne reçoit que les diffusions à tous. Le navigateur connecté transmet le JWT en
paramètre `access_token`, accepté uniquement sur les chemins `/hubs`. Les groupes sont
attribués par le serveur, jamais demandés par le client.

## Front — PWA Angular mobile-first

- Une seule application, trois zones : client (`/`), back-office (`/gestion`), écrans de
  service (`/service` : cuisine/KDS et commandes, barre de navigation en haut pour
  tablette paysage), routes protégées par rôle.
- Installable (manifest + service worker) ; le service worker met en cache les ressources
  statiques et la carte, **jamais** les commandes ni les paiements.
- Client API généré depuis le Swagger de l'API.
- Pas de mode hors ligne fonctionnel (cf. [exploitation](03-exploitation.md#mode-dégradé--panne-internet)).
- Français uniquement.

## SumUp

- Paiement en ligne : création d'un *checkout* côté serveur, paiement par le widget ou la page
  hébergée SumUp, confirmation par notification + relecture de l'état. Détails :
  [ADR-0007](../40-decisions/ADR-0007-sumup-psp.md).
- Seul moyen de paiement de l'app ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) ; pas de terminal ni d'encaissement sur place
  piloté par l'app.
