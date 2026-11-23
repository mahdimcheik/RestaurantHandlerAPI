# Feuille de route du front-end

Déclinaison côté client Angular (`RestaurantHandlerClient`) des jalons **J4 — PWA client** et
**J5 — Back-office & KDS** de la [feuille de route](01-feuille-de-route.md). L'API de J1 à J3
est en place ; ce document suit les écrans.

**Hypothèses :** développeur solo, API stable (contrat OpenAPI régénéré par
`npm run api:update`), ouverture progressive possible (comptoir + KDS d'abord, click & collect
ensuite, cf. J6). D'où l'ordre retenu : **back-office et KDS d'abord** (F3 à F7, sortie J5),
**PWA client ensuite** (F8, sortie J4). C'est le **critère de sortie** qui clôt une étape, pas
la date.

Statuts : ✅ terminé · 🟡 en cours · ⬜ à faire.

## Vue d'ensemble

| Étape | Objectif | Stories | Critère de sortie | Statut |
|---|---|---|---|---|
| **F0 — Socle** | Application saine, outillée, client API généré | — | build et tests verts | ✅ |
| **F1 — Comptes** | Se connecter, créer et gérer un compte | E01 | parcours complets contre la vraie API | 🟡 |
| **F2 — Gestion de la carte** | Le Manager saisit toute la carte à l'écran | E02 | la vraie carte saisie sans Swagger | 🟡 |
| **F3 — Ouverture & réglages** | Horaires, fermetures, pause, ruptures, créneaux | E03, US-E10-5 | ouvrir, fermer, mettre en pause depuis la tablette | 🟡 |
| **F4 — Temps réel** | Connexion SignalR fiable et visible | US-E07-3, US-E08-1 | coupure réseau rattrapée sans rechargement | 🟡 |
| **F5 — Comptoir** | Piloter les commandes du jour | E06, US-E05-3 | une commande comptoir saisie en moins de 30 s | ⬜ |
| **F6 — KDS** | La cuisine prépare sans papier | E07 | file lisible à 2 m, gestes annulables | ⬜ |
| **F7 — Pilotage** | Statistiques et utilisateurs | US-E09-1→3, US-E10-1→4 | chiffres du jour justes | ⬜ |
| → **Sortie J5** | | | **service simulé de 30 min sans papier** | |
| **F8 — PWA client** | Commander, payer, suivre | E04, E05, E08, légal | **parcours 1 et 2 sur iPhone et Android réels (sortie J4)** | ⬜ |
| **F9 — Qualité** (en continu) | Sûreté, accessibilité, tests de bout en bout | — | axe sans violation, E2E verts en CI | 🟡 |

## F0 — Socle — ✅

- Angular 22 **zoneless**, composants standalone, routes chargées à la demande.
- Client API généré (ng-openapi-gen, 16 services) ; un wrapper par service dans
  `shared/services/` (`unwrap`, `unwrapPage`, `toVoid`).
- Cache des données qui changent peu : `shared/services/store.service.ts` (`Cached<T>`).
- Styles globaux par classes (`src/styles/`), thèmes clair / sombre / système, sélecteur de thème.
- Intercepteurs : jeton en mémoire + renouvellement sur 401, `Idempotency-Key`, XSRF.
- Proxy de dev même origine (`proxy.conf.mjs`, `API_URL`), i18n `@angular/localize`, tests Vitest.

## F1 — Comptes (E01) — 🟡

**Fait :** connexion, inscription (US-E01-1), mot de passe oublié et réinitialisation
(US-E01-5), activation par e-mail et renvoi du lien, bandeau d'activation, gardes `authGuard`,
`guestGuard`, `managerGuard`, retour filtré par `safeReturnUrl`, reprise de session au
démarrage.

**Reste :**
- Connexion Google (US-E01-2) : `GOOGLE_CLIENT_ID` à renseigner, CSP à autoriser.
- Profil et téléphone (US-E01-4), suppression du compte (US-E01-6).
- Appareil partagé Manager (US-E01-7) : déconnexion visible, durée de session.
- Passage au cookie de session `HttpOnly` quand l'API appliquera
  [ADR-0002](../40-decisions/ADR-0002-auth-cookies-httponly.md) (calendrier : [question 13](02-questions-ouvertes.md)).

## F2 — Gestion de la carte (E02) — 🟡

