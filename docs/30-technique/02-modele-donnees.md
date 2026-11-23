# Modèle de données

Source : [`Catalog.cs`](../../RestaurantHandler/Domain/Entities/Catalog/Catalog.cs),
[`Ordering.cs`](../../RestaurantHandler/Domain/Entities/Ordering/Ordering.cs),
[`Restaurant.cs`](../../RestaurantHandler/Domain/Entities/Restaurant/Restaurant.cs), configurations dans
`RestaurantHandler/Persistence/Configurations/`, migration unique `InitialCreate`.

## Conventions

- Entités à vie longue : héritent de `BaseModel` (Guid, `CreatedAt`, `UpdatedAt`, `ArchivedAt`).
  Le catalogue est **archivé**, jamais supprimé, car les commandes y pointent.
- Tables de liaison à clé composite (`ProductIngredient`, `ProductSupplement`,
  `MenuSlotOption`, `ProductOptionGroup`) : sans `BaseModel`.
- Enums stockés en texte. Montants en `int` (centimes). TVA en `decimal(5,2)`.
- Contraintes posées en base : unicités, prix ≥ 0, `Quantity > 0`, `MinChoices ≤ MaxChoices`,
  une ligne = produit **ou** menu, `SubstituteIngredientId` renseigné ssi `Replace`,
  `OptionChoiceId` renseigné ssi `Choice`.
- Utilisateurs : `UserApp` (Identity, table `Users`), rôle via `RoleApp`.
- Archivage : **pas de filtre global** ; chaque service filtre `ArchivedAt == null`
  explicitement, pour que les commandes relisent librement leurs produits archivés (M9). Les
  unicités de nom (catégorie, ingrédient) et de position d'emplacement ne portent que sur les
  lignes non archivées.

## Carte

| Table | Rôle | Points clés |
|---|---|---|
| `Category` | Rubrique de carte | nom unique, `Position`, `IsActive` |
| `Product` | Plat / boisson / dessert | `Kind`, `PriceCents`, `VatRate`, `Position`, `IsActive`, `IsAvailable` (rupture) |
| `Ingredient` | Ingrédient | nom unique, `IsAvailable` (rupture), `PriceCents` (référence pour les remplacements, ≥ 0), `IsOfferedAsReplacement` |
| `ProductIngredient` | Composition | PK (produit, ingrédient), `IsRemovable` (retirable et remplaçable) |
| `OptionGroup` | Groupe d'options partagé entre produits (pain, sauces, taille) | `Label` unique (non archivé), `ImageUrl`, `Min/MaxChoices` et `FreeChoices` (en unités), `ExtraChoicePriceCents`, `MaxPerChoice` (≥ 1, fois qu'un même choix peut être pris) ; archivé, refusé tant qu'un produit le propose |
| `OptionChoice` | Choix d'un groupe | nom unique par groupe (non archivé), `ImageUrl`, `IngredientId?` (rupture), `ExtraPriceCents`, `Position` ; archivé |
| `Menu` | Formule | `PriceCents`, `VatRate`, `Position`, `ImageUrl`, `IsActive` |
| `MenuSlot` | Emplacement | `Label`, position unique par menu, `Min/MaxChoices` |
| `ProductOptionGroup` | Groupe proposé sur un produit | PK (produit, groupe), `Position` (ordre dans le produit), `Min/Max/FreeChoices?` propres au produit (tous nuls = ceux du groupe) ; supprimé avec le produit, jamais avec le groupe |
| `MenuSlotOption` | Produit proposé | PK (emplacement, produit), `ExtraPriceCents` |

## Restaurant

| Table | Rôle | Points clés |
|---|---|---|
| `OpeningHours` | Plage hebdomadaire | jour, `Start` < `End` en heure locale, pas de passage de minuit |
| `Closure` | Fermeture exceptionnelle ou partielle | `StartLocal`/`EndLocal` en heure locale (sans fuseau), `Reason` affiché |
| `RestaurantSettings` | Paramètres (ligne unique) | identité légale, logo, délais, créneaux (capacité en commandes), `IsPaused` + `PausedUntil` |
| `RestaurantSettingsHistory` | Historique des paramètres | auteur, date, instantané `jsonb` |

## Commandes

