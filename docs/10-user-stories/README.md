# User stories — conventions d'écriture

> Statut : **Stable**

## Identifiant

`US-Exx-nn` — `Exx` désigne l'épique, `nn` le numéro dans l'épique. **Un identifiant ne se
réutilise jamais**, même si la story est supprimée : il est cité dans les commits, les
tests et les tickets.

## Format

```markdown
### US-E06-03 — Payer une commande

**En tant qu'** élève
**je veux** payer une commande en attente par carte
**afin de** confirmer mes réservations avant expiration du délai.

**Priorité :** Doit · **Dépend de :** US-E06-01

**État :** Partielle · **Code :** [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs)
Écart : …

**Critères d'acceptation**

- Étant donné une commande en attente dont l'échéance n'est pas dépassée
  Quand je demande à payer
  Alors je suis redirigé vers une page de paiement bancaire
  Et l'échéance de la commande correspond à celle de la session de paiement.

- Étant donné une commande déjà payée
  Quand je demande à payer
  Alors la demande est refusée avec un message explicite.
```

## Règles de rédaction

1. **Le « afin de » est obligatoire** et doit porter une valeur, pas une paraphrase.
   « afin de payer » est interdit après « je veux payer ».
2. **Un critère d'acceptation est testable.** S'il ne peut pas devenir un test automatisé
   ou une manipulation reproductible, il est mal écrit.
3. **Les cas d'erreur font partie de la story.** Une story sans cas négatif est incomplète.
4. **Aucune règle de calcul n'est écrite ici.** On renvoie vers
   [les règles métier](../20-specs-fonctionnelles/01-regles-metier.md). Une règle dupliquée
   est une règle qui divergera.
5. **Pas de solution technique** dans la story. Le « comment » est dans
   [les specs techniques](../30-specs-techniques/).
6. **L'état dit où en est le code**, jamais ce qu'il devrait faire : `Implémentée` si tous
   les critères côté API sont tenus, `Partielle` avec la liste des écarts, `Absente`, ou
   `Hors API` si la story relève entièrement du front. Il est mis à jour dans la même
   contribution que le code qui le change.

## Priorités (MoSCoW)

| Niveau | Sens |
|---|---|
| **Doit** | Sans elle, la plateforme ne fonctionne pas. Bloque la mise en production. |
| **Devrait** | Fortement attendue, mais on peut ouvrir sans. |
| **Pourrait** | Confort, à faire si le temps le permet. |
| **Plus tard** | Consciemment reporté après la v1. |

## Épiques

| # | Épique | Cœur du sujet |
|---|---|---|
| [E01](E01-authentification-comptes.md) | Authentification & comptes | entrer dans le produit |
| [E02](E02-profils.md) | Profils | se présenter et être crédible |
| [E03](E03-catalogue-produits.md) | Catalogue de produits | ce qui est vendu |
| [E04](E04-recherche-decouverte.md) | Recherche & découverte | rencontrer l'offre |
| [E05](E05-creneaux-planning.md) | Créneaux & planning | quand |
| [E06](E06-reservation-commande.md) | Réservation & commande | l'engagement |
| [E07](E07-paiement.md) | Paiement | l'argent entre |
| [E08](E08-wallet-versements.md) | Portefeuille & versements | l'argent sort |
| [E09](E09-annulation-remboursement.md) | Annulation & remboursement | l'argent revient |
| [E10](E10-preuve-de-realisation.md) | Preuve de réalisation | le cours a-t-il eu lieu |
| [E11](E11-litiges.md) | Litiges | quand ça se passe mal |
| [E12](E12-avis-reputation.md) | Avis & réputation | la pression de long terme |
| [E13](E13-messagerie.md) | Messagerie | se parler |
| [E14](E14-notifications.md) | Notifications | savoir qu'il se passe quelque chose |
| [E15](E15-certification-documents.md) | Certification & documents | filtrer l'offre |
| [E16](E16-facturation-comptabilite.md) | Facturation & comptabilité | les traces légales |
| [E17](E17-administration.md) | Administration | exploiter la plateforme |
| [E18](E18-referentiels-i18n.md) | Référentiels & i18n | les listes administrables |
| [E19](E19-transverse.md) | Transverse | RGPD, SEO, accessibilité, observabilité |
| [E20](E20-suivi-de-cours.md) | Suivi de cours | accompagner l'élève entre les séances |

## État d'implémentation

Relevé au 2026-09-16 en confrontant chaque story au code de BonProfAPI. Le détail et les
écarts sont sur chaque story.

| # | Épique | Implémentées | Partielles | Absentes | Hors API |
|---|---|--:|--:|--:|--:|
| [E01](E01-authentification-comptes.md) | Authentification & comptes | 3 | 5 | 2 | 0 |
| [E02](E02-profils.md) | Profils | 0 | 8 | 1 | 0 |
| [E03](E03-catalogue-produits.md) | Catalogue de produits | 2 | 3 | 0 | 0 |
| [E04](E04-recherche-decouverte.md) | Recherche & découverte | 4 | 2 | 0 | 0 |
| [E05](E05-creneaux-planning.md) | Créneaux & planning | 4 | 2 | 0 | 0 |
| [E06](E06-reservation-commande.md) | Réservation & commande | 7 | 2 | 0 | 0 |
| [E07](E07-paiement.md) | Paiement | 5 | 2 | 0 | 0 |
| [E08](E08-wallet-versements.md) | Portefeuille & versements | 2 | 3 | 1 | 0 |
| [E09](E09-annulation-remboursement.md) | Annulation & remboursement | 6 | 1 | 0 | 0 |
| [E10](E10-preuve-de-realisation.md) | Preuve de réalisation | 3 | 0 | 1 | 0 |
| [E11](E11-litiges.md) | Litiges | 1 | 2 | 3 | 0 |
| [E12](E12-avis-reputation.md) | Avis & réputation | 2 | 3 | 0 | 0 |
| [E13](E13-messagerie.md) | Messagerie | 1 | 4 | 2 | 0 |
| [E14](E14-notifications.md) | Notifications | 3 | 2 | 0 | 0 |
| [E15](E15-certification-documents.md) | Certification & documents | 6 | 2 | 0 | 0 |
| [E16](E16-facturation-comptabilite.md) | Facturation & comptabilité | 5 | 1 | 0 | 0 |
| [E17](E17-administration.md) | Administration | 0 | 4 | 3 | 0 |
| [E18](E18-referentiels-i18n.md) | Référentiels & i18n | 3 | 1 | 0 | 1 |
| [E19](E19-transverse.md) | Transverse | 0 | 3 | 3 | 3 |
| [E20](E20-suivi-de-cours.md) | Suivi de cours | 13 | 0 | 0 | 0 |
| | **Total** | **70** | **50** | **16** | **4** |

**Les écarts qui comptent le plus** sont des failles d'autorisation, pas des fonctions
manquantes : paiement et onboarding anonymes (US-E07-07, US-E08-01), lecture des réservations,
conversations et portefeuilles d'autrui (US-E06-09, US-E13-01, US-E08-03), modification du
créneau d'un autre professeur (US-E05-02), bannissement inopérant (US-E15-08). Ils ouvrent le
[lot 0 de la feuille de route](../50-backlog/01-roadmap-refactoring.md).
