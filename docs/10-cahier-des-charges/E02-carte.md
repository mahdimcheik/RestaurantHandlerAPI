# E02 — Carte

Modèle ([modèle](../30-technique/02-modele-donnees.md)) et API faits en J1 ; aucun écran.
Toutes les écritures sont réservées au Manager.

### US-E02-1 — Consulter la carte

En tant que visiteur, je veux voir la carte par catégorie afin de choisir ce que je commande.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`GET /menu`, cache invalidé à chaque écriture) ; écran client en J4.

**Critères d'acceptation**
- Les catégories actives s'affichent dans l'ordre `Position` ; les produits actifs de chacune,
  dans leur ordre.
- Chaque produit montre nom, description, photo, prix TTC.
- Les menus actifs ont leur propre rubrique avec leurs emplacements et options.
- Un produit en rupture (cf. [E03](E03-ouverture-ruptures.md)) est grisé et non ajoutable.
- Les éléments archivés ou inactifs n'apparaissent jamais côté client.
- La carte publique est servie depuis le cache et invalidée à chaque écriture Manager.

### US-E02-2 — Gérer les catégories

En tant que Manager, je veux créer, modifier, réordonner, désactiver une catégorie afin
d'organiser la carte.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/categories`) ; glisser-déposer en J5.

**Critères d'acceptation**
- Le nom est unique et obligatoire (64 caractères max).
- Le réordonnancement se fait par glisser-déposer et enregistre toutes les positions d'un coup.
- Une catégorie contenant des produits actifs ne peut pas être archivée.

### US-E02-3 — Gérer les produits

En tant que Manager, je veux créer et modifier un produit (nom, description, photo, nature,
catégorie, prix TTC, taux de TVA, position) afin de tenir la carte à jour.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/products`, photo WebP publique) ; écran en J5.

**Critères d'acceptation**
- Le prix est saisi en euros et stocké en centimes ; il est ≥ 0.
- Le taux de TVA est choisi dans une liste fermée (5,5 %, 10 %, 20 %) — cf.
  [conformité](../20-regles-metier/04-conformite-legale.md).
- La photo passe par le stockage objet (taille et format validés).
- Un changement de prix ne modifie aucune commande existante (snapshot).
- Un produit utilisé dans des commandes n'est jamais supprimé, seulement **archivé**.

### US-E02-4 — Gérer les ingrédients

En tant que Manager, je veux gérer la liste des ingrédients (nom, prix de référence, proposé en
remplacement) afin de décrire les compositions et de chiffrer les remplacements.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/ingredients`) ; écran back-office fait (client,
`/gestion/ingredients` : ajout, recherche, modification du nom, du prix et de la case « proposé
en remplacement », rupture, archivage ; création rapide depuis la fiche produit).

**Critères d'acceptation**
- Le nom est unique.
- Un ingrédient peut être marqué indisponible (rupture) sans être supprimé.
- Le prix de référence (≥ 0, 0 par défaut) sert uniquement au surcoût des remplacements
  ([ADR-0012](../40-decisions/ADR-0012-remplacements-prix-ingredient.md)).
- « Proposé en remplacement » (vrai par défaut) se décoche pour la pâte, le pain…

### US-E02-5 — Définir la composition d'un produit

En tant que Manager, je veux choisir les ingrédients d'un produit et indiquer lesquels sont
retirables (donc remplaçables) afin que le client personnalise sa commande. Les suppléments se
proposent par un groupe d'options facultatif ([US-E02-9](#us-e02-9--définir-les-groupes-doptions-et-les-proposer-sur-les-produits)).

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`PUT /manage/products/{id}/composition`) ; section « Composition »
de la fiche produit faite (client, `/gestion/produits/:id`).

**Critères d'acceptation**
- Un ingrédient ne figure qu'une fois dans une composition.
- Un ingrédient retirable se retire gratuitement ou se remplace par n'importe quel ingrédient
  proposé en remplacement, au surcoût `max(0, écart de prix de référence)` (R4).

### US-E02-6 — Gérer les menus