**Fait :** coquille `/gestion` (menu latéral, tableau de bord avec tuiles carte et chiffres du
jour), catégories (US-E02-2), produits avec photo (US-E02-3), ingrédients avec prix, rupture,
« proposé en remplacement » et photo (US-E02-4,
[ADR-0012](../40-decisions/ADR-0012-remplacements-prix-ingredient.md)), composition retirable
(US-E02-5), groupes d'options partagés avec quantités par choix, bornes par produit et images
(US-E02-9), archivage et affichage des archivés. Menus (US-E02-6) : liste, fiche (prix, TVA
unique du menu, photo), emplacements avec bornes de choix et produits actifs proposés avec
surcoût. Réordonnancement ▲▼ des catégories, des produits (par catégorie filtrée), des menus et
des emplacements (liste complète envoyée, 409 → rechargement). Bascule de rupture d'un produit
(liste et fiche).

**Reste :**
- Validation à l'écran contre la vraie API (tests unitaires seulement à ce jour).
- **Sortie :** saisir la vraie carte du restaurant à l'écran (aussi le « reste » de J1).

## F3 — Ouverture & réglages (E03, US-E10-5) — 🟡

**Fait :** feature `features/settings/`.
- État d'ouverture toujours visible dans la barre du haut de `/gestion` (icône + texte :
  « Ouvert — ferme à 14:30 », « Fermé — ouvre lundi à 18:30 », « Commandes en pause jusqu'à… »),
  relu sur `AvailabilityChanged` (portée `opening`), à chaque resynchronisation temps réel et à
  l'échéance de la prochaine bascule horaire : fin de pause, fermeture, ouverture, que le serveur
  ne pousse pas (`shared/services/opening-state.service.ts`).
- Dialogue « Service » depuis toute page de gestion : pause 15 / 30 / 60 min ou jusqu'à reprise,
  reprise, délai ASAP ajustable par pas de 5 min (US-E03-3, US-E03-5).
- `/gestion/ouverture` : horaires de la semaine, plusieurs plages par jour, recopie sur les jours
  suivants (US-E03-1) ; fermetures exceptionnelles en journées entières ou en plage horaire, avec
  motif, liste des passées, suppression confirmée (US-E03-2).
- `/gestion/ruptures` : produits actifs et ingrédients, recherche, filtre « seulement les
  ruptures », une touche par article (US-E03-4).
- `/gestion/restaurant` : informations (nom, raison sociale, adresse, téléphone, SIRET, TVA),
  logo, paramètres de service et de créneaux (US-E03-6, US-E10-5), dernières modifications.
- Heures affichées dans le fuseau du restaurant (`shared/utils/restaurant-time.ts`).

**Reste :**
- Validation à l'écran contre la vraie API (tests unitaires seulement à ce jour).
- Commandes programmées touchées par une fermeture (US-E03-2, API en J2).
- Détail de chaque modification des réglages (l'historique n'affiche que date et auteur).
- **Sortie :** ouvrir, fermer, mettre en pause et déclarer une rupture depuis la tablette.

## F4 — Temps réel — 🟡

**Fait :**
- `core/realtime/realtime.service.ts` : connexion SignalR unique à `/hubs/realtime`
  (`@microsoft/signalr`, chargé à la demande hors du bundle initial), ouverte à l'entrée de
  `/gestion` ; JWT passé en `access_token` et renouvelé s'il expire dans moins de 30 s ;
  reconnexion sans fin (0, 2, 5, 10 s puis toutes les 30 s), y compris quand la toute première
  connexion échoue ; connexion rouverte à chaque changement d'utilisateur (les groupes sont
  attribués par le serveur à la connexion).
- Événements typés (`realtime-events.ts`) : `Order*`, `PaymentAnomaly`, `AvailabilityChanged`,
  `Notification` ; le hub ne fait que pousser, toute action passe par l'API REST.
- Rattrapage : pas de numéro de séquence côté API, donc `resync` à chaque (re)connexion et à
  chaque retour au premier plan après plus de 5 s d'absence (risque R6) ; chaque écran relit
  alors son état par REST.
- État de connexion toujours visible dans la barre du haut (icône + libellé + couleur :
  « Temps réel connecté », « Reconnexion… », « Hors ligne » dès l'événement `offline` du
  navigateur) et bandeau d'alerte après 5 s sans connexion (`shared/ui/connection-status`).
- Branchements : état d'ouverture (portée `opening`), caches de la carte déjà chargés (portée
  `catalog` : rupture saisie sur une autre tablette), chiffres du jour du tableau de bord (chaque
  événement de commande). Horaires, fermetures et réglages ne sont pas relus automatiquement :
  leurs formulaires suivent le cache et perdraient la saisie en cours.

**Vérifié contre la vraie API (2026-11-23) :** connexion WebSocket via le proxy de dev, rupture
saisie dans un onglet reflétée dans l'autre sans rechargement, indicateur « Hors ligne » et
bandeau après 5 s sur les événements `offline` / `online` du navigateur.

