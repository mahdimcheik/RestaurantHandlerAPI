# ADR-0009 — Fenêtre de litige égale au délai de versement

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

Deux durées auraient pu être indépendantes :

- combien de temps l'élève peut contester après le cours ;
- combien de temps la plateforme attend avant de verser le professeur.

Si la fenêtre de contestation est **plus longue** que le délai de versement, un litige peut
survenir alors que l'argent est déjà parti chez le professeur. Il faut alors le récupérer —
donc un renvoi de transfert ou une créance — et ce cas cesse d'être exceptionnel pour devenir
nominal.

## Décision

**Une seule constante** pour les deux : 2 jours et 2 heures.

Tant que la fenêtre de contestation est inférieure ou égale au délai de versement, un litige
ne peut jamais arriver après un versement.

Les deux heures supplémentaires sont une marge qui absorbe le délai de règlement du
prestataire : un transfert exige des fonds effectivement disponibles sur le solde de la
plateforme.

## Conséquences

**Ce qu'on gagne**

- Le cas « rembourser un crédit déjà versé » devient **exceptionnel par construction**. Il ne
  subsiste que pour les remboursements décidés hors parcours applicatif.
- Aucun renvoi de transfert n'est nécessaire dans le fonctionnement normal.
- Une seule valeur à faire évoluer, donc pas de dérive entre deux constantes censées rester
  liées.
- L'erreur de solde insuffisant devient rare, la marge couvrant le règlement.

**Ce que ça coûte**

- **Les deux durées ne peuvent plus évoluer séparément.** Allonger la fenêtre de contestation
  pour le confort de l'élève retarde mécaniquement le paiement du professeur.
- 48 heures est court pour un élève qui découvre un problème le lundi après un cours du
  vendredi soir.

**Ce qu'on surveille**

Le nombre de contestations arrivant **après** l'échéance. S'il est significatif, la fenêtre
est trop courte — et l'allonger imposera alors de concevoir la récupération comme un cas
nominal. C'est le moment où cet ADR devra être remplacé.

## État dans le code

`HardCode.DISPUTE_WINDOW` = 2 jours + 2 heures ([`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs)),
lu à la fois par [`DisputeService`](../../RestaurantHandler/Services/DisputeService.cs) et par
[`JobChron`](../../RestaurantHandler/Services/JobChron.cs). Aucune colonne `DisputeDeadline` : l'échéance est
calculée depuis `Slot.DateTo`.