En tant que Manager, je veux créer un menu à prix fixe composé d'emplacements (ex. Plat,
Accompagnement, Boisson) proposant chacun des produits, avec un surcoût par option, afin de
vendre des formules.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/menus`, emplacements, options) ; écran en J5.

**Critères d'acceptation**
- Chaque emplacement a un libellé, une position unique dans le menu, un minimum et un maximum
  de choix (`0 ≤ min ≤ max`).
- Une option ne référence qu'un produit actif.
- Le menu a son propre taux de TVA (cf. question ouverte sur la ventilation de TVA des menus).

### US-E02-7 — Prévisualiser la carte

En tant que Manager, je veux voir la carte telle que le client la verra afin de vérifier mes
modifications avant le service.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**
- La prévisualisation inclut les éléments inactifs, signalés comme tels.

### US-E02-8 — Afficher les allergènes

En tant que client, je veux connaître les allergènes d'un produit afin de commander sans risque.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**
- Les 14 allergènes réglementaires sont rattachés aux ingrédients et remontent au produit
  (composition + remplacements + choix d'options).
- En V1, l'information est assurée hors application (affichage en salle) — cf.
  [conformité](../20-regles-metier/04-conformite-legale.md).

### US-E02-9 — Définir les groupes d'options et les proposer sur les produits

En tant que Manager, je veux définir des choix dans une liste (pain d'un sandwich, sauces, taille
d'une pizza), avec un minimum, un maximum, un nombre de choix offerts et des surcoûts, et les
proposer sur plusieurs produits, afin que le client compose son plat sans passer par la
composition.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite : groupes partagés (`/manage/option-groups`), groupes proposés par
produit (`PUT /manage/products/{id}/option-groups`), carte publique, commande ; écran du
back-office en cours (client, `/gestion/options`).

**Critères d'acceptation**
- Un groupe est **partagé** : il se définit une fois (`/manage/option-groups`) et se propose sur
  autant de produits que voulu. Le modifier le modifie pour tous ces produits.
- Chaque produit choisit ses groupes et leur ordre d'affichage en un seul appel
  (`{ optionGroups: [{ optionGroupId, rules? }] }`) ; un groupe archivé ou inconnu est refusé.
- Un produit peut redéfinir min, max et choix inclus d'un groupe (`rules`) : une seule liste
  « Ingrédients » sert à la pizza 3 ingrédients et à la pizza 5 ingrédients. Le minimum doit
  rester atteignable ; modifier le groupe est refusé s'il ne l'est plus pour un produit.
- Un groupe peut autoriser plusieurs fois le même choix (`maxPerChoice`, 1 par défaut) : « double
  jambon + œuf » compte pour 3 ingrédients.
- Le libellé d'un groupe est unique parmi les groupes non archivés.
- Les choix d'un groupe sont remplacés en un seul appel, dans l'ordre d'affichage ; un choix
  retiré est archivé, jamais supprimé.
- Un groupe proposé sur un produit non archivé ne peut pas être archivé ; l'archivage est
  définitif.
- Un groupe a un libellé, `0 ≤ min ≤ max`, au moins `min` unités possibles (choix × fois par choix), un nombre de choix offerts
  (tous par défaut) et un prix par choix au-delà des offerts (≥ 0).
- Un choix a un nom unique dans son groupe, un surcoût ≥ 0 (ex. +3 € pour la grande taille) et
  peut être lié à un ingrédient : la rupture de l'ingrédient grise le choix.
- Un ingrédient lié à un choix d'un groupe non archivé ne peut pas être archivé.
- Groupes et choix peuvent avoir une photo (même contrôle et format que les produits),
  envoyée à part (`POST /manage/option-groups/{id}/image`, `…/choices/{choiceId}/image`) et
  conservée quand les choix sont remplacés.
- Exemples : « Pain » 1 parmi baguette / pita ; « Sauces » 0 à 3, 2 offertes puis +0,50 € ;
  « Taille » 1 parmi 29 cm / 33 cm (+3 €).
