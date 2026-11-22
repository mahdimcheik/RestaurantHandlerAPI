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

En tant que Manager, je veux gérer la liste des ingrédients afin de décrire les compositions et
les suppléments.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/ingredients`) ; écran en J5.

**Critères d'acceptation**
- Le nom est unique.
- Un ingrédient peut être marqué indisponible (rupture) sans être supprimé.

### US-E02-5 — Définir la composition d'un produit

En tant que Manager, je veux définir les ingrédients d'un produit, lesquels sont retirables,
par quoi chacun peut être remplacé (et à quel surcoût), et quels suppléments sont proposés
(prix, quantité max) afin que le client personnalise sa commande.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`PUT /manage/products/{id}/composition`) ; écran en J5.

**Critères d'acceptation**
- Un ingrédient ne figure qu'une fois dans une composition.
- Une substitution ne vise qu'un ingrédient de la composition et un remplaçant différent.
- Les surcoûts sont ≥ 0 ; la quantité max d'un supplément est ≥ 1 (3 par défaut).

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
  (composition + substitutions + suppléments choisis).
- En V1, l'information est assurée hors application (affichage en salle) — cf.
  [conformité](../20-regles-metier/04-conformite-legale.md).
