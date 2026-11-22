# E03 — Catalogue de produits

> Statut : **Stable** · Conventions : [README](README.md)
>
> Un **produit** est à la fois la vitrine du profil et l'objet vendu lors d'une réservation.
> Il n'y a pas de catalogue séparé.

## US-E03-01 — Créer un produit

**En tant que** professeur
**je veux** décrire un cours que je propose avec son prix
**afin qu'** un élève puisse le réserver.

**Priorité :** Doit

**État :** Partielle · **Code :** [`ProductService`](../../RestaurantHandler/Services/ProductService.cs)
Écart : aucun attribut `Teacher` sur `POST /Product` ; le rôle est contrôlé dans le
service.

**Critères d'acceptation**

- Champs obligatoires : intitulé, description, prix horaire, niveau.
- Une ou plusieurs catégories peuvent être associées.
- Le prix est strictement positif et s'entend **TTC**.
- Le produit apparaît immédiatement sur ma fiche publique.

## US-E03-02 — Modifier un produit

**En tant que** professeur
**je veux** corriger l'intitulé, la description, le niveau, les catégories ou le prix
**afin de** faire évoluer mon offre.

**Priorité :** Doit · **Dépend de :** US-E03-01

**État :** Partielle · **Code :** [`ProductService`](../../RestaurantHandler/Services/ProductService.cs)
Propriété vérifiée dans le service ; prix figé à la réservation. Écart : aucun
attribut d'autorisation sur le contrôleur.

**Critères d'acceptation**

- Je ne peux modifier **que mes propres produits** ; la vérification est faite côté serveur
  à partir de l'identité de la session, jamais d'un identifiant fourni par le client.
- Un changement de prix **n'affecte aucune réservation existante** : le prix est figé à la
  réservation. Voir
  [invariant I2](../20-specs-fonctionnelles/01-regles-metier.md#i2--les-prix-sont-figés-à-la-réservation-jamais-recalculés).

## US-E03-03 — Supprimer un produit

**En tant que** professeur
**je veux** retirer un produit de mon catalogue
**afin de** ne plus le proposer.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ProductService`](../../RestaurantHandler/Services/ProductService.cs)
Suppression logique et contrôle de propriété dans le service. Écart : aucun attribut
d'autorisation.

**Critères d'acceptation**

- La suppression est **logique** : les réservations passées qui le référencent restent
  lisibles, avec leur intitulé et leur prix d'origine.
- Le produit disparaît de la fiche publique et des listes de réservation.
- Une réservation à venir qui le référence n'est **pas** annulée.

## US-E03-04 — Consulter les produits d'un professeur

**En tant que** visiteur
**je veux** voir les cours proposés par un professeur, avec niveau, catégories et prix
**afin de** choisir celui qui me convient.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`ProductService`](../../RestaurantHandler/Services/ProductService.cs)
`GET /Product/teacher/{id}`, `GET /Product/level/{id}`. `GET /Product/all` est une
liste globale sans usage identifié.

**Critères d'acceptation**

- La liste est publique et n'affiche que les produits non supprimés.
- Elle est filtrable par niveau.
- Les libellés de niveau et de catégorie proviennent des référentiels administrables. Voir
  [E18](E18-referentiels-i18n.md).

## US-E03-05 — Choisir le produit au moment de réserver

**En tant qu'** élève
**je veux** choisir le cours concerné en réservant un créneau
**afin que** le prix et l'objet de la réservation soient sans ambiguïté.

**Priorité :** Doit · **Dépend de :** US-E06-01

**État :** Implémentée · **Code :** [`BookingService`](../../RestaurantHandler/Services/Reservations/BookingService.cs)

**Critères d'acceptation**

- Seuls les produits **du professeur propriétaire du créneau** sont proposés.
- Le montant est calculé à partir du prix du produit et de la durée du créneau, et affiché
  avant confirmation.
- Réserver avec le produit d'un autre professeur est refusé côté serveur.
