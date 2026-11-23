# Feuille de route

**Hypothèses :** développeur solo (back + front), ouverture visée **sous 3 mois** (~12
semaines à partir du 2026-11-23), V1 sans caisse maison
([ADR-0006](../40-decisions/ADR-0006-caisse-certifiee-tierce-v1.md)). Les semaines sont
indicatives ; c'est le **critère de sortie** qui clôt un jalon, pas la date.

Ordre retenu : **backend métier d'abord** (J1–J3, testé), puis **front** (J4–J5), parce que les
règles d'argent et d'états sont le risque principal et que le front s'appuie sur un contrat
d'API stable.

## Vue d'ensemble

| Jalon | Semaines | Objectif | Critère de sortie |
|---|---|---|---|
| **J0 — Socle propre** | S1 | Base saine pour le restaurant | build vert, doc validée, aucun reliquat inutile |
| **J1 — Carte & ouverture** | S2–S3 | Le Manager saisit toute la carte et les horaires par l'API | carte réelle du restaurant saisie via Swagger |
| **J2 — Commande** | S3–S5 | Le cœur : calcul, validation, créneaux, états | tests unitaires R1–R10 + transitions verts |
| **J3 — Paiement SumUp** | S5–S6 | Paiement en ligne sûr, encaissements déclarés | paiement sandbox de bout en bout, rejeu de webhook sans effet |
| **J4 — PWA client** | S6–S8 | Le client commande et suit sa commande | parcours click & collect complet sur mobile |
| **J5 — Back-office & KDS** | S8–S10 | Le restaurant pilote le service | service simulé de 30 min sans papier |
| **J6 — Mise en service** | S10–S12 | Ouvrir en production | recette terrain signée, sauvegarde restaurée, ouverture |

**Jalon de décision G0 (avant la fin de J2)** : réponses de l'expert-comptable sur les
questions 1–3 et 8 ([questions ouvertes](02-questions-ouvertes.md)). Un « non » sur la
caisse tierce ou les allergènes en ligne remet en cause le périmètre V1.

## J0 — Socle propre (S1) — ✅ terminé le 2026-11-22

- Retirer du socle : centre d'aide (`HelpEntry`, `HelpController`, seed), envois groupés
  (`MailBroadcast*`), mode maintenance (middleware, contrôleur, paramètres) ; décider du sort
  de Web Push (question 7).
- Renommer le hub temps réel (`ChatHub` → hub de commandes) et en définir les groupes.
- Statuer sur la date de naissance à l'inscription (US-E01-1).
- Dette immédiate : nom d'image Docker `bonprof` dans les workflows, `ADDRESS_ENCRYPTION_KEY`,
  mise à jour ImageSharp (vulnérabilités), suppression de `bin/Debug/net8.0`.
- Recréer un projet de tests (xUnit) vide branché en CI.
- Mettre à jour `.claude/CLAUDE.md` (fait avec cette doc).
- **Sortie :** build vert, migration à jour, CI exécutant les tests.

**Bilan J0 :** centre d'aide, envois groupés, maintenance (et `PlatformSettings`) retirés ;
Web Push gardé dormant ; date de naissance supprimée, téléphone exposé dans le profil
(`ProfileIncomplete` = téléphone manquant) ; `ChatHub` → `RealtimeHub` (`/hubs/realtime`,
groupe `restaurant`, envoi par utilisateur, jeton accepté en `access_token` sur `/hubs`) ;
image `mahdimcheik/restauranthandler` ; workflow `tests.yml` conditionnant les déploiements ;
projet `tests/RestaurantHandler.Tests` (7 tests) ; `InitialCreate` régénérée (26 tables) et
vérifiée sur PostgreSQL jetable (démarrage, Manager initial, connexion, hub, routes retirées
en 404).

## J1 — Carte & ouverture (S2–S3) — ✅ API terminée le 2026-11-22