| Table | Rôle | Points clés |
|---|---|---|
| `Order` | Commande | `OrderNumber` (identity), `ServiceDate` + `DailyNumber` (unique), `IdempotencyKey` (unique par auteur), `CustomerId?`, `CustomerFirstName?`, `CreatedByUserId`, `Channel`, `Status`, `PickupMode`, `PickupAt`, `PaymentChoice`, `TotalCents`, `PaidAt/ConfirmedAt/ReadyAt/CompletedAt`, `PaymentIssue` (anomalie à traiter), jeton `xmin` |
| `OrderItem` | Ligne | produit **ou** menu ; composants de menu en lignes enfants ; snapshots nom/prix/TVA |
| `OrderItemModification` | Personnalisation | `Remove` / `Replace` / `Supplement` / `Choice`, libellé et prix figés, `Position` ; `OptionChoiceId` ssi `Choice`, `IngredientId` obligatoire sauf pour un `Choice` |
| `Payment` | Paiement en ligne ou encaissement sur place déclaré | `Method` (`SumupOnline`, `OnSiteCard`, `OnSiteCash`, `OnSiteOther`), `Status`, `AmountCents`, identifiants SumUp uniques (`SumupCheckoutId`, `SumupTransactionCode`, `SumupTransactionId`), `CheckoutUrl`, `RefundedAt`, `DeclaredByUserId` |
| `OrderStatusHistory` | Journal des statuts | auteur, horodatage, `Reason` |

## Écarts à combler

| # | Écart | Pourquoi | Jalon |
|---|---|---|---|
| M1 | ✅ `OrderStatus` : remplacer `Paid` par `Confirmed` ; ajouter `AwaitingApproval`, `Refused`, `NoShow` | [machine à états](../20-regles-metier/02-machine-etats-commande.md) | J2 |
| M2 | ✅ `OrderStatusHistory.Reason` | motifs de refus / annulation | J2 |
| M3 | ✅ `Order` : mode de retrait (ASAP / créneau), `PickupAt` (heure prévue), mode de paiement choisi (en ligne / au retrait), `IdempotencyKey` unique, `CustomerFirstName` facultatif (comptoir) | E04, E06 | J2 |
| M4 | ✅ `PaymentMethod` : distinguer paiement en ligne SumUp et **encaissements sur place déclarés** (carte, espèces, autre) ; un encaissement déclaré porte l'auteur | paiement mixte, [US-E05-3](../10-cahier-des-charges/E05-paiement.md#us-e05-3--marquer-une-commande--payée--sur-place) | J3 |
| M5 | ✅ `Payment` : identifiant de transaction SumUp unique (idempotence), `Refunded` + date | E05 | J3 |
| M6 | ✅ `Product.IsAvailable` (rupture produit, distincte de `IsActive`) | [US-E03-4](../10-cahier-des-charges/E03-ouverture-ruptures.md#us-e03-4--déclarer-une-rupture) | J1 |
| M7 | ✅ Nouvelles tables : `OpeningHours` (jour, début, fin), `Closure` (début, fin, motif), `RestaurantSettings` (identité légale, délais, créneaux, pause jusqu'à) | E03, E10 | J1 |
| M8 | ✅ `UserApp.NoShowCount` : compteur de non-venues / blocage paiement au retrait | [US-E04-5](../10-cahier-des-charges/E04-commande-en-ligne.md#us-e04-5--limiter-les-abus-du-paiement-au-retrait) | J2 |
| M9 | ✅ Filtre automatique `ArchivedAt == null` : **non rétabli**, les services filtrent explicitement | lecture de commandes anciennes | J1 |
| M10 | ✅ Index de lecture pour les statistiques : `Order(Status, CompletedAt)` et `Order(Status, ReadyAt)` ; table `NoShowResets` (historique des levées de blocage, US-E10-4) | E09, E10 | J5 |
| M11 | Socle : retirer `HelpEntry`, `MailBroadcast`, paramètres de maintenance | J0 | J0 |
| M12 | ✅ Groupes d'options, partagés entre produits depuis le 2026-11-23 : `OptionGroup`, `OptionChoice`, liaison `ProductOptionGroup`, `ModificationType.Choice`, `OrderItemModification.OptionChoiceId` et `Position` | sandwichs (pain, sauces), pizzas (taille) — [US-E02-9](../10-cahier-des-charges/E02-carte.md#us-e02-9--définir-les-groupes-doptions-et-les-proposer-sur-les-produits) | J5 |

Chaque écart est une migration nommée (`dotnet ef migrations add <Nom>`) ; plus de
régénération d'`InitialCreate` une fois la première mise en production faite.
