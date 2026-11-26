# Vision et objectifs

## Le problème

Un fast-food à emporter perd du chiffre aux heures de pointe : file d'attente au comptoir,
clients qui renoncent, commandes mal transmises à la cuisine, attente sans visibilité. Les
solutions du marché (bornes, plateformes de click & collect) prennent une commission ou
imposent leur parcours, et ne se branchent pas sur la carte réelle du restaurant — ses
compositions, ses « sans oignons », ses menus à choix.

## L'objectif

**RestaurantHandler** est l'outil de commande d'**un seul restaurant** de type fast-food /
snack à emporter. Il permet :

- au **client** de commander et payer **par carte, en ligne**, depuis son téléphone, pour un
  retrait **dès que possible** ou **à un créneau choisi**, puis de suivre sa commande en temps
  réel jusqu'à « Prête » ;
- au **personnel** (compte Manager) de piloter la **cuisine** sur un écran (KDS), de remettre
  les commandes, d'annuler à la demande d'un client et de gérer la carte, les horaires et les
  ruptures ;
- au **gérant** de suivre l'activité (chiffre d'affaires, panier moyen, ventes par produit,
  heures de pointe).

Le projet vise une **mise en production réelle**, développée **en solo (back + front)**, avec
une ouverture **sous 3 mois**.

## Périmètre V1

| Dans la V1 | Hors V1 |
|---|---|
| Comptes client obligatoires (e-mail/mot de passe ou Google) | Commande en invité |
| Carte : catégories, produits, ingrédients retirables et remplaçables, groupes d'options (dont suppléments) (pain, sauces, taille), menus à emplacements | Allergènes dans l'app (V2) |
| Horaires, fermetures exceptionnelles, pause des commandes | Gestion de stock chiffrée |
| Ruptures d'ingrédients et de produits en temps réel | Multi-restaurants, multi-langue |
| Click & collect : retrait ASAP ou sur créneau à capacité limitée | Livraison, commande à table, borne |
| Paiement **par carte en ligne** (SumUp), seul moyen de paiement ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) | Paiement au retrait, saisie au comptoir, encaissement sur place dans l'app |
| Annulation par le restaurant, à la demande du client (téléphone ou sur place) | Annulation en ligne par le client |
| — | **Caisse enregistreuse** et impression de tickets pilotées par l'app |
| Écran cuisine (KDS) temps réel | Notifications push / e-mail / SMS de suivi |
| Suivi de commande temps réel dans la PWA | Codes promo, fidélité |
| Statistiques Manager | Export comptable |

**Point structurant :** l'application ne prend que des **commandes en ligne payées par carte**
et n'enregistre **aucun encaissement sur place** ; elle n'est pas une caisse. Une vente faite au
comptoir du restaurant passe par sa propre caisse, hors application. Voir [ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md) (qui remplace
ADR-0006 et ADR-0010) et [conformité légale](../20-regles-metier/04-conformite-legale.md).

## Critères de succès de la V1

1. Un service complet de midi se déroule avec l'application, sans retour au papier.
2. Aucune commande payée en ligne n'est perdue ni dupliquée (rapprochement SumUp ↔ commandes
   à 100 %).
3. Le temps entre la validation d'une commande et son apparition au KDS est inférieur à
   2 secondes.
4. Un client commande un menu personnalisé en moins de 2 minutes sur mobile.
5. Le gérant consulte le CA du jour et les ventes par produit sans export.

## Ce qu'on ne sacrifie pas

- **Le montant est toujours calculé par le serveur**, jamais repris du client.
- **Une commande n'est jamais modifiée après validation** sur ses prix : nom, prix et TVA sont
  copiés dans la ligne au moment de la commande.
- **La conformité légale** : aucun encaissement sur place dans l'application.
