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

- au **client** de commander et (s'il le souhaite) payer **en ligne** depuis son téléphone, pour
  un retrait **dès que possible** ou **à un créneau choisi**, puis de suivre sa commande en
  temps réel jusqu'à « Prête » ;
- au **personnel** (compte Manager) de saisir les commandes **au comptoir**, de valider les
  commandes à payer au retrait, de piloter la **cuisine** sur un écran (KDS) et de gérer la
  carte, les horaires et les ruptures ;
- au **gérant** de suivre l'activité (chiffre d'affaires, panier moyen, ventes par produit,
  heures de pointe).

Le projet vise une **mise en production réelle**, développée **en solo (back + front)**, avec
une ouverture **sous 3 mois**.

## Périmètre V1

| Dans la V1 | Hors V1 |
|---|---|
| Comptes client obligatoires (e-mail/mot de passe ou Google) | Commande en invité |
| Carte : catégories, produits, ingrédients retirables, substitutions, suppléments, groupes d'options (pain, sauces, taille), menus à emplacements | Allergènes dans l'app (V2) |
| Horaires, fermetures exceptionnelles, pause des commandes | Gestion de stock chiffrée |
| Ruptures d'ingrédients et de produits en temps réel | Multi-restaurants, multi-langue |
| Click & collect : retrait ASAP ou sur créneau à capacité limitée | Livraison, commande à table, borne |
| Paiement en ligne SumUp **ou** paiement au retrait (après validation Manager) | **Caisse enregistreuse certifiée maison** (V2, NF525) |
| Saisie comptoir par le Manager | Impression de tickets pilotée par l'app (V2) |
| Écran cuisine (KDS) temps réel | Notifications push / e-mail / SMS de suivi |
| Suivi de commande temps réel dans la PWA | Codes promo, fidélité |
| Statistiques Manager | Export comptable |

**Point structurant :** en V1, **l'application n'est pas la caisse légale**. Toute vente
encaissée sur place (comptoir, paiement au retrait, espèces, carte, paiement mixte) est
enregistrée sur une **caisse certifiée tierce** (SumUp POS ou équivalent). L'application
prend la commande, l'envoie en cuisine et en suit l'état ; le Manager y marque la commande
« payée ». Voir [ADR-0006](../40-decisions/ADR-0006-caisse-certifiee-tierce-v1.md) et
[conformité légale](../20-regles-metier/04-conformite-legale.md).

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
- **La conformité légale** : pas d'encaissement sur place hors caisse certifiée.
