# Feuille de route du front-end

Déclinaison côté client Angular (`RestaurantHandlerClient`) des jalons **J4 — PWA client** et
**J5 — Back-office & KDS** de la [feuille de route](01-feuille-de-route.md). L'API de J1 à J3
est en place ; ce document suit les écrans.

**Hypothèses :** développeur solo, API stable (contrat OpenAPI régénéré par
`npm run api:update`). Depuis le 2026-11-26 ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)), l'application ne prend que des commandes
en ligne payées par carte : plus de comptoir, de paiement au retrait ni d'annulation par le
client. L'ordre retenu : **back-office et KDS d'abord** (F3 à F7, sortie J5),
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
| **F5 — Commandes du jour** | Piloter les commandes du jour | E06 | retrouver, remettre ou annuler une commande en moins de 30 s | 🟡 |
| **F6 — KDS** | La cuisine prépare sans papier | E07 | file lisible à 2 m, gestes annulables | 🟡 |
| **F7 — Pilotage** | Statistiques et utilisateurs | US-E09-1→3, US-E10-1→3 | chiffres du jour justes | 🟡 |
| → **Sortie J5** | | | **service simulé de 30 min sans papier** | |
| **F8 — PWA client** | Commander, payer, suivre | E04, E05, E08, légal | **parcours 1 sur iPhone et Android réels (sortie J4)** | 🟡 |
| **F9 — Qualité** (en continu) | Sûreté, accessibilité, tests de bout en bout | — | axe sans violation, E2E verts en CI | 🟡 |

## F0 — Socle — ✅

- Angular 22 **zoneless**, composants standalone, routes chargées à la demande.
- Client API généré (ng-openapi-gen, 16 services) ; un wrapper par service dans
  `shared/services/` (`unwrap`, `unwrapPage`, `toVoid`).
- Cache des données qui changent peu : `shared/services/store.service.ts` (`Cached<T>`).
- Styles globaux par classes (`src/styles/`), thèmes clair / sombre / système, sélecteur de thème.
- Intercepteurs : jeton en mémoire + renouvellement sur 401, `Idempotency-Key`, XSRF.
- Proxy de dev même origine (`proxy.conf.mjs`, `API_URL`), tests Vitest (i18n `@angular/localize` retirée le 2026-11-24 : textes en français écrits en clair, ADR-0011).

## F1 — Comptes (E01) — 🟡

**Fait :** connexion, inscription (US-E01-1), mot de passe oublié et réinitialisation
(US-E01-5), activation par e-mail et renvoi du lien, bandeau d'activation, gardes `authGuard`,
`guestGuard`, `managerGuard`, retour filtré par `safeReturnUrl`, reprise de session au
démarrage.

