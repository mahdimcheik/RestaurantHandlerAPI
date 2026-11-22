# Règles métier

> Statut : **Stable** — toute valeur ci-dessous est une **constante nommée**, définie à un
> seul endroit dans le code ([`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs)). Aucune de ces valeurs ne doit apparaître en dur ailleurs.
>
> Les colonnes et mentions **Code** / **État** disent où en est BonProfAPI. Les écarts sont
> repris dans la [dette technique](../50-backlog/03-dette-technique.md).

## 1. Constantes de plateforme

| Constante | Valeur | Rôle | Code |
|---|---|---|---|
| `PLATFORM_COMMISSION_RATE` | `0,05` | Part que la plateforme retient sur chaque réservation. Le professeur est crédité du reste. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `CHECKOUT_WINDOW` | 30 min | Délai laissé à l'élève pour payer une commande. Pilote l'expiration de la session Stripe, le compte à rebours affiché et le balayage de nettoyage : les trois sont toujours d'accord. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `DISPUTE_WINDOW` | 2 jours + 2 h | Fenêtre de contestation **et** délai de versement. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `TEACHER_RESPONSE_WINDOW` | 48 h | Délai laissé au professeur pour accepter ou refuser une réservation en flux sans paiement. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `LATE_CANCELLATION_WINDOW` | 48 h | En deçà, l'annulation de l'élève devient « tardive ». | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `TEACHER_CANCELLATION_FEE_RATE` | `0,05` | Pénalité prélevée sur le professeur qui annule un cours payé. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `VAT_RATE` | `0,20` | TVA appliquée sur la facture. Les prix stockés sont TTC. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `FEATURED_TEACHERS_COUNT` | 10 | Professeurs affichés dans le bandeau « mis en avant ». | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `FEATURED_TEACHERS_POOL_SIZE` | 30 | Vivier des mieux notés dans lequel on tire les 10. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `DEFAULT_LANGUAGE_CODE` | `"fr"` | Langue de repli des libellés traduisibles. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `DEBT_TRANSACTION_TYPES` | `PENALTY`, `CLAWBACK` | Les **seuls** types d'écriture qui constituent une dette du professeur. Voir [I8](#i8--cest-le-type-pas-le-signe-qui-fait-la-dette). | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) — les identifiants y sont recopiés au lieu de réutiliser les constantes `TYPE_TEACHER_TRANSACTION_*` |
| `MESSAGE_MAX_LENGTH` | 2 000 caractères | Plafond d'un message de conversation à la saisie. | **absente** |
| `CONTACT_MAX_PER_WINDOW` | 3 | Demandes de contact recevables par couple élève-professeur sur la fenêtre. | **absente** — aucun plafond de contact |
| `CONTACT_WINDOW` | 24 h | Fenêtre du plafond ci-dessus. | **absente** |
| `VALIDATION_CODE_MAX_ATTEMPTS` | 5 | Essais erronés de code de présence avant verrouillage temporaire. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `VALIDATION_CODE_LOCKOUT` | 15 min | Durée du verrou. La date de déverrouillage est renvoyée à l'appelant. | en dur : variable locale `lockout` dans [`PresenceService`](../../RestaurantHandler/Services/Reservations/PresenceService.cs) |
| `DISPUTE_RESOLUTION_WINDOW` | 7 jours | Au-delà, un litige que personne ne traite est tranché en faveur du professeur. | **absente** — voir [ADR-0011](../40-decisions/ADR-0011-resolution-auto-litige.md) |
| `DISPUTE_ESCALATION_DELAY` | 5 jours | Ancienneté à laquelle l'administration est relancée — **avant** l'échéance ci-dessus. | **absente** |
| `REVIEW_COMMENT_MAX_LENGTH` | 2 000 caractères | Plafond du commentaire d'un avis, à la saisie. | **absente** |
| `BOARD_TITLE_MAX_LENGTH` | 64 caractères | Plafond du titre d'un tableau de suivi et d'une liste. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `LIST_MAX_PER_BOARD` | 20 | Nombre maximal de listes dans un tableau de suivi. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `CARD_TITLE_MAX_LENGTH` | 128 caractères | Plafond du titre d'une carte. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) |
| `CARD_DESCRIPTION_MAX_LENGTH` | 10 000 caractères | Plafond de la description d'une carte, à la saisie. | [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) — mesuré sur le texte par [`RichTextSanitizer`](../../RestaurantHandler/Utilities/RichTextSanitizer.cs) |

### Pourquoi ces valeurs, et pas d'autres

**`CHECKOUT_WINDOW` = 30 min.** C'est le **minimum imposé par Stripe** sur l'expiration
d'une session de paiement (maximum : 24 h). Une valeur plus courte est impossible. Elle
sert simultanément de durée de blocage du créneau : tant que la commande est en attente,
le créneau n'est plus réservable par un autre élève.

**`DISPUTE_WINDOW` couvre deux besoins avec une seule valeur, volontairement.** Tant que
la fenêtre de contestation est inférieure ou égale au délai de versement, **un litige ne
peut jamais arriver après un versement**. C'est ce qui évite d'avoir à récupérer de
l'argent déjà parti chez le professeur dans le cas nominal. Les 2 heures supplémentaires
sont une marge qui absorbe le délai de règlement Stripe : un transfert exige des fonds
effectivement disponibles sur le solde de la plateforme.

> **Conséquence à ne pas casser :** si un jour la fenêtre de litige devient plus longue que
> le délai de versement, il faut concevoir la récupération (`CLAWBACK`) comme un cas
> nominal et non comme une exception.

**`TEACHER_CANCELLATION_FEE_RATE` est distincte de `PLATFORM_COMMISSION_RATE` malgré une
valeur identique.** Ce sont deux règles indépendantes : l'une peut évoluer sans l'autre.
Les confondre dans le code créerait un couplage invisible.

**`LATE_CANCELLATION_WINDOW` = 48 h** aligne le seuil d'annulation tardive sur la fenêtre
de litige : l'élève dispose du même ordre de grandeur des deux côtés du cours.

**`MESSAGE_MAX_LENGTH` = 2 000, comme le message d'une notification.** Un message de
conversation en produit une : les aligner garantit qu'un extrait tient toujours dans le
panneau, sans troncature muette. La colonne, elle, est plus large — le balisage conservé par
le nettoyage compte dans la longueur stockée, pas dans celle qu'on impose à l'auteur.

**`CONTACT_MAX_PER_WINDOW` se compte par couple, jamais globalement.** Un plafond par élève
l'empêcherait de contacter plusieurs professeurs ; un plafond par professeur le punirait du
comportement d'un tiers. Trois messages en 24 h suffisent à poser une question et à relancer ;
au-delà, c'est du harcèlement. Cette limite métier double la limitation de débit de bordure,
qui compte les requêtes d'une origine et non les écritures d'un compte vers un autre.

**`DISPUTE_RESOLUTION_WINDOW` = 7 jours, et `DISPUTE_ESCALATION_DELAY` strictement en deçà.**
Sans échéance, un litige que personne n'ouvre gèle l'argent d'un professeur indéfiniment. Sept
jours laissent le temps d'arbitrer tout en bornant l'immobilisation, et la relance à cinq jours
doit tomber **avant** la décision : une administration prévenue après coup n'a plus rien à
arbitrer. Le défaut est « en faveur du professeur », comme partout ailleurs dans ce modèle —
trancher pro élève donnerait un cours gratuit à qui se tait. Voir
[ADR-0011](../40-decisions/ADR-0011-resolution-auto-litige.md).

**`VALIDATION_CODE_MAX_ATTEMPTS` et `VALIDATION_CODE_LOCKOUT` ne protègent pas de l'argent.**
Le code de présence n'ouvre aucun droit : ne pas le saisir ne bloque rien. Le verrou existe pour
qu'un code deviné par énumération ne produise pas un horodatage **faux** — c'est cet horodatage,
et lui seul, qui sert en cas de litige. Voir
[ADR-0012](../40-decisions/ADR-0012-code-de-presence.md).

**`REVIEW_COMMENT_MAX_LENGTH` = 2 000, comme un message.** Même raison : un extrait doit tenir
dans le panneau sans troncature muette. La colonne, elle, est plus large.

**`CARD_DESCRIPTION_MAX_LENGTH` = 10 000, cinq fois un message.** Une carte décrit un
exercice ou un plan de travail, pas une réplique : elle doit pouvoir porter une liste, un
énoncé, quelques liens. Comme pour `MESSAGE_MAX_LENGTH`, le plafond porte sur le texte saisi
et la colonne est plus large pour le balisage conservé par le nettoyage.

**`LIST_MAX_PER_BOARD` = 20.** Au-delà, un tableau ne se lit plus d'un coup d'œil, même en
défilement horizontal : c'est le signe qu'il faut un second tableau. Le plafond borne aussi le
coût d'un réordonnancement, qui réécrit toutes les positions du tableau.

**`VAT_RATE` est un taux plat pour l'instant.** À affiner selon le régime fiscal réel de
chaque professeur (franchise en base de TVA notamment). Voir
[questions ouvertes](../50-backlog/02-questions-ouvertes.md).

## 2. Invariants — les règles qui ne doivent jamais être violées

### I1 — Une commande porte sur un seul professeur

**État :** Appliqué — `Order.TeacherId`, `GetActiveOrder` dans [`ReservationCartService`](../../RestaurantHandler/Services/Reservations/ReservationCartService.cs).

Toutes les réservations d'une commande appartiennent au même professeur. Un élève qui
réserve chez plusieurs professeurs a **plusieurs commandes en attente simultanées**, une
par professeur, payées séparément.

*Pourquoi :* l'argent se répartit entre plusieurs vendeurs. Une commande mono-professeur
garde la relation encaissement ↔ commande en 1:1 et rend chaque paiement attribuable sans
calcul. Voir [ADR-0003](../40-decisions/ADR-0003-commande-mono-prof.md).

### I2 — Les prix sont figés à la réservation, jamais recalculés

**État :** Appliqué — instantané posé à la création dans [`BookingService`](../../RestaurantHandler/Services/Reservations/BookingService.cs).

`Reservation.UnitPrice`, `Reservation.Amount` et `Reservation.CommissionRate` sont des
**copies** prises au moment de la réservation. Le professeur peut ensuite changer son prix
ou la plateforme sa commission : les réservations existantes ne bougent pas.

*Pourquoi :* sans cela, un remboursement calculé six semaines plus tard ne correspondrait
plus à ce qui a été encaissé.

### I3 — Jamais deux liens de paiement vivants sur une même commande

**État :** Appliqué — `Order.CheckoutAttempt` (contrôle de concurrence) et `Order.PaymentDeadline` dans [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs).

Sinon l'élève peut être débité deux fois, et il faut le rembourser. Trois gardes
cumulatives :

1. la commande doit être en attente — on ne rouvre pas un paiement déjà encaissé ;
2. une session existante est relue chez Stripe : si elle est **complète**, on refuse (le
   paiement est passé, la notification n'a simplement pas encore été traitée) ; si elle est
   **ouverte**, on l'expire avant d'en émettre une nouvelle ;
3. le numéro de tentative et l'échéance sont **écrits en base avant** l'appel à Stripe,
   avec un contrôle de concurrence qui fait échouer la seconde de deux requêtes simultanées
   (double-clic).

### I4 — Une clé d'idempotence est dérivée de données stables

**État :** Appliqué — [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs), [`JobChron`](../../RestaurantHandler/Services/JobChron.cs). S'y ajoute `coupon_{orderId}_{tentative}`.

`checkout_{orderId}_{tentative}`, `payout_{reservationId}`, `refund_{reservationId}`.
Jamais un identifiant fraîchement généré, jamais la composition d'un lot.

*Pourquoi :* Stripe ne déduplique une clé que pendant **24 h** et **refuse** la même clé
accompagnée de paramètres différents. Une clé dérivée d'un lot n'est pas stable : si le
transfert part mais que l'enregistrement local échoue, et qu'entre-temps une ligne entre ou
sort du lot, le rejeu envoie une clé différente et Stripe verse deux fois.

*Corollaire :* le corps de la requête doit être **reproductible**. C'est pour cela que
l'échéance est figée en base avant l'appel, et arrondie à la minute supérieure.

### I5 — L'appel externe d'abord, la validation en base ensuite

**État :** Appliqué — [`JobChron`](../../RestaurantHandler/Services/JobChron.cs).

Ordre imposé sur toute opération d'argent : appeler Stripe, **puis** valider la transaction
en base. L'inverse produit un versement fantôme — une écriture comptable sans argent
derrière.

*Garde complémentaire côté base :* une ligne n'est transférée que si elle n'est pas déjà
rattachée à un versement, et ce rattachement est posé dans la **même** transaction que
l'argent.

### I6 — Un versement = une réservation

**État :** Appliqué — `JobChron.PayoutTeacherBalances`.

Un transfert Stripe et un enregistrement de versement par cours. Le professeur reçoit tout
de même un virement bancaire groupé : c'est Stripe qui agrège selon son échéancier.

*Pourquoi :* rend la clé d'idempotence stable (I4) et isole les échecs — un solde
insuffisant ne bloque plus que sa propre réservation, rejouée au cycle suivant, au lieu de
tout le solde du professeur.

### I7 — Le montant d'un versement est figé avant l'appel Stripe

**État :** Appliqué — `TeacherPayout` figé en `PENDING`, rejoué par `RetryPendingPayouts` dans [`JobChron`](../../RestaurantHandler/Services/JobChron.cs).

Le versement est écrit **en attente** en base — montant net, dette appliquée, rattachement
des lignes — puis passé à **payé** une fois Stripe acquitté. Un versement resté en attente
est **rejoué tel quel**, jamais recalculé.

*Pourquoi :* depuis que le montant dépend de la dette du professeur, un montant recalculé
au cycle suivant enverrait la même clé avec des paramètres différents, que Stripe rejette
(I4).

### I8 — C'est le type, pas le signe, qui fait la dette

**État :** Appliqué — `HardCode.DEBT_TRANSACTION_TYPES`.

Seuls les types `PENALTY` et `CLAWBACK` constituent une dette.

Une ligne `REFUND` est négative mais **toujours adossée** à la ligne `PAYMENT` de la même
réservation : les deux s'annulent, aucun argent n'est parti, rien n'est dû. La recouvrer
ferait payer le remboursement deux fois au professeur.

### I9 — Toute somme de portefeuille exclut les lignes archivées

**État :** Appliqué dans le calcul du solde et des versements ; à revérifier sur **chaque** nouvelle somme (aucun filtre global EF).

Quand une dette est plus grosse qu'un versement, la ligne d'origine est **archivée** et
remplacée par deux lignes qui somment à l'identique (part recouvrée / reste dû). La somme
des lignes vivantes reste la dette infligée ; la somme des lignes non réglées reste ce qui
est encore dû. Oublier le filtre d'archivage double la dette.

### I10 — Une commande payée est immuable

**État :** Appliqué — entité `Refund` ([`Refund.cs`](../../RestaurantHandler/Domain/Entities/Order/Refund.cs)).

Une annulation n'efface rien : elle **ajoute** une ligne de remboursement par-dessus. Le
montant remboursé d'une commande est la somme de ses lignes de remboursement.

### I11 — Un statut de réservation ne change que par la voie officielle

**État :** Appliqué — [`ReservationStatusExtensions`](../../RestaurantHandler/Utilities/ReservationStatusExtensions.cs) ; aucune affectation directe de `Reservation.StatusId` trouvée dans les services.

Toute transition passe par une fonction unique qui écrit simultanément le nouvel état et
une ligne d'historique (état d'origine, état d'arrivée, auteur, motif). Une affectation
directe du statut est un bug.

*Pourquoi :* c'est le seul journal d'audit d'un domaine où l'argent dépend de l'état.

### I12 — Un avis est adossé à une réservation réellement passée

**État :** Appliqué — [`ReviewService`](../../RestaurantHandler/Services/ReviewService.cs) exige `DONE`
(lot 4), et l'unicité est tenue par l'index unique sur `Review.ReservationId` dans
[`ReviewConfiguration`](../../RestaurantHandler/Persistence/Configurations/ReviewConfiguration.cs).

Un avis ne peut exister que sur une réservation terminée, et une seule fois (unicité en
base sur la réservation). C'est ce qui le rend infalsifiable.

### I13 — Le paiement n'est exigé que si le professeur peut être payé

**État :** Appliqué — `paymentRequired = IsStripeConfirmed && IsVerified` dans [`BookingService`](../../RestaurantHandler/Services/Reservations/BookingService.cs).

`Order.PaymentRequired` est figé à la création de la commande à partir de l'état du
professeur : **vérifié documentairement et confirmé côté Stripe**. Si l'un des deux manque,
aucun argent ne circule et la réservation suit le flux manuel à 48 h.

*Pourquoi :* encaisser pour un professeur qu'on ne peut pas payer, c'est créer une dette de
la plateforme envers lui, sans mécanisme de sortie.

### I14 — Un litige résolu ne produit jamais de dette

**État :** Appliqué — [`DisputeService`](../../RestaurantHandler/Services/DisputeService.cs) n'écrit que des lignes `REFUND`.

Le crédit d'une réservation en litige est **gelé et jamais versé** : le rembourser ne produit
aucun mouvement à rebours. Une résolution n'écrit donc qu'une ligne de type `REFUND`, jamais de
`PENALTY` ni de `CLAWBACK`.

*Pourquoi :* en inscrire une ferait payer au professeur un argent qu'il n'a jamais touché. C'est
la conséquence directe de [I8](#i8--cest-le-type-pas-le-signe-qui-fait-la-dette) et de
[ADR-0009](../40-decisions/ADR-0009-fenetre-de-litige.md) — la fenêtre de contestation se fermant
avant le versement, le cas « récupérer un crédit déjà parti » ne se présente pas.

### I15 — Un tableau de suivi relie un professeur et un élève qui partagent une réservation

**État :** Absent — aucun tableau de suivi dans le code. La règle de rattachement existe déjà :
[`ReservationAccess.SharesReservationAsync`](../../RestaurantHandler/Utilities/ReservationAccess.cs).

Un `Board` n'est créé que si l'élève a au moins une réservation sur un créneau du professeur.
Tout ce qu'une carte référence appartient à ce couple : le produit est **un produit du
professeur**, la réservation relie **ce professeur à cet élève**. Une fois le tableau créé, la
relation n'est plus revérifiée : l'annulation ultérieure de toutes les réservations ne
supprime pas le suivi déjà ouvert.

*Pourquoi :* sans ce rattachement, un professeur pourrait ouvrir un espace au nom de
n'importe quel élève inscrit et y écrire — un canal de démarchage non sollicité. Le même
critère protège déjà la lecture du profil d'un élève. Voir
[ADR-0014](../40-decisions/ADR-0014-tableaux-de-suivi.md).

## 3. Règles de calcul

### Montant d'une réservation

```
Amount = UnitPrice × durée du créneau en heures
```

Arrondi au centime, `MidpointRounding.AwayFromZero`. Les prix stockés sont **TTC**.

### Répartition au paiement

```
part professeur  = Amount × (1 − CommissionRate)     → crédit au portefeuille
part plateforme  = Amount × CommissionRate           → moins les frais Stripe réels
```

Les frais Stripe ne sont **pas** connus à l'encaissement : ils sont renseignés plus tard,
une fois l'opération réglée chez Stripe. Aucune règle métier ne doit en dépendre au moment
du clic.

### Solde d'un professeur

```
Disponible = Σ(Amount)  des lignes non versées, non archivées, hors types de dette
Dette      = Σ|Amount|  des lignes non versées, non archivées, de type PENALTY ou CLAWBACK
Solde      = Disponible − Dette          (peut être négatif)
```

### Facture

Les prix étant TTC, la base hors taxes et la TVA sont **dérivées** :

```
HT  = TTC / (1 + VAT_RATE)
TVA = TTC − HT
```

Le vendeur légal est le **professeur** ; la plateforme émet la facture en son nom (mandat
de facturation).

### Résolution d'un litige

```
en faveur du professeur   → remboursé 0            ; crédit inchangé
en faveur de l'élève      → remboursé Amount       ; crédit annulé de la part professeur
partielle                 → remboursé R ≤ Amount   ; crédit annulé de min(R, part professeur)
```

Le remboursement d'une issue partielle est **à la charge du professeur** : il sort de son crédit,
la plateforme conservant sa commission. Le plafond à la part professeur garantit qu'une issue
partielle portant sur la totalité du montant donne exactement le même résultat qu'une issue
« élève » — la même décision exprimée de deux façons ne peut pas produire deux montants.

Comme pour l'annulation, le calcul vit **à un seul endroit**,
[`DisputeResolutionPolicy`](../../RestaurantHandler/Domain/Policies/DisputeResolutionPolicy.cs), et sert
les trois voies de résolution : acceptation, arbitrage et résolution automatique
([ADR-0013](../40-decisions/ADR-0013-litige-partiel-a-charge-du-prof.md)).

### Annulation

La répartition est calculée à un seul endroit, et le même code sert à l'exécution **et** à
la prévisualisation affichée à l'élève — de sorte qu'on ne puisse jamais lui annoncer autre
chose que ce qu'il obtiendra. Détail complet et exemples chiffrés :
[politique d'annulation](05-politique-annulation.md).

## 4. Règles de disponibilité et de réservation

| Règle | Énoncé |
|---|---|
| R1 | Un créneau est réservable s'il est à venir, non archivé, et sans réservation vivante (en attente, acceptée, livrée, versable ou en litige). |
| R2 | Deux créneaux d'un même professeur ne peuvent pas se chevaucher. |
| R3 | Un créneau passé sans réservation, ou avec seulement des réservations jamais confirmées, est purgé quotidiennement. |
| R4 | Une réservation en attente bloque le créneau pendant toute la fenêtre de paiement ; à l'expiration, le créneau est libéré. |
| R5 | Un élève ne peut pas réserver un créneau de son propre compte professeur. |
| R6 | Le produit réservé doit appartenir au professeur du créneau. |

La définition de « réservable » est unique et partagée par le filtre de recherche, le tri
par disponibilité et le compteur de créneaux affiché sur la fiche professeur — les trois ne
peuvent pas diverger.

**État dans le code**

| Règle | État | Constat |
|---|---|---|
| R1 | Appliqué | [`TeacherAvailability.BlockingStatuses`](../../RestaurantHandler/Domain/Policies/TeacherAvailability.cs) est la seule liste des statuts occupants, partagée par la recherche, la réservation, les calendriers et la purge ; la réservation refuse un créneau commencé. |
| R2 | Appliqué | contrôle de chevauchement dans [`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs). |
| R3 | Appliqué | `SlotCleaningJob`, tâche quotidienne `DailyCleanSlots` ([`Scheduler.cs`](../../RestaurantHandler/Utilities/Scheduler.cs)) : archivage logique, jamais de suppression ; un créneau porteur d'une réservation en attente est épargné. |
| R4 | Appliqué | `CHECKOUT_WINDOW` + tâche `clean-stale-orders`. |
| R5 | Appliqué | refus « Vous ne pouvez pas réserver votre propre créneau » dans `ReservationService`. |
| R6 | Appliqué | refus « Ce produit n'est pas proposé par ce professeur ». |

Un professeur dont le compte n'est pas actif n'est ni réservable ni listé par la recherche.

## 5. Identifiants de référentiel

Les valeurs des référentiels (rôles, genres, statuts de compte, statuts de réservation,
statuts de commande, statuts de transaction, types de transaction professeur, niveaux,
catégories, types d'adresse, types de créneau, langues, statuts et issues de litige, types
de document, moyens de paiement) sont **pré-alimentées avec des identifiants fixes**,
déclarés à un seul endroit.

Deux règles à respecter :

1. **Aucun identifiant en double entre familles.** Un même identifiant pour des entités de
   familles différentes est une bombe à retardement lors des jointures de diagnostic.
   *État :* aucun doublon entre familles dans [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs) ; seuls les deux types de dette y sont
   recopiés dans `DEBT_TRANSACTION_TYPES`.
2. **Les énumérations persistées sont en ajout seul.** Leur valeur numérique est stockée en
   base : réordonner une énumération corrompt silencieusement les données existantes.

Énumérations concernées : statut de réservation, type de transaction professeur, statut de
commande, statut de vérification SIRET, statut de revue documentaire, niveau, catégorie,
type de créneau, type de notification. *État :* les ajouts récents sont marqués « append-only »
dans `HardCode.cs`.
