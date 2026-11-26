# Règles de commande

Ces règles sont **pures** (sans base ni web) et vivent dans `Domain/Policies/` (cf.
[ADR-0001](../40-decisions/ADR-0001-archi-backend.md)) : chacune est couverte par des tests
unitaires.

## R1 — Montants

- Tous les montants sont des **entiers en centimes d'euro** (`*Cents`), jamais des flottants.
- Les prix de la carte sont **TTC**. Le HT et la TVA se déduisent par taux :
  `HT = round(TTC × 100 / (100 + taux))`, `TVA = TTC − HT`, arrondi au centime le plus proche,
  calculé **par taux sur le total de la commande** (pas ligne par ligne) pour éviter les
  écarts d'arrondi.
- Devise unique : EUR.

## R2 — Le total est calculé par le serveur

Le client envoie **ce qu'il veut** (produits, menus, choix, personnalisations, quantités),
jamais un prix. Le serveur calcule :

```
prix d'une ligne produit  = (prix produit
                             + Σ surcoûts de remplacement
                             + Σ prix des groupes d'options) × quantité
prix d'un groupe d'options = Σ surcoût du choix × quantité
prix d'une ligne menu     = (prix menu
                             + Σ surcoûts d'option d'emplacement
                             + Σ personnalisations des produits choisis) × quantité
total commande            = Σ lignes de premier niveau
```

Un retrait d'ingrédient (« sans X ») est **toujours gratuit** et ne fait jamais baisser le prix.

## R3 — Snapshots

À la validation, chaque ligne copie **nom, prix unitaire, taux de TVA** ; chaque
personnalisation copie son **libellé et son prix**. Toute lecture ultérieure (suivi,
historique, reçu, statistiques) utilise ces copies, jamais la carte courante.

## R4 — Validité d'une personnalisation

| Type | Autorisé si | Prix |
|---|---|---|
| Retrait | l'ingrédient est dans la composition **et** `IsRemovable` | 0 |
| Remplacement | l'ingrédient est dans la composition **et** `IsRemovable` ; le remplaçant est un autre ingrédient non archivé, disponible et `IsOfferedAsReplacement` | `max(0, prix du remplaçant − prix du remplacé)` ([ADR-0012](../40-decisions/ADR-0012-remplacements-prix-ingredient.md)) |
| Supplément | refusé : les suppléments sont des choix d'un groupe d'options facultatif | — |

| Choix d'option | le choix appartient à un groupe actif du produit, n'est pas archivé, son ingrédient éventuel est disponible, quantité entre 1 et `MaxPerChoice` du groupe | surcoût du choix × quantité |

Un même ingrédient ne peut pas être à la fois retiré et remplacé dans une ligne.

Pour chaque groupe d'options actif du produit : `MinChoices ≤ unités ≤ MaxChoices`, où les
unités sont la somme des quantités (« double jambon + œuf » = 3 unités). Un choix figure une
seule fois dans la requête, avec sa quantité ; `MaxPerChoice = 1` (par défaut) revient à des
choix distincts. Les bornes sont celles du groupe, sauf si le produit les
redéfinit (même liste « Ingrédients » : 3 sur une pizza, 5 sur une autre).

Seul le surcoût propre d'un choix est facturé, à chaque fois qu'il est pris : un groupe n'a ni
choix offerts ni prix par choix supplémentaire, et le maximum suffit à borner les quantités.
Chaque choix est une personnalisation `Choice` de libellé « Groupe : Choix » (« Sauces :
Algérienne »), de quantité et de prix unitaire égal à son surcoût, dans l'ordre de la carte.

## R5 — Validité d'un menu

- Pour chaque emplacement : `MinChoices ≤ nombre de choix ≤ MaxChoices`.
- Chaque choix est une `MenuSlotOption` de cet emplacement, avec un produit actif et disponible.
- Les choix sont des lignes enfants (`ParentItemId`) qui portent le surcoût d'option comme prix
  unitaire et leurs propres personnalisations.
- TVA : en attendant la réponse à la question 3, la ligne menu et ses composants portent le
  **taux du menu**.

## R6 — Disponibilité

Un produit est commandable si : actif, non archivé, catégorie active, non marqué
indisponible, tous ses ingrédients **non retirables** disponibles et, pour chaque groupe
d'options, `choix disponibles × MaxPerChoice ≥ MinChoices` (plus aucun pain → sandwich indisponible). Un menu est commandable si
actif et si chaque emplacement à minimum > 0 a au moins une option commandable.

## R7 — Ouverture et retrait

- Une commande en ligne exige : restaurant non fermé à l'heure de retrait, commandes non en
  pause (pour ASAP et pour toute validation pendant la pause), créneau non plein.
- La capacité d'un créneau est vérifiée à la validation **sous verrou** (transaction
  sérialisable ou verrou applicatif sur le créneau) pour qu'aucune commande concurrente ne la
  dépasse.
- Toute commande se passe en ligne ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) :
  il n'y a plus de saisie au comptoir.
- Toute commande active dont l'heure de retrait tombe dans un créneau l'occupe :
  la capacité protège la cuisine. Les créations de commandes sont sérialisées par un verrou
  `pg_advisory_xact_lock` (capacité, numéro du jour, idempotence).
- Un ingrédient retirable en rupture doit figurer en retrait (ou remplacement) dans la ligne :
  sinon la commande est refusée et le panier mis à jour.

## R8 — Numérotation

- `OrderNumber` est une séquence globale attribuée par la base (identity Postgres), unique.
- Le numéro **affiché** est `DailyNumber` : 1, 2, 3… par jour de service (heure de Paris),
  unique par jour (question 11, tranchée le 2026-11-22). La référence technique reste
  `OrderNumber`.

## R9 — Paiement

- Seul moyen de paiement : la **carte bancaire en ligne** via SumUp ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)).
  Une commande est **payée** quand un paiement en ligne `Successful` couvre le total ; elle
  n'entre en cuisine (`Confirmée`) qu'à ce moment.
- Le montant confirmé par SumUp doit être **exactement** le total ; tout écart met la commande
  en anomalie (pas de transition automatique).
- Une commande non payée ne peut pas être `Retirée` (garde-fou : toute commande en cuisine est
  déjà payée).
- Le client n'annule pas lui-même : il appelle le restaurant ou le demande sur place ; seul un
  Manager annule, et un paiement en ligne est alors remboursé.

## R10 — Idempotence

- La validation d'un panier porte une **clé d'idempotence** générée par le client ; un rejeu
  avec la même clé renvoie la commande déjà créée.
- Chaque notification SumUp est traitée au plus une fois (identifiant de transaction unique).
  Les notifications SumUp **ne sont pas signées** : elles déclenchent seulement une relecture
  du checkout, sérialisée par commande (`pg_advisory_xact_lock`) ; la décision est la règle pure
  `OnlinePaymentPolicy` (confirmer, échec de tentative, anomalie, remboursement d'un paiement
  arrivé sur une commande qui ne l'attendait plus).