**Fait le 2026-11-26 (US-E05-6) :** compte professionnel `/compte/professionnel`
(`features/account`) : SIREN, SIRET, raison sociale, TVA (calculée par l'API si vide), adresse
de facturation, « Redevenir particulier » ; lien « Compte professionnel » dans le panneau
Compte d'un client.

**Reste :**
- Connexion Google (US-E01-2) : `GOOGLE_CLIENT_ID` à renseigner dans `src/environments/environment*.ts` (front) et le `.env` de l'API, même valeur ; CSP à autoriser.
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

## Écrans de service (F5, F6) — ✅ coquille

**Fait (2026-11-26) :** coquille `shared/ui/service-layout` sous `/service` (`managerGuard`),
pour tablette tactile en paysage : pas de menu à gauche, barre du haut collante avec trois
grandes touches (Cuisine, Comptoir, Commandes ; nombre à valider sur Commandes), état
d'ouverture et pause, son des commandes à valider, connexion, lien « Gestion ». `/service`
ouvre la cuisine ; `/cuisine`, `/gestion/comptoir`, `/gestion/commandes(/:id)` redirigent.
Au back-office, un seul lien « Écrans de service » remplace Comptoir, Commandes et Cuisine.
Styles : `src/styles/_service.scss`.

**Paiement en ligne seul (2026-11-26, [ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) :** deux touches seulement (Cuisine, Commandes) ;
plus de nombre « à valider » ni de son des commandes à valider (`ApprovalAlertService`
supprimé, au back-office aussi).

**Reste :** vérifier sur la tablette réelle (paysage et portrait, où les onglets passent sur
une seconde ligne) ; remettre depuis la cuisine contre la vraie API.

## F5 — Commandes du jour (E06) — 🟡

**Paiement en ligne seul (2026-11-26, [ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) :** supprimés :
- la saisie comptoir (`counter-page`, `counter-cart`, `menu-composer`, `product-customizer`) ;
- l'encaissement (`shared/ordering/payment-panel`) ;
- accepter / refuser ;
- les filtres « À valider », « À encaisser » et « Canal ».

La liste garde « Paiement en cours », « En cours », « Prêtes » et « Closes ». Le détail garde la
remise (dès `Prête`), « non venu », l'annulation (le dialogue rappelle qu'elle se fait à la
demande du client) et le remboursement. Les puces ci-dessous sont l'historique d'avant ce
changement.

**Fait (feature `features/ordering`, sous `/service` depuis le 2026-11-26) :**
- Saisie comptoir `/service/comptoir` (US-E06-1) : catégories en onglets, produits et menus en
  tuiles, ticket à côté. Un produit sans choix obligatoire s'ajoute en une touche ; un choix
  obligatoire unique s'ajoute à la deuxième touche (les options facultatives restent sous
  « Personnaliser »). Personnalisation : « Sans X », remplacement par un ingrédient proposé
  ([ADR-0012](../40-decisions/ADR-0012-remplacements-prix-ingredient.md)), options dans leurs
  bornes ; menus emplacement par emplacement. Prénom et note facultatifs. Total et totaux de
  ligne par le devis `POST /orders/quote` (R2). Clé d'idempotence dans le corps, gardée pour un
  nouvel essai. Après l'envoi : numéro du jour en très grand et encaissement en une touche.
- Commandes du jour `/service/commandes` (US-E06-2) : jour précédent / suivant, filtres par étape
  (à valider, en cours, prêtes, à encaisser, closes) avec leurs nombres, canal, recherche par
  numéro du jour ; à valider en tête, closes en bas ; relue à chaque événement de commande et à
  chaque `resync` ; bandeau sur `PaymentAnomaly`.
- Détail `/service/commandes/:id` : lignes, personnalisations, note, TVA, paiements, historique ;
  gestes selon le statut (masquage seulement) : accepter / refuser avec motif (US-E06-3),
  encaisser (US-E05-3 : une touche par moyen = tout le reste, paiement mixte par montant),
  remettre si payée (US-E06-4), client non venu après retrait + délai de grâce (US-E06-5),
  annuler avec motif (US-E06-6), rembourser une commande close payée en ligne (US-E05-4).
  409 → relecture et message.
- Commandes à valider (US-E06-3) : nombre en badge sur « Commandes » dans le menu, annonce aux
  lecteurs d'écran, carillon Web Audio à chaque arrivée (coupable dans la barre du haut,
  réglage propre à l'appareil) — `shared/services/approval-alert.service.ts`.
- Statut partagé `shared/ui/order-status` (icône + libellé, réutilisable au KDS).

**Vérifié contre la vraie API (2026-11-23) :** commande comptoir pizza + taille saisie, envoyée
et payée en espèces en 4 touches ; détail (TVA, paiement, historique) et liste à jour.

**Reste :**
- Vérifier en navigateur : remise, non-venue, annulation avec remboursement, 409 à deux
  onglets ; rendu tablette en portrait ; contrôle axe des écrans, thèmes clair et sombre.
- **Sortie :** retrouver une commande par son numéro, la remettre ou l'annuler en moins de
  30 s sur une tablette réelle.

## F6 — KDS (E07) — 🟡

**Paiement en ligne seul (2026-11-26, [ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) :** bande « À valider », « À encaisser » et
encaissement dans le détail supprimés ; le canal n'est plus affiché. Toute commande arrive
payée : « Remis au client » est proposé dès `Prête`. Les puces ci-dessous sont l'historique
d'avant ce changement.

**Fait (feature `features/kitchen`, route `/service/cuisine`, `managerGuard`, onglet
« Cuisine » de la coquille Service) :**
- File `GET /manage/orders/kitchen` en deux colonnes, « À préparer » (`Confirmée`) et « En
  préparation », dans l'ordre de l'API (heure de retrait puis validation) avec leur nombre
  (US-E07-1). Bon : numéro du jour en très grand, heure de retrait (« dès que possible » si
  ASAP), canal, prénom, statut (icône + libellé), lignes avec quantités, composants des menus,
  personnalisations mises en évidence par type (retrait en capitales soulignées, supplément
  « + … ×2 », remplacement, choix), note client encadrée.
- Relue à chaque événement de commande, à chaque `resync` (reconnexion, retour au premier plan)
  et chaque minute (une commande programmée entre dans la file sans événement) ; la dernière
  file connue reste affichée si une relecture échoue.
- Commandes « à payer au retrait » à valider (US-E06-3) dans une bande « À valider » au-dessus
  des colonnes, visible seulement s'il y en a : bon complet avec total, touches « Accepter »
  (la commande passe dans « À préparer », sans carillon de la cuisine) et « Refuser » (motif
  obligatoire dans une boîte de dialogue). Liste et carillon d'`ApprovalAlertService`, partagés
  avec le back-office ; le Manager n'a plus à quitter la cuisine.
- Colonne « Prêtes » (`GET /manage/orders?Status=Ready`, par heure de retrait) jusqu'à la
  remise : numéro, heure, prénom, « Payée » ou « À encaisser » avec le total ; trois colonnes
  à partir de 80rem (US-E07-2, décision du 2026-11-26).
- Détail sans quitter l'écran (US-E07-5) : le numéro de chaque bon et chaque carte « Prête »
  ouvrent une fenêtre (`components/kitchen-order-details.ts`) : lignes avec prix, suppléments
  chiffrés, composants de menus, note, total, paiements, reste à encaisser ; relue à
  l'ouverture et à chaque événement, la version déjà à l'écran s'affiche d'abord (hors ligne
  compris). Encaissement déclaratif dans la fenêtre pour toute commande acceptée non
  payée (`shared/ordering/payment-panel.ts`, partagé avec le comptoir ; règle `collect` de
  `shared/ordering/order-actions.ts`), puis « Remis au client » dès qu'elle est prête et payée.
- Une touche par étape : « Commencer », « Prête » ; pas de mise à jour optimiste. Barre
  « N° 12 : Prête — Annuler (9 s) » après chaque geste, proposée 9 s (marge sous les 10 s du
  serveur), refus du serveur affiché (US-E07-2).
- Carillon (Web Audio partagé avec les commandes à valider, `shared/utils/chime.ts`) à
  l'arrivée d'une commande jamais vue, coupable, réglage propre à l'appareil ; annonce aux
  lecteurs d'écran.
- Coupures (US-E07-3) : `ConnectionStatus` et bandeau rouge `ConnectionBanner` ; geste tenté
  sans réseau refusé par un message visible (« le geste n'a pas été enregistré ») ; 409 →
  relecture et message.

**Vérifié contre la vraie API (2026-11-23) :** file affichée ; Commencer, puis Annuler ;
geste fait dans un deuxième onglet KDS répercuté seul dans le premier (SignalR) ; thèmes clair
et sombre.

**Reste :**
- Vérifier : arrivée d'une nouvelle commande pendant l'ouverture (carillon), « Prête » →
  « à remettre » au back-office, coupure réseau réelle 1 min ; tablette en portrait ; axe.
- **Sortie :** 20 commandes simulées traitées sans papier ni rechargement manuel.

## F7 — Pilotage (E09, E10) — 🟡

**Paiement en ligne seul (2026-11-26, [ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) :** tableau de bord sans « Comptoir / En ligne »
ni « Payé sur place » ; fiche client sans compteur de non-venues ni « Réautoriser le paiement au
retrait » (US-E10-4 abandonnée) ; réglage « délai de validation » et « seuil de non-venues »
retirés de la page Restaurant.

**Fait :**
- Tableau de bord (US-E09-1) : CA, commandes, panier moyen, comptoir / en ligne, payé en ligne /
  sur place, commandes par étape (libellés, jamais la couleur seule), TVA du jour par taux ;
  relu à chaque événement de commande et à chaque reconnexion. Aucun chiffre recalculé côté
  client.
- `features/reports` : `/gestion/statistiques`, période jour / semaine / mois / dates libres
  (résolue par l'API) gardée dans l'adresse (`periode`, `du`, `au`, `tranche`, `jour`).
  Ventes par produit (seul / dans un menu), menu et supplément (US-E09-2) ; heures de pointe
  par tranche de 15 ou 30 min, filtre par jour de semaine, barres CSS dans le tableau
  (commandes, CA, préparation moyenne) (US-E09-3). « Tous les jours » additionne les tranches
  et pondère la préparation par les commandes : simple mise en forme.
- `features/staff` : `/gestion/utilisateurs` (recherche nom, e-mail ou téléphone, filtres rôle
  et statut, pagination serveur) (US-E10-1) ; création d'un compte Manager (US-E10-2, créé
  « en attente », à activer depuis sa fiche) ; fiche : statut, Activer / Bannir avec
  confirmation (US-E10-3), compteur de non-venues et « Réautoriser le paiement au retrait »
  (US-E10-4), commandes du client.
- API complétée pour F7 : `FilterUsers.StatusId`, recherche sur le téléphone,
  `UserAdminDetails.NoShowCount` (`GET /users/{id}` renvoie la fiche admin),
  `GET /manage/customers/{userId}/orders`, refus de bannir son propre compte ou le dernier
  Manager actif (tests `UsersApiTests`).
- Identifiants des statuts de compte : `ACCOUNT_STATUS_IDS`
  (`shared/services/user-management.service.ts`), recopiés de `HardCode.ACCOUNT_*` faute de
  route listant les statuts.

**Vérifié contre la vraie API (2026-11-23) :** tableau de bord, ventes et heures de pointe du
mois identiques aux réponses brutes de l'API (une commande comptée) ; liste et fiches
utilisateurs ; « C'est votre compte » sur sa propre fiche ; thèmes clair et sombre.

**Reste :**
- **Sortie :** rejouer la comparaison sur un jeu de plusieurs commandes payées en ligne
  (restaurant ouvert) ; le jeu connu est couvert côté API par `StatsApiTests`.
- Vérifier en navigateur : bannir puis réactiver un client, refus du dernier Manager, création
  d'un Manager puis connexion ; axe sur les trois pages.
- Statuts de compte : une route de l'API listant les statuts éviterait les identifiants
  recopiés.

**→ Sortie J5 :** service simulé de 30 minutes (commandes en ligne payées + KDS + remise) sans
papier.

## F8 — PWA client (E04, E05, E08) — 🟡

Livrée en deux tranches : **F8a** (commander, payer, suivre) puis **F8b** (historique, reçu,
bandeau, pages légales, PWA, appareils réels).

**Paiement en ligne seul (2026-11-26, [ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) :**
- Validation : plus de choix du paiement ; seul « Carte bancaire, en ligne » est proposé, et la
  validation est bloquée avec un message si le paiement en ligne est coupé.
- Suivi : plus de « Payer au retrait » ni de bouton « Annuler la commande ». Pour une commande
  confirmée, en préparation ou prête, la page affiche « Pour annuler, appelez le restaurant par
  téléphone ou demandez directement sur place », avec le numéro en lien `tel:` (depuis
  `/restaurant/info`).
- CGV : section « Annulation et remboursement » complétée de cette procédure.

Les puces ci-dessous sont l'historique d'avant ce changement.

**Fait (F8a) :**
- Note facultative par article (produit ou menu, 200 caractères ; API `CartLineDto.note`,
  `OrderItemDto.note`, migration `AddOrderItemNote`) : saisie `shared/ordering/line-note.ts` sur
  la fiche produit sans options, au récapitulatif de l'assistant d'options et sur la fiche menu ;
  affichée dans le panier, la validation, la cuisine, les commandes du jour, le suivi et le reçu.
- API : refus de panier documenté dans l'OpenAPI (`CartProblemDetails`, `issues` par ligne,
  409 sur `POST /orders/quote` et `POST /orders`) ; `GET /orders/checkout-options` (rôle Client :
  paiement en ligne actif, paiement au retrait possible ou motif du blocage, même règle que la
  validation) ; tests `OrderApiTests`.
- Logique de personnalisation commune au comptoir et à la commande en ligne dans
  `shared/ordering/` : modèle neutre avec adaptateurs carte de gestion / carte publique, bornes,
  libellés, `Cart` (clé d'idempotence), corps de formulaire `app-product-options` et
  `app-menu-slots` (le comptoir garde ses dialogues).
- `shared/services/public-store.service.ts` : carte publique et infos du restaurant en cache,
  carte relue sur `AvailabilityChanged` (`catalog`) et à chaque resynchronisation ; connexion
  temps réel anonyme ouverte par les pages client.
- Accueil branché sur `/restaurant/info` et l'état d'ouverture (horaires de la semaine,
  fermetures à venir, téléphone).
- `features/shop` : carte `/commander` (rubriques, prix TTC, ruptures grisées avec libellé,
  état d'ouverture et délai), fiche produit `/commander/produit/:id` (retraits, remplacements au
  surcoût affiché, prix par le devis ; pas de sélecteur de quantité sur les fiches produit et
  menu, une portion par ajout, petites commandes ; les groupes d'options se choisissent pas à pas
  dans un second dialogue ouvert d'office à l'ouverture de la fiche ; compteur « − n + » à côté
  du prix sur chaque carte de commande : « + » ajoute d'office un produit sans options, sinon
  ouvre la fiche ; « − » retire une unité, ou ouvre le détail de la commande si l'article y est
  en plusieurs lignes différentes, `shared/ordering/options-wizard.ts` : un groupe par étape, Précédent /
  Suivant, Passer pour un groupe facultatif, avance seule après un choix unique, récapitulatif
  où chaque groupe se corrige, vignette du choix s'il a une photo ; même assistant pour un produit
  choisi dans un menu, le comptoir garde l'affichage d'un seul tenant), menu `/commander/menu/:id`, panier
  `/panier` conservé sur l'appareil (`localStorage`, identifiants seulement ; lignes disparues de
  la carte retirées et signalées ; nouvelle clé d'idempotence à chaque modification), TVA et
  total du devis, lignes refusées signalées une à une.
- Validation `/commander/valider` (compte exigé, retour après connexion) : dès que possible si
  ouvert, créneaux du jour, aucun → commande impossible ; paiement en ligne / au retrait selon
  `checkout-options` (motif du blocage affiché) ; note ; 409 avec `issues` → retour au panier ;
  paiement en ligne → redirection vers l'adresse `https` renvoyée par l'API. Au nouveau design :
  étapes numérotées (retrait, paiement, note) à gauche, récapitulatif, total et bouton dans un
  panneau collé à droite sur grand écran (une colonne sur téléphone).
- Suivi `/commandes/:id` (adresse de retour SumUp) : numéro du jour, statut en termes client,
  retrait, total, paiement, contenu ; relu sur `OrderStatusChanged`, à chaque resynchronisation
  et toutes les 5 s tant que le paiement en ligne est attendu ; « Prête » mise en avant
  (bandeau, annonce, vibration) ; payer de nouveau, passer au paiement au retrait, annuler avant
  préparation (confirmation).

**Vérifié contre la vraie API (2026-11-23, Chrome) :** parcours 2 jusqu'à la commande « à
valider » : fiche produit avec retrait, remplacement, sauce et taille (prix du devis exact),
panier conservé au rechargement, connexion exigée puis retour à la validation, créneau choisi
restaurant fermé, commande créée et suivi ouvert ; annulation depuis un deuxième onglet
répercutée seule dans le premier (SignalR) ; compte Manager refusé à la validation ; thèmes
clair et sombre.

**Fait (refonte de la carte au nouveau design, 2026-11-24) :**
- `/commander` reprend le style de l'accueil et de `/a-propos` (surface `.brand`, fond ondulé,
  barre du haut `site-topbar`) ; classe `.brand-ui` (`_order-page.scss`) : les composants de
  l'ancien style posés sur un parfum en prennent l'encre et le fond.
- Rubriques dans un rail vertical à gauche (onglets ARIA verticaux, image de la catégorie ou
  icône, « Menus » en premier) : une seule rubrique affichée à la fois, en cartes.
- Téléphone : bandeau fixe en bas (nombre d'articles et total du devis) qui ouvre le détail de
  la commande dans une feuille. Grand écran (≥ 64rem) : détail toujours affiché dans une
  colonne collée à droite, de hauteur fixe. Dans la colonne comme dans la feuille, seules les
  lignes défilent : TVA, total et « Valider ma commande » restent en bas. Détail commun `features/shop/components/cart-summary`, aussi utilisé
  par `/panier` ; devis unique `data-access/cart-quote.ts`.
- Fiches produit et menu en routes enfants de `/commander`, affichées dans un dialogue par-dessus
  la carte (adresses inchangées ; Échap, Fermer, clic sur le fond et Retour du navigateur
  referment ; navigation sans défilement de la carte).
- Vérifié contre la vraie API (2026-11-24, Chrome, 390, 800 et 1280 px) : rubriques, ajout avec
  taille obligatoire et prix du devis, détail à droite et feuille du bas, « Modifier » rouvre la
  ligne, Échap et Fermer referment sans faire défiler la carte, adresse de fiche ouverte
  directement. axe-core 4.14 (WCAG 2.2 AA + bonnes pratiques) : 0 violation sur la carte (grand
  écran et téléphone, autre rubrique, menu de la barre ouvert), fiche produit ouverte, feuille
  « Votre commande » ouverte et `/panier`, après avoir foncé l'encre du parfum 1 (`#281d3c`,
  4,78:1 même sur les lignes du fond `.waves`) et placé le bandeau d'activation dans une région
  nommée.

**Fait (carte à consulter, 2026-11-24) :**
- Page `/carte` séparée de la page de commande (`features/shop/pages/menu-board-page`) : toutes
  les rubriques à la suite (« Menus » en premier), et une barre collante de liens vers chaque
  rubrique (ancres `#rubrique-…`). Un article ouvre sa fiche de commande
  (`/commander/produit/:id` ou `/commander/menu/:id`). Ni panier ni colonne de commande.
- 2026-11-26 : cartes propres à `/carte` (`components/board-card`), plus grandes que celles de
  `/commander` : deux par ligne sur grand écran (visuel à gauche), une sur mobile (visuel en
  haut) ; description entière, ingrédients (rupture signalée), groupes d'options avec
  suppléments, et pour un menu ses rubriques et produits au choix, visibles sans clic (plus de
  bouton info). Tests et build OK ; rendu navigateur et axe pas encore vérifiés.
- Le lien « Carte » de la barre du haut et les boutons « Voir la carte » (À propos, pages
  légales) mènent à `/carte` ; « Commander » reste sur `/commander`.
- Tests unitaires et build OK ; rendu navigateur et axe pas encore vérifiés.

**Fait (F8b) :**
- Historique `/commandes` (US-E08-3) : plus récente en premier, pages de 20, statut en termes
  client ; lien « Mes commandes » dans l'en-tête pour un client connecté.
- Reçu `/commandes/:id/recu` (US-E05-5) : vendeur (raison sociale, adresse, SIRET, n° TVA),
  commande, lignes, total HT, TVA par taux, TTC, paiements réussis ; imprimable (en-tête et
  boutons masqués). Proposé depuis le suivi pour une commande payée en ligne seulement (sur
  place, le ticket vient de la caisse certifiée). Contenu provisoire (question 2).
- Facture professionnelle (US-E05-6, 2026-11-26) : à la validation, étape « Facture » pour un
  client professionnel, case cochée par défaut (`requestInvoice`) ; sur le reçu, état de la
  demande en termes client (un échec technique reste « en cours de préparation »). **Reste :**
  liste des demandes et relance au back-office (`/manage/invoice-requests`).
- Notifications de suivi (US-E08-2), à la place du bandeau « commande en cours » (2026-11-26 :
  les bandeaux s'empilaient au-dessus de la barre du haut et ne partaient qu'à la clôture de la
  commande) : toast à chaque notification poussée (`shared/ui/toast-stack`, mis en avant à
  « Prête »), cloche avec non-lues et dernières notifications dans `shared/ui/site-topbar`, état
  dans `shared/services/notification-center.service.ts` (relu à chaque resynchronisation).
  Notifications natives (Web Push, US-E08-4) à brancher ensuite sur les mêmes notifications.
- Pages `/cgv`, `/confidentialite`, `/mentions-legales`, `/allergenes` (`features/legal`) :
  identité du vendeur lue sur `/restaurant/info`, rubriques non rédigées signalées « À
  rédiger » et bandeau « texte en cours de rédaction » ; lien « Informations allergènes
  disponibles » sur la carte et chaque fiche produit ; liens dans le pied de page, à la
  validation et à l'inscription. Au nouveau design (coquille `components/legal-document`,
  parfum 2, sommaire à ancres) ; confidentialité (`pages/privacy-page`) et mentions légales
  (`pages/notice-page`) rédigées en texte générique d'après `20-regles-metier/04-conformite-legale.md`
  § 5 ; e-mail, directeur de publication, hébergeur et Instagram dans
  `shared/site-details.ts` (vide = « À compléter »).
- Accueil : section « Contact » au-dessus du pied de page (appeler, écrire, venir avec lien
  d'itinéraire, Instagram ; seuls les moyens renseignés sont affichés).
- PWA (`@angular/service-worker`) : `manifest.webmanifest` (français, `start_url` `/`,
  icônes provisoires monochromes, dont masquables), méta iOS et `theme-color` clair / sombre ;
  `ngsw-config.json` : coquille préchargée, images à la demande, `/api/menu` et
  `/api/restaurant/info` en « fraîcheur » (réseau d'abord, 3 s, 1 jour), **aucune autre route
  `/api` en cache**, `/api` et `/hubs` exclus de la navigation ; bandeau « Une nouvelle version
  est disponible — Recharger », jamais pendant la validation d'une commande ni la saisie
  comptoir.

**Vérifié (2026-11-23, Chrome, build de production servi tel quel) :** service worker actif
en état normal ; seul `/api/menu` dans le cache de données (commandes et état d'ouverture
absents) ; serveur arrêté, la carte se rouvre depuis le cache. Historique et accueil contre la
vraie API. Le service worker ne se teste pas avec `ng serve` (fichiers servis différents des
empreintes de `ngsw.json` : état dégradé) : servir `dist/`.

**Nouveau design — accueil (2026-11-24, en cours) :** premier écran de `/` remplacé par un
carrousel plein écran (`features/home/components/hero-carousel`, géométrie pure dans
`hero-geometry.ts`) : cinq coupes sur un anneau tournant, fond et encre par parfum (jetons
`--flavour-N-*`, contraste AA de 4,68 à 8,73), titre au nom du restaurant, « Commander »,
bouton pause (WCAG 2.2.2), pause d'office avec `prefers-reduced-motion`. Barre du haut du
nouveau design (`shared/ui/site-topbar` : menu, panier) à la place de l'en-tête sur l'accueil
(`data: { header: false }`). Contenu fixe. Reste : images définitives dans
`public/images/home/`, polices auto-hébergées (`@fontsource/dela-gothic-one`,
`@fontsource/archivo`), rendu vérifié dans le navigateur, nombre d'articles sur le bouton
panier (déplacer `CartStore` dans `shared`).

**Nouveau design — écrans de compte (2026-11-24) :** connexion, inscription, mot de passe
oublié, nouveau mot de passe, confirmations et lien d'activation expiré passent au nouveau
design par `shared/ui/auth-layout` : parfum 1 (lilas, `.brand-ui`), fond ondulé, barre du haut,
accroche et assiette à gauche dès 60rem, formulaire dans une carte (`.auth__card`,
`_brand.scss`) ; boutons `.btn-brand`. Image `auth.webp` retirée. Rendu vérifié en Chrome
headless (grand écran, téléphone) ; axe à repasser.

**Nouveau design — back-office (2026-11-24) :** toute la gestion `/gestion` passe au parfum 1
(clair dans les deux thèmes) : la coquille `shared/ui/admin-layout` pose `.brand--1 .brand-ui`
et le fond ondulé, et les pages suivent sans changer de gabarit (classes de `_admin.scss`
redessinées : cartes à bord plein et coins arrondis, voile clair, titres en police d'affiche).
Menu latéral par rubriques (Service, Carte, Réglages), nom du restaurant en tête ; sélecteur de
thème retiré de la barre du haut (sans effet sur un parfum). Tableau de bord recomposé :
« Bonjour Prénom » et date, trois chiffres phares, pastilles par étape, carte « Service ».
`/cuisine` inchangé (lisibilité à 2 m). Rendu et axe à vérifier dans le navigateur.

**Nouveau design — suivi de commande (2026-11-26) :** l'historique `/commandes`, le suivi
`/commandes/:id` (retour SumUp, chemin inchangé) et le reçu `/commandes/:id/recu` reprennent le
gabarit du panier : surface `.brand .brand-ui`, barre du haut, sur-titre et grand titre, contenu
en panneaux `.order-panel`, action principale en `.btn-brand`. Le reçu s'imprime sans fond, sans
vague ni barre du haut. Les bandeaux « compte non activé » et « nouvelle
version » prennent le fond et l'encre de la surface. L'ancien en-tête `shared/ui/site-header`
est supprimé, avec le drapeau de route `data: { header: false }` : chaque page client porte sa
propre `app-site-topbar`. Rendu, impression et axe à vérifier dans le navigateur.

**Photos en plusieurs tailles (2026-11-24) :** l'API décline chaque photo envoyée en WebP 160,
320, 640 et 1200 px (`…-w{taille}.webp`, [ADR-0003](../40-decisions/ADR-0003-stockage-objet.md)) ;
le client fournit un `IMAGE_LOADER` (`core/images/catalog-image-loader.ts`) qui choisit la taille
dans le `srcset` 1x/2x de `NgOptimizedImage` : vignettes du rail et du back-office en 160/320,
cartes en 320/640, fiches en 640/1200. Photos déjà envoyées inchangées (à renvoyer pour en
profiter). Reste : poids des images de `/commander` mesuré dans le navigateur avant/après.

**Reste :**
- Acceptation par le Manager puis cuisine jusqu'à « Prête » vue côté client ; parcours 1 avec
  SumUp en sandbox (clés et URL publique de l'API pour la notification) ; menus (aucun en base
  de dev) ; rendu 360 px et axe sur toutes les pages client.
- Textes des CGV et document des allergènes à rédiger ; politique de confidentialité et mentions
  légales génériques à faire relire (RGPD) ; `shared/site-details.ts` à remplir (questions 6 et
  8) ; identité visuelle et
  icônes définitives (question 14).
- CSP de production : SumUp n'est qu'une redirection (aucun script), `worker-src 'self'`.
- **Sortie (J4) :** parcours 1 et 2 complets sur un iPhone (Safari, installée ou non) et un
  Android (Chrome), application servie en HTTPS.

## F9 — Qualité transverse (en continu) — 🟡

**Fait :** tests unitaires Vitest sur le socle, l'auth et le back-office ; contrôles axe
ponctuels sur l'auth ; budgets de build.

**Fait (navigation en bas sur petit écran, 2026-11-26) :**
- Sous 64rem (téléphone et tablette), barre d'onglets fixée en bas sur toutes les pages client
  (accueil, carte, à propos, commande, panier, validation, suivi, reçu, pages légales) : Accueil,
  Carte, Commander, Panier, Compte (À propos et Gestion pour le Manager). En haut ne restent que
  le logo et la cloche. Pas sur les écrans de compte, `/gestion` ni `/service`.
- L'onglet Compte ouvre le panneau du compte vers le haut ; le pied de page, le bandeau de
  commande de `/commander` et les toasts passent au-dessus de la barre.

**Reste :**
- `strict` + `strictTemplates` dans `tsconfig.json`, ESLint.
- Playwright : parcours auth, saisie carte, comptoir, KDS, commande client ; axe dans chaque
  parcours.
- CSP stricte (dont Google Identity) vérifiée en préproduction.
- Essai régulier contre la vraie API et PostgreSQL (aucun écran n'a encore été validé de bout
  en bout dans un navigateur contre le vrai backend).

## Dette front connue

- Dossier `src/app/design-dev` vide.
- Jeton d'accès en mémoire tant qu'ADR-0002 n'est pas appliquée côté API.

## V2 — côté écran

Voir la [liste V2](01-feuille-de-route.md#v2--après-louverture) ; touchent le front :
allergènes dans la carte (US-E02-8), prévisualisation de la carte (US-E02-7), notification
« c'est prêt » (US-E08-4), vue cuisine agrégée (US-E07-4), recommande (US-E04-7), comparaison
de périodes (US-E09-4).
