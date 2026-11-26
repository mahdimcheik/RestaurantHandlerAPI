# ADR-0006 — Caisse certifiée tierce en V1, caisse maison NF525 en V2

**Statut :** Remplacée par [ADR-0013](ADR-0013-paiement-en-ligne-seul.md) (2026-11-26) : plus aucun encaissement sur place dans l'application
**Date :** 2026-11-22

## Contexte

Greenadine encaisse sur place (comptoir, paiement au retrait ; carte, espèces, paiement
mixte). En France, un logiciel qui enregistre ces paiements doit être **certifié** (NF525 ou
équivalent) : inaltérabilité, sécurisation, conservation, archivage, clôtures. La
certification d'un logiciel maison demande des développements spécifiques (journal chaîné,
clôtures, archivage 6 ans) puis un audit de plusieurs mois et un coût de plusieurs milliers
d'euros. L'ouverture est visée **sous 3 mois**, en développement solo.

Une caisse maison conforme dès la V1 a été envisagée puis écartée : elle rendait l'échéance
intenable.

## Décision

- **V1** : toute vente sur place est **encaissée et enregistrée sur une caisse certifiée
  tierce** (SumUp POS ou équivalent). L'application prend la commande, l'envoie en cuisine,
  suit son état, et le Manager y **déclare** l'encaissement (moyens et montants) pour savoir
  ce qui reste à encaisser. L'application n'est pas un système de caisse.
- **Paiements en ligne** : traités dans l'application via SumUp (vente à distance) ; leur
  traitement fiscal exact est à valider avec l'expert-comptable.
- **V2** : caisse maison certifiée (journal inaltérable, Z de caisse, tickets, imprimante
  ePOS/WebPRNT), qui supprimera la double saisie.

## Conséquences

- **Double saisie** au comptoir en V1 (application pour la cuisine, caisse tierce pour
  l'encaissement) : coût opérationnel accepté.
- Les statistiques de l'application sont des **indicateurs de pilotage**, pas la référence
  comptable.
- Pour préparer la V2 sans refonte, la V1 garantit déjà : snapshots immuables, historique
  des statuts, aucune suppression physique de commande.
- Pas d'imprimante pilotée par l'application en V1.
