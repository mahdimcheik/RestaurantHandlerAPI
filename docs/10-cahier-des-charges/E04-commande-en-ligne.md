# E04 — Commande en ligne (click & collect)

Calcul, validations et états : voir [règles de commande](../20-regles-metier/01-regles-commande.md)
et [machine à états](../20-regles-metier/02-machine-etats-commande.md).

### US-E04-1 — Ajouter un produit personnalisé au panier

En tant que client, je veux ajouter un produit en retirant, remplaçant ou ajoutant des
ingrédients et en faisant mes choix (pain, sauces, taille) afin de commander exactement ce que
je veux.

**Priorité :** V1
**État :** Partielle
**Avancement :** Règles et prix côté serveur faits en J2 (`POST /orders/quote`, R2/R4) ; écran fait (F8a : fiche produit, prix par le devis, panier conservé sur l'appareil).

**Critères d'acceptation**

- Seuls les ingrédients retirables peuvent être retirés ou remplacés ; un remplacement se fait
  par un ingrédient proposé en remplacement et disponible, au surcoût
  `max(0, écart de prix de référence)` affiché avant l'ajout.
- Les suppléments sont les choix d'un groupe d'options facultatif du produit.
- Chaque groupe d'options (pain, sauces, taille) reçoit entre son minimum et son maximum de
  choix distincts et disponibles ([US-E02-9](E02-carte.md#us-e02-9--définir-les-groupes-doptions-et-les-proposer-sur-les-produits)).
- Le prix de la ligne se met à jour à chaque choix (prix de base + surcoûts).
- Le panier est conservé sur l'appareil (rechargement, fermeture de l'onglet).

### US-E04-2 — Composer un menu

En tant que client, je veux choisir un produit dans chaque emplacement d'un menu (et le
personnaliser) afin de commander une formule.

**Priorité :** V1
**État :** Partielle
**Avancement :** Règles de menu faites en J2 (R5, surcoûts, personnalisation des composants) ; écran fait (F8a : `/commander/menu/:id`).

**Critères d'acceptation**

- Le nombre de choix par emplacement respecte son minimum et son maximum ; un emplacement
  obligatoire non rempli bloque l'ajout.
- Le surcoût d'une option (ex. grande boisson) s'ajoute au prix du menu.
- Chaque produit choisi peut être personnalisé comme en US-E04-1.

### US-E04-3 — Choisir le moment du retrait

En tant que client, je veux choisir « dès que possible » ou un créneau précis afin de récupérer
ma commande quand cela m'arrange.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (ASAP si ouvert et hors pause, créneau du jour non plein) ; écran fait (F8a : validation `/commander/valider`).

**Critères d'acceptation**

- « Dès que possible » n'est proposé que si Greenadine est ouvert et non en pause ; l'heure
  estimée est affichée.
- Les créneaux proposés sont ceux de [US-E03-6](E03-ouverture-ruptures.md#us-e03-6--configurer-les-créneaux-de-retrait) ;
  un créneau plein n'est pas sélectionnable.
- Restaurant fermé : seuls les créneaux futurs sont proposés (commande programmée) ; s'il n'y
  en a aucun, la commande est impossible et le prochain horaire d'ouverture est affiché.

### US-E04-4 — Valider la commande

En tant que client connecté, je veux valider mon panier avec un mode de paiement afin que ma
commande soit transmise au restaurant.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`POST /orders` : recalcul, revérification, refus détaillé par ligne, idempotence, numéro du jour) ; redirection SumUp en J3 ; écran fait (F8a : lignes refusées signalées au panier).

**Critères d'acceptation**

- Le serveur **recalcule** le total à partir de la carte en vigueur ; le total envoyé par le
  client est ignoré.
- Le serveur **revérifie** : ouverture / pause, créneau non plein, disponibilité de chaque
  produit et ingrédient, validité des personnalisations, contraintes de menus.
- En cas d'écart (prix changé, rupture), la commande est refusée avec le détail des lignes en
  cause et le panier est mis à jour ; rien n'est créé.
- Une note client facultative (500 caractères) est acceptée.
- Mode « payer en ligne » → statut `En attente de paiement` et redirection vers SumUp ([E05](E05-paiement.md)).
- Mode « payer au retrait » → statut `À valider` et attente de la validation Manager ([US-E06-3](E06-comptoir.md#us-e06-3--valider-une-commande-à-payer-au-retrait)).
- Un double clic ou un rejeu ne crée pas deux commandes (clé d'idempotence).
- Un numéro de commande unique est attribué et affiché.

### US-E04-5 — Limiter les abus du paiement au retrait

En tant que restaurant, je veux limiter le paiement au retrait afin de réduire les commandes non
retirées.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (une commande au retrait en cours, blocage au seuil de non-venues, levée par `POST /manage/customers/{id}/no-shows/reset`) ; `GET /orders/checkout-options` annonce le blocage avant la validation (F8a).

**Critères d'acceptation**

- Un client a au plus une commande « payer au retrait » en cours.
- Un client marqué « non venu » N fois (paramètre, ex. 2) ne peut plus choisir le paiement au
  retrait ; le Manager peut lever le blocage.

### US-E04-6 — Annuler sa commande

En tant que client, je veux annuler ma commande tant qu'elle n'est pas en préparation afin de
corriger une erreur.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`POST /orders/{id}/cancel`) ; remboursement en ligne en J3 ; bouton sur le suivi (F8a).

**Critères d'acceptation**

- Annulation possible en `En attente de paiement`, `À valider` ou `Confirmée` ; impossible à
  partir de `En préparation`.
- Une commande payée en ligne et annulée est remboursée ([US-E05-4](E05-paiement.md#us-e05-4--rembourser-une-commande-payée-en-ligne)).
- Le KDS retire la commande en temps réel.

### US-E04-7 — Recommander une commande passée

En tant que client, je veux remettre une ancienne commande dans mon panier afin de gagner du
temps.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**

- Les lignes encore disponibles sont ajoutées au prix actuel ; les autres sont signalées.
