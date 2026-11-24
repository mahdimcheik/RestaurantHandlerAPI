# ADR-0007 — SumUp comme prestataire de paiement

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

Greenadine utilise SumUp (terminal et, potentiellement, caisse SumUp POS). Un seul
prestataire pour l'en ligne et le sur place simplifie le rapprochement des encaissements.
Le socle d'origine utilisait Stripe, retiré.

## Décision

- Paiement en ligne par **SumUp Online Payments** : le serveur crée un _checkout_ (montant
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

## Précisions (J3, documentation SumUp lue le 2026-11-22)

- Checkout : `POST /v0.1/checkouts`, montant **décimal en euros**, `checkout_reference` unique
  (64 caractères, ici l'identifiant du `Payment`), page hébergée (`hosted_checkout.enabled`),
  valable 30 min ; `valid_until` = création de la commande + délai d'expiration.
- Notification : `POST` sur `return_url` avec `{ event_type: "CHECKOUT_STATUS_CHANGED", id }`,
  **non signée** ; réponse 2xx vide attendue, nouvelles tentatives à 1 min, 5 min, 20 min, 2 h.
  D'où : relecture systématique, et réconciliation périodique.
- Désactivation : `DELETE /v0.1/checkouts/{id}`, 409 si déjà traité (on relit alors).
- Remboursement : `POST /v1.0/merchants/{code}/payments/{transaction_id}/refunds`, corps
  vide = total (montant en **centimes** si partiel).
- Frais et délais de remboursement : non documentés sur les pages lues, à demander à SumUp.
