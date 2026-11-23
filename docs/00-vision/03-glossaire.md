# Glossaire

| Terme | Définition | Dans le code |
|---|---|---|
| **Carte** | Ensemble des catégories, produits et menus proposés à la vente. | `Domain/Entities/Catalog/` |
| **Catégorie** | Rubrique d'affichage de la carte (Burgers, Boissons…), ordonnée. | `Category` |
| **Produit** | Article vendable seul : plat, boisson ou dessert (sa *nature*, indépendante de sa catégorie). | `Product`, `ProductKind` |
| **Ingrédient** | Composant d'un produit ; peut être en rupture ; a un prix de référence pour les remplacements. | `Ingredient.IsAvailable`, `Ingredient.PriceCents` |
| **Composition** | Liste des ingrédients d'un produit par défaut, chacun éventuellement **retirable**. | `ProductIngredient.IsRemovable` |
| **Retrait d'ingrédient** | « Sans X » : gratuit, seulement si l'ingrédient est retirable. | `ModificationType.Remove` |
| **Remplacement** | Échange d'un ingrédient retirable contre n'importe quel ingrédient proposé en remplacement ; surcoût = écart de prix de référence s'il est positif. | `ModificationType.Replace`, `Ingredient.IsOfferedAsReplacement` |
| **Supplément** | Ajout payant d'un ingrédient, proposé par un groupe d'options facultatif (« Suppléments pizza »). | `OptionGroup`, `ModificationType.Choice` |
| **Menu** | Formule à prix fixe composée d'emplacements. | `Menu` |
| **Groupe d'options** | Choix à faire dans une liste propre au produit (« Pain », « Sauces », « Taille »), partagée entre produits, avec un minimum, un maximum, des choix offerts, des surcoûts et un nombre de fois par choix (double jambon) ; un produit peut redéfinir les bornes. | `OptionGroup`, `OptionChoice`, `ProductOptionGroup`, `ModificationType.Choice` |
| **Emplacement** | Étape d'un menu (« Plat », « Boisson »…) avec un nombre minimal et maximal de choix. | `MenuSlot` |
| **Option d'emplacement** | Produit proposé dans un emplacement, avec un surcoût éventuel (« grande boisson +1 € »). | `MenuSlotOption` |
| **Commande** | Ensemble de lignes passées par un client ou saisies au comptoir, avec un numéro unique. | `Order`, `OrderNumber` |
| **Ligne de commande** | Un produit ou un menu commandé ; les choix d'un menu sont des lignes enfants. | `OrderItem`, `ParentItemId` |
| **Snapshot** | Copie du nom, du prix et de la TVA au moment de la commande, pour qu'une modification de carte ne change pas une commande passée. | `NameSnapshot`, `UnitPriceCents`, `VatRate` |
| **Canal** | Origine de la commande : en ligne ou comptoir. | `OrderChannel` |
| **Retrait ASAP** | « Dès que possible » : la commande est préparée au plus tôt ; un délai estimé est affiché. | à créer |
| **Créneau de retrait** | Tranche horaire choisie par le client, à capacité limitée. | à créer |
| **Pause des commandes** | Interrupteur Manager qui bloque temporairement les commandes en ligne sans fermer le restaurant. | à créer |
| **Validation Manager** | Acceptation explicite d'une commande à payer au retrait avant qu'elle parte en cuisine. | à créer |
| **KDS** | *Kitchen Display System* : écran de cuisine affichant les commandes à préparer en temps réel. | à créer (SignalR existant) |
| **Caisse certifiée tierce** | Logiciel de caisse conforme à la loi anti-fraude TVA (ex. SumUp POS), utilisé en V1 pour toute vente encaissée sur place. | hors app |
| **NF525** | Certification des logiciels de caisse (inaltérabilité, sécurisation, conservation, archivage). | V2 |
| **Centimes** | Tous les montants sont des entiers en centimes d'euro. | `*Cents` |
