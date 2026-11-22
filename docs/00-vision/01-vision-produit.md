# Vision produit

> Statut : **Stable**

## Le problème

Trouver un professeur particulier se fait encore largement par bouche-à-oreille, petites
annonces et paiement de la main à la main. Les deux côtés y perdent :

- **L'élève** ne sait pas si le professeur est réel, compétent et disponible ; il paie
  d'avance sans garantie, ou après coup sans trace.
- **Le professeur** court après les paiements, n'a aucune preuve d'activité, et doit
  gérer lui-même planning, relances et facturation.
- **Personne n'arbitre** quand le cours ne se passe pas comme prévu.

## La proposition de valeur

BonProf est un **tiers de confiance** entre les deux : la plateforme encaisse l'argent
de l'élève au moment de la réservation, le garde le temps que le cours ait lieu, puis le
reverse au professeur. Elle prélève une commission de 5 % pour ce service.

Concrètement, chaque côté obtient :

| | Élève | Professeur |
|---|---|---|
| Confiance | professeurs vérifiés (identité, diplômes, SIRET), avis adossés à de vrais cours | paiement garanti dès la réservation |
| Praticité | recherche géolocalisée, réservation en ligne, paiement par carte | planning en ligne, catalogue de cours, facturation automatique |
| Recours | annulation cadrée, remboursement, ouverture d'un litige arbitré | fenêtre de litige bornée, versement automatique par défaut |

## Le principe directeur

**Par défaut, le professeur est payé.** Le versement est déclenché par le temps qui passe,
pas par une confirmation. Aucune partie ne détient un droit de veto sur l'argent de
l'autre : pour empêcher un versement, l'élève doit poser un acte explicite, nommé et
journalisé, qui déclenche un arbitrage humain.

Ce choix découle d'un constat : **un cours en présentiel ne se prouve pas.** L'objectif
n'est donc pas la preuve, mais de rendre le chemin honnête facile et la triche coûteuse et
traçable. Voir [E10 — Preuve de réalisation](../10-user-stories/E10-preuve-de-realisation.md).

## Périmètre de la v1

La v1 est le périmètre de BonProfAPI tel qu'il est en production. Le refactoring en cours le rend
sûr et maintenable, et comble les écarts avec les specs. L'état de chaque fonctionnalité est donné dans le
[récapitulatif des user stories](../10-user-stories/README.md#état-dimplémentation).

- comptes élève / professeur / administrateur, avec vérification documentaire et SIRET ;
- profils publics, catalogue de cours, recherche géolocalisée et favoris ;
- gestion de planning et réservation de créneaux ;
- paiement par carte, portefeuille professeur, versements automatiques ;
- annulations, remboursements, litiges ;
- avis et notation ;
- messagerie temps réel et notifications ;
- facturation PDF, comptabilité par commande, bilans mensuels et exports ;
- back-office d'administration.

## Non-objectifs de la v1

Explicitement hors périmètre, pour éviter la dérive :

- **Fournisseur de visioconférence intégré.** Le créneau peut être de type `Visio`, mais
  le lien de connexion est fourni par le professeur. Voir
  [questions ouvertes](../50-backlog/02-questions-ouvertes.md).
- **Suivi pédagogique** (progression de l'élève, devoirs, corrections, bibliothèque de
  ressources). Le README historique le promettait ; aucun écran n'a jamais été livré.
- **Abonnements et forfaits.** Une réservation = un cours = un paiement.
- **Application mobile native.** Le web responsive couvre le besoin.
- **Multilingue.** L'architecture est prête (référentiels traduisibles côté serveur), mais
  seul le français est livré. Voir [E18](../10-user-stories/E18-referentiels-i18n.md).
- **Marketplace multi-professeurs dans un même panier.** Une commande porte sur un seul
  professeur, par construction. Voir [ADR-0003](../40-decisions/ADR-0003-commande-mono-prof.md).

## Indicateurs de succès

| Indicateur | Pourquoi il compte |
|---|---|
| Taux de conversion recherche → réservation payée | mesure l'utilité du moteur de recherche |
| Part des commandes payées avant expiration (30 min) | mesure la friction du tunnel de paiement |
| Taux de litige ouvert par réservation | doit rester marginal ; au-delà, le modèle de confiance ne tient pas |
| Délai moyen de vérification d'un professeur | goulot d'étranglement de l'acquisition côté offre |
| Part des professeurs avec Stripe Connect actif | un professeur non onboardé accumule un solde qu'il ne touche pas |
| Dette professeur non recouvrée | fuite d'argent structurelle, voir [E09](../10-user-stories/E09-annulation-remboursement.md) |
