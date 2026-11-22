# ADR-0007 — SumUp comme prestataire de paiement

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

Le restaurant utilise SumUp (terminal et, potentiellement, caisse SumUp POS). Un seul
prestataire pour l'en ligne et le sur place simplifie le rapprochement des encaissements.
Le socle d'origine utilisait Stripe, retiré.

## Décision

- Paiement en ligne par **SumUp Online Payments** : le serveur crée un *checkout* (montant
  calculé par le serveur, EUR, référence = commande), le client paie sur le widget ou la page
  SumUp (3-D Secure), SumUp notifie l'API.
- La notification déclenche une **relecture de l'état** du checkout par l'API (clé secrète) ;
  le retour navigateur n'a aucun effet.
- Traitement **idempotent** (identifiant de transaction unique), contrôle montant/devise.
- Tâche planifiée de **réconciliation** des paiements en attente et d'expiration des
  commandes non payées.
- Remboursement total via l'API SumUp.
- Terminal SumUp : utilisé via la caisse certifiée tierce en V1, sans intégration.

## Conséquences

- Intégration HTTP propre dans `Integrations/SumUp/`, testée en sandbox avant la fin de J3.
- Dépendance à un seul prestataire ; l'abstraction reste mince (pas de « multi-PSP »).
- Points à vérifier en J3 : forme exacte et signature des notifications SumUp, délais de
  remboursement, frais.