**Reste :**
- Vraie coupure réseau d'1 min (DevTools hors ligne) pendant un flux de commandes.
- Connexion anonyme de la PWA client (carte et état d'ouverture, F8), suivi de commande
  (US-E08-1, F8), KDS (US-E07-3, F6) : réutiliser `RealtimeService`, `resync` et
  `ConnectionBanner`.
- **Sortie :** couper le réseau 1 min pendant un flux de commandes, tout se recale seul.

## F5 — Comptoir (E06) — ⬜

- Commandes du jour, filtrées par statut, recherche par numéro du jour (US-E06-2).
- Validation des commandes « à payer au retrait » `AValider` : son + badge (US-E06-3).
- Saisie comptoir rapide, optimisée tablette d'une main (US-E06-1).
- Encaissement déclaré, paiement mixte (US-E05-3) ; remise (US-E06-4).
- Client non venu (US-E06-5), annulation côté restaurant avec motif (US-E06-6), remboursement
  en ligne déclenché par l'API (US-E05-4).
- 409 → rechargement et message ; aucun total calculé côté client (règle R2).
- **Sortie :** une commande comptoir saisie et encaissée en moins de 30 s.

## F6 — KDS (E07) — ⬜

- Route `/cuisine`, plein écran, contraste élevé, lisible à 2 m (US-E07-1).
- File triée par heure de retrait, gestes « en préparation » / « prête », annulation du dernier
  geste (US-E07-2).
- Son à l'arrivée d'une commande ; statut = couleur **et** libellé.
- Résistance aux coupures : indicateur F4, resynchronisation, écran jamais figé en silence
  (US-E07-3).
- **Sortie :** 20 commandes simulées traitées sans papier ni rechargement manuel.

## F7 — Pilotage (E09, E10) — ⬜

- Statistiques : tableau du jour (US-E09-1, déjà amorcé sur le tableau de bord), ventes par
  produit (US-E09-2), heures de pointe (US-E09-3).
- Utilisateurs (US-E10-1), création d'un compte Manager (US-E10-2), statut d'un compte
  (US-E10-3), levée d'un blocage « paiement au retrait » (US-E10-4).
- **Sortie :** chiffres du jour identiques à ceux de l'API sur un jeu de commandes connu.

**→ Sortie J5 :** service simulé de 30 minutes (comptoir + en ligne + KDS) sans papier.

## F8 — PWA client (E04, E05, E08) — ⬜

- PWA installable : manifest, service worker (ressources statiques et carte en cache,
  **jamais** commandes ni paiements).
- Accueil branché sur `/restaurant/info` et `/restaurant/status` (ouvert, fermé, en pause).
- Carte (US-E02-1), fiche produit avec options, retraits et remplacements (US-E04-1),
  composition de menu (US-E04-2), panier persistant, devis par l'API.
- Moment du retrait ASAP ou créneau (US-E04-3), validation avec refus détaillé par ligne
  (US-E04-4, US-E04-5), commande exigeant un compte (US-E01-3).
- Paiement en ligne SumUp ou au retrait (US-E05-1), reçu (US-E05-5).
- Suivi temps réel (US-E08-1), bandeau « commande en cours » (US-E08-2), annulation
  (US-E04-6), historique (US-E08-3).
- Pages légales : CGV, confidentialité, mentions, lien allergènes
  ([conformité](../20-regles-metier/04-conformite-legale.md)).
- **Sortie (J4) :** parcours 1 et 2 complets sur un iPhone et un Android réels.

## F9 — Qualité transverse (en continu) — 🟡

**Fait :** tests unitaires Vitest sur le socle, l'auth et le back-office ; contrôles axe
ponctuels sur l'auth ; budgets de build.

**Reste :**
- `strict` + `strictTemplates` dans `tsconfig.json`, ESLint.
- Playwright : parcours auth, saisie carte, comptoir, KDS, commande client ; axe dans chaque
  parcours.
- CSP stricte (dont Google Identity) vérifiée en préproduction.
- Essai régulier contre la vraie API et PostgreSQL (aucun écran n'a encore été validé de bout
  en bout dans un navigateur contre le vrai backend).

## Dette front connue

- Image `auth.webp` provisoire.
- Accueil en dur (adresse, horaires, téléphone) ; bouton « Commander » vers `/commander`, qui
  n'existe pas encore (F8).
- Dossier `src/app/design-dev` vide.
- Jeton d'accès en mémoire tant qu'ADR-0002 n'est pas appliquée côté API.

## V2 — côté écran

Voir la [liste V2](01-feuille-de-route.md#v2--après-louverture) ; touchent le front :
allergènes dans la carte (US-E02-8), prévisualisation de la carte (US-E02-7), notification
« c'est prêt » (US-E08-4), vue cuisine agrégée (US-E07-4), recommande (US-E04-7), comparaison
de périodes (US-E09-4).
