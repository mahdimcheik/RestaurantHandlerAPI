# Conformité légale

> Ce document recense les obligations identifiées ; il **ne remplace pas** l'avis d'un
> expert-comptable. Chaque point marqué « à valider » figure dans les
> [questions ouvertes](../50-feuille-de-route/02-questions-ouvertes.md).

## 1. Logiciel de caisse (loi anti-fraude TVA)

**Règle :** un assujetti à la TVA qui enregistre les paiements de clients particuliers dans un
logiciel ou système de caisse doit utiliser un système satisfaisant aux conditions
d'**inaltérabilité, sécurisation, conservation et archivage** (CGI art. 286, I-3° bis),
justifié par une **certification** (NF525 ou équivalent LNE). L'attestation individuelle de
l'éditeur n'est plus admise depuis la loi de finances 2025 *(à valider)*. Sanction : amende par
système non conforme.

**Décision V1** ([ADR-0006](../40-decisions/ADR-0006-caisse-certifiee-tierce-v1.md)) :

- toute vente encaissée sur place (comptoir, paiement au retrait ; CB, espèces, mixte) est
  **saisie et encaissée sur une caisse certifiée tierce** ;
- l'application **ne fait pas office de caisse** : son marquage « payée » est une information de
  suivi, et ses statistiques sont des indicateurs de pilotage ;
- les paiements **en ligne** via SumUp sont des ventes à distance encaissées par un prestataire
  de paiement ; leur traitement fiscal (enregistrement dans la caisse certifiée ou non,
  justificatif à délivrer) est **à valider**.

**Conséquence opérationnelle :** double saisie au comptoir (app pour la cuisine + caisse tierce
pour l'encaissement). C'est le prix de la V1 ; la V2 la supprime.

**V2 — caisse maison certifiée :** journal des ventes chaîné et signé, clôtures journalières /
mensuelles / annuelles (« Z »), tickets numérotés, gestion des annulations par écritures
négatives, archivage 6 ans, impression ePOS/WebPRNT, puis audit de certification. Pour ne pas
tout refaire, la V1 respecte déjà : snapshots immuables, historique des statuts, aucune
suppression physique de commande.

## 2. Ticket et justificatif

- Depuis 2023, le ticket de caisse n'est plus imprimé systématiquement mais **reste dû à la
  demande** du client : il est fourni par la caisse certifiée tierce en V1.
- Paiement en ligne : la page reçu ([US-E05-5](../10-cahier-des-charges/E05-paiement.md#us-e05-5--délivrer-un-justificatif-de-paiement-en-ligne))
  porte les mentions utiles ; contenu exact **à valider**.
- Affichage obligatoire des prix TTC (carte en ligne comprise).

## 3. TVA en restauration à emporter

| Cas courant | Taux usuel |
|---|---|
| Aliments à consommer immédiatement (sandwichs, burgers, plats chauds) | 10 % |
| Boissons non alcoolisées | 10 % |
| Produits conditionnés pouvant être conservés (ex. canette fermée, dessert emballé) | 5,5 % ou 10 % selon le produit |
| Boissons alcoolisées | 20 % |

Chaque produit porte son taux ; **le taux par produit et la ventilation de TVA d'un menu
mêlant plusieurs taux sont à valider** avec l'expert-comptable. Le modèle permet un taux par
menu ; une ventilation par composant pourrait être nécessaire.

## 4. Allergènes (règlement UE 1169/2011 « INCO »)

L'information sur les **14 allergènes** est **obligatoire** pour les denrées non
préemballées, y compris en vente à distance où elle doit être disponible **avant** l'achat.
En V1, l'application ne gère pas les allergènes ([US-E02-8](../10-cahier-des-charges/E02-carte.md#us-e02-8--afficher-les-allergènes) en V2) :

- sur place : affichage ou classeur à disposition ;
- en ligne : **à minima** un lien vers un document des allergènes depuis la carte et une
  mention « informations allergènes disponibles » — **à valider**, car c'est un risque de
  non-conformité pour la vente en ligne.

## 5. Données personnelles (RGPD)

- Données collectées : identité, e-mail, téléphone, historique de commandes. Base légale :
  exécution du contrat (commande) ; pas de prospection en V1.
- Politique de confidentialité et CGU/CGV acceptées à l'inscription (consentements du socle).
- Effacement : anonymisation du compte, conservation des commandes (obligation comptable
  10 ans pour les pièces).
- Sous-traitants : hébergeur (VPS), SumUp, Google (connexion), stockage objet OVH.
- Pas de cookie tiers de mesure d'audience en V1 (sinon : bandeau de consentement).

## 6. CGV de vente en ligne

À rédiger (hors code) : identité du vendeur, prix TTC, modalités de retrait, de paiement,
d'annulation et de remboursement, non-application du droit de rétractation aux denrées
périssables (C. consom. L221-28), médiation de la consommation.
