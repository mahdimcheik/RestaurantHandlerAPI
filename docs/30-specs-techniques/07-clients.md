# Clients de l'API

> Statut : **Stable** · Contrat : [contrat d'API](03-contrat-api.md) · Sécurité :
> [sécurité](04-securite.md)
>
> Ce dépôt ne contient que l'API. Ce document décrit ce qu'elle expose à ses clients et ce
> qu'elle attend d'eux. Les choix du front (cadre, rendu serveur, organisation) relèvent de
> son propre dépôt.

## Les clients

| Client | Dépôt | Usage |
|---|---|---|
| Application web | `BonProf` (Angular 20) | seul client applicatif ; client d'API **généré** par `ng-openapi` |
| Stripe | — | notifications signées et redirections vers le front |
| Outils de développement | — | Swagger UI, `stripe listen` |

## Description OpenAPI

| Élément | Existant | Cible |
|---|---|---|
| Générateur | Swashbuckle ([`Program.cs`](../../RestaurantHandler/Program.cs), `ConfigureSwagger`) | inchangé ; génération native du cadre après montée de version |
| Document | `/swagger/v1/swagger.json`, publié **dans tous les environnements** | développement et recette uniquement |
| Interface | à la racine `/`, en développement ou si `ALLOW_DEMO_SEED=true` | développement uniquement |
| Énumérations | sérialisées **par nom** (`JsonStringEnumConverter`) | inchangé |

**Règle :** toute modification d'une représentation ou d'une route est une modification du
contrat. Le client du front est régénéré et livré **en même temps** que l'API qui le change.
Aucun contrôle automatique ne le vérifie aujourd'hui : l'API et le front vivent dans deux
dépôts. Voir [questions ouvertes](../50-backlog/02-questions-ouvertes.md).

## Authentification côté client

**Existant** — voir [ADR-0002](../40-decisions/ADR-0002-auth-cookies-httponly.md) pour la
cible :

1. `POST /Auth/login` (ou `/Auth/google`) renvoie un **JWT d'accès** dans le corps et dépose
   le cookie `refreshToken` (`httpOnly`, `Secure`, `SameSite=Strict`).
2. Le client envoie le JWT dans l'en-tête `Authorization: Bearer …`.
3. À l'expiration, `GET /Auth/refresh-token` — avec les identifiants de requête activés — lit
   le cookie et renvoie un nouveau JWT.
4. `GET /Auth/logout` efface le cookie (sans invalider le jeton en base).

Le client doit envoyer ses requêtes **avec identifiants** (`withCredentials`) pour que le
cookie circule : la politique CORS l'autorise via `AllowCredentials`.

## CORS

Politique unique ([`Program.cs`](../../RestaurantHandler/Program.cs), `ConfigureCors`) : origines
filtrées par [`CorsHelper.IsOriginAllowed`](../../RestaurantHandler/Utilities/CorsHelper.cs), toutes
méthodes, tous en-têtes, identifiants autorisés, en-têtes `filename` et
`Content-Disposition` exposés (téléchargement de factures et d'exports).

**Écart :** la liste d'origines est codée en dur et mélange production, développement et
réseau local. Cible : une liste **par environnement**, fournie par configuration, sans
origine de développement en production.

## Temps réel

| Élément | Existant |
|---|---|
| Point de connexion | `/chathub` ([`ChatHub`](../../RestaurantHandler/Controllers/ChatHub.cs)) |
| Authentification | `[Authorize]` sur le concentrateur ; le JWT n'est **pas** lu depuis la chaîne de requête (aucun `OnMessageReceived`) — le transport WebSocket depuis un navigateur est donc à vérifier |
| Adressage | par **adresse e-mail**, via un registre de connexions **en mémoire** (`ConnectionManager`) : une seule instance d'API possible |
| Types d'événements | `SignalRNotificationTypeEnum` : `Message`, `Ping`, `Notification`, `Chat`, `Writing` |

La cible (concentrateur unique, adressage par identifiant de compte, aucune méthode serveur
exposée sauf l'indicateur de saisie) est décrite dans le
[contrat d'API](03-contrat-api.md#temps-réel).

## Retours de Stripe

Adresses construites à partir de `API_FRONT_URL` dans
[`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs), [`StripeConnectService`](../../RestaurantHandler/Services/Payments/StripeConnectService.cs). Le front **doit** exposer ces
routes :

| Route du front | Déclencheur |
|---|---|
| `/payment/success?session_id={CHECKOUT_SESSION_ID}` | paiement terminé |
| `/payment/cancel` | paiement abandonné |
| `/payment/connect/refresh?account_id=…` | lien d'onboarding expiré |
| `/payment/connect/return?account_id=…` | onboarding terminé |

Aucune de ces pages ne vaut confirmation : seule la notification signée fait foi
([E07](../10-user-stories/E07-paiement.md)).

## Fichiers

Les URL publiques (avatars, images) sont construites par l'API à partir de l'URL publique du
stockage et renvoyées dans les représentations. Le client ne construit jamais une URL de
fichier lui-même. Les fichiers privés (factures) sont servis par des points d'accès dédiés,
avec l'en-tête `filename`.

## Service

`GET /version` renvoie `{ "version": "<APP_VERSION>" }`, gravée dans l'image au build. Le
client peut comparer sa version à celle de l'API. **Absent :** points de santé.