- Écarts modèle M6, M7, M9 ([modèle](../30-technique/02-modele-donnees.md#écarts-à-combler)).
- API Manager : catégories, produits (photo), ingrédients, compositions, substitutions,
  suppléments, menus / emplacements / options, archivage, réordonnancement — E02.
- API publique : carte complète en une requête, mise en cache + invalidation — US-E02-1.
- Ruptures ingrédient / produit, règle de disponibilité R6 — US-E03-4.
- Horaires, fermetures, pause, paramètres du restaurant — E03, US-E10-5.
- Calcul de l'état d'ouverture et des créneaux (règle pure + tests).
- **Sortie :** la vraie carte du restaurant est saisie via Swagger ; l'API publique renvoie
  disponibilité et état d'ouverture corrects.

**Bilan J1 :** `Product.IsAvailable`, tables `OpeningHours`, `Closure`, `RestaurantSettings`
(+ historique) dans `InitialCreate` régénérée ; pas de filtre global d'archivage (M9). Règles
pures `AvailabilityPolicy` (R6, retrait imposé — question 10), `OpeningPolicy` (fuseau
`Europe/Paris`, changements d'heure), `SlotPolicy` (capacité en commandes, jour même —
question 5), `OpeningHoursPolicy` : 35 tests unitaires. API Manager sous `/manage/*`
(catégories, produits, photos WebP publiques, ingrédients, composition, menus, emplacements,
options, archivage, réordonnancement, horaires, fermetures, pause, paramètres) ; API publique
`GET /menu`, `/restaurant/status`, `/restaurant/slots`, `/restaurant/info` ; cache invalidé à
chaque écriture ; hub ouvert aux anonymes. Vérifié sur PostgreSQL jetable (scénario de saisie
complet, ruptures, pause, fermeture, droits 401/403). **Reste pour la sortie :** saisir la vraie
carte du restaurant.

## J2 — Commande (S3–S5) — ✅ API terminée le 2026-11-22

- Écarts M1, M2, M3, M8.
- `Domain/Policies/` : calcul des prix (R2), ventilation TVA (R1), validation des
  personnalisations (R4) et des menus (R5), transitions (machine à états), capacité des
  créneaux (R7). **Tests unitaires d'abord.**
- Validation de commande (en ligne et comptoir) avec revérification complète, idempotence
  (R10), capacité sous verrou.
- Transitions Manager / cuisine, historique avec motif, diffusion temps réel.
- Tâches Hangfire : refus automatique des `AValider`, reprise de pause.
- Tests d'intégration (PostgreSQL Testcontainers) : concurrence sur un créneau, permissions.
- **Sortie :** tous les cas de [tests obligatoires](../30-technique/05-tests.md) hors paiement
  passent.

**Bilan J2 :** écarts M1, M2, M3, M8 faits, et M4 en partie (encaissements sur place déclarés,
paiement mixte, garde « payée » avant `Retirée`, avancés depuis J3). Règles pures
`CartPolicy` (R2–R5, ruptures), `VatPolicy` (R1, taux unique du menu en attendant G0),
`OrderTransitionPolicy` (machine validée, question 4) : 52 tests unitaires. Validation en ligne
et comptoir sous verrou `pg_advisory_xact_lock` (capacité, numéro du jour — question 11 —,
idempotence), devis du panier, refus détaillé par ligne, limite de 10 validations par minute et
par utilisateur, refus automatique Hangfire, transitions Manager et cuisine avec annulation de
geste, suivi client, file KDS, temps réel. 13 tests d'intégration (Testcontainers) : prix
client ignoré, rupture entre panier et validation, concurrence sur la dernière place, rejeu,
isolement entre clients, permissions, garde « payée », snapshots, pause, non-venues. Paiement
en ligne SumUp (statut `PendingPayment` créé sans checkout) : J3.

## J3 — Paiement SumUp (S5–S6) — 🟡 intégration faite, sandbox réel à passer

- Compte marchand SumUp en mode test, clés en variables d'environnement.
- Écarts M4, M5. `Integrations/SumUp/` : création de checkout, relecture, remboursement.
- Notification SumUp : relecture, idempotence, contrôle du montant, anomalies.
- Réconciliation + expiration des commandes non payées (Hangfire).
- Encaissements déclarés sur place (mixte) ; garde « payée » avant `Retirée`.
- Page reçu des paiements en ligne (contenu selon réponse question 2).
- **Sortie :** paiement sandbox de bout en bout, notification rejouée sans effet, remboursement
  testé.

**Bilan J3 (2026-11-22) :** API SumUp vérifiée sur la documentation officielle (notifications
non signées). Écarts M4 et M5 faits. `Integrations/SumUp/SumUpClient` (checkout hébergé, lecture,
désactivation, remboursement total) ; règle pure `OnlinePaymentPolicy` (12 tests) ; service de
paiement : création du checkout à la validation, nouvelle tentative, bascule au retrait
(transition ajoutée), notification relue et sérialisée par commande, réconciliation et
expiration Hangfire, remboursement automatique à l'annulation et des paiements tardifs,
anomalies signalées au Manager, reçu. 10 tests d'intégration contre un faux SumUp au format de
l'API. **Reste pour la sortie :** le même parcours en sandbox SumUp réel (procédure dans
[exploitation](../30-technique/03-exploitation.md#configuration)).

## J4 — PWA client (S6–S8)

- Projet Angular PWA (manifest, service worker), client API généré, auth (mot de passe +
  Google).
- Carte, fiche produit avec personnalisation, composition de menu, panier persistant.
- Choix ASAP / créneau, validation, paiement en ligne ou au retrait.
- Suivi temps réel, bandeau de commande en cours, historique et reçu.
- Pages légales (CGV, confidentialité, mentions, lien allergènes).
- **Sortie :** parcours 1 et 2 complets sur un iPhone et un Android réels.

## J5 — Back-office & KDS (S8–S10) — 🟡 API terminée le 2026-11-22, écrans à faire

- Back-office tablette : commandes du jour, validation `AValider` (son + badge), saisie
  comptoir rapide, encaissement déclaré, remise, non venu, annulation.
- KDS plein écran : file triée, gestes, annulation de geste, son, resynchronisation.
- Gestion de la carte, ruptures, pause, horaires, paramètres (écrans sur l'API de J1).
- Statistiques : tableau du jour, ventes par produit, heures de pointe (écart M10).
- **Sortie :** service simulé de 30 minutes (comptoir + en ligne + KDS) sans papier.

**Bilan J5 (API, 2026-11-22) :** J4 sauté pour l'instant, front non créé : seule la partie serveur
est faite. Règle pure `StatsPolicy` (commande comptée = retirée, ou prête et payée, à son
instant de retrait ; journées en heure de Paris ; 18 tests) et routes Manager
`/manage/stats/dashboard`, `/products`, `/peak-hours` (US-E09-1 à 3). Écart M10 (index
`Status + CompletedAt` / `ReadyAt`). Recherche par numéro du jour (`GET /manage/orders?number=`),
levée du blocage non-venues historisée (`NoShowResets`) et compteur exposé au Manager dans le
détail de commande. `InitialCreate` régénérée (30 tables). 2 nouveaux tests d'intégration (25 au
total). **Reste pour la sortie :** les écrans back-office, KDS et statistiques, puis le service
simulé.

**Groupes d'options (API, 2026-11-22) :** la carte réelle (sandwichs, pizzas) exige des choix
bornés que la composition ne sait pas exprimer : groupes d'options par produit (US-E02-9, écart
M12) — pain 1/1, sauces 0 à 3 avec choix offerts puis payants, taille de pizza avec surcoût.
Règle pure `OptionChoicePolicy` (R2/R4/R6, 16 tests), `PUT /manage/products/{id}/option-groups`,
`optionGroups` dans la carte publique, `optionChoiceIds` dans les lignes du panier (produit seul
ou composant de menu). `InitialCreate` régénérée (32 tables) ; 5 nouveaux tests d'intégration
(30 au total). Écran back-office et sélection côté client à faire avec les écrans.

**Groupes d'options partagés (API, 2026-11-23) :** un même groupe (« Sauces », « Pain ») sert à
plusieurs produits : `OptionGroup` / `OptionChoice` sans produit, liaison ordonnée
`ProductOptionGroup`. Routes `/manage/option-groups` (CRUD, archivage refusé tant qu'un produit
le propose, images du groupe et des choix) ; `PUT /manage/products/{id}/option-groups` ne prend
plus que `{ optionGroupIds }`. Carte publique et commande inchangées pour le client.
`InitialCreate` régénérée ; 4 nouveaux tests d'intégration (34 au total).

**Quantités par choix et bornes par produit (API, 2026-11-23) :** pizzas « 3 ingrédients » et
« 5 ingrédients » avec un ingrédient pris deux fois. `OptionGroup.MaxPerChoice`, bornes
facultatives sur `ProductOptionGroup` (`rules` dans `PUT /manage/products/{id}/option-groups`,
`productRules` dans le détail), panier en `optionChoices: [{ choiceId, quantity }]` au lieu de
`optionChoiceIds`, `maxPerChoice` dans la carte publique. Migration `OptionChoiceQuantities` ;
8 nouveaux tests unitaires, 1 test d'intégration (35 au total).

**Remplacements au prix de l'ingrédient (API, 2026-11-23,
[ADR-0012](../40-decisions/ADR-0012-remplacements-prix-ingredient.md)) :** le Manager ne déclare
plus de remplaçants ni de suppléments par produit. `Ingredient.PriceCents` et
`IsOfferedAsReplacement` ; un ingrédient retirable se remplace par tout ingrédient proposé, au
surcoût `max(0, écart)` ; suppléments en groupe d'options facultatif. `IngredientSubstitution`
et `ProductSupplement` supprimés (migration `IngredientReplacementPrices`), `replacements` dans
la carte publique, statistiques des suppléments étendues aux choix payants. 154 tests unitaires,
36 tests d'intégration.

## J6 — Mise en service (S10–S12)

- Production : VPS, domaine, TLS, variables, SumUp en mode réel, sauvegardes + test de
  restauration, supervision.
- Test de charge d'un pic de service.
- Recette terrain avec l'équipe sur les appareils réels ; procédure de mode dégradé testée
  (bascule 4G).
- Formation de l'équipe (1 h) ; affichage de la procédure papier.
- Ouverture progressive possible : comptoir + KDS d'abord, click & collect une semaine après.
- **Sortie :** ouverture, puis gel des déploiements pendant les services.

## V2 — après l'ouverture

Par ordre de valeur estimée (à reprioriser après un mois d'exploitation) :

1. Caisse maison certifiée NF525 + imprimante ePOS/WebPRNT (supprime la double saisie).
2. Allergènes dans la carte (US-E02-8).
3. Notification push « c'est prêt » (US-E08-4).
4. Rôle équipier distinct du gérant.
5. Vue cuisine agrégée (US-E07-4), délai ASAP calculé selon la charge.
6. Recommande (US-E04-7), comparaison de périodes (US-E09-4), prévisualisation de carte.
7. Codes promo, fidélité, export comptable.
