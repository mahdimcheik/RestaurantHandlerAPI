# ADR-0014 — Facturation électronique via une plateforme agréée derrière une interface

**Statut :** Accepté
**Date :** 2026-11-26

## Contexte

Avec la réforme de la facturation électronique, une vente à une entreprise (B2B) devra être
facturée par voie électronique, en passant par une **plateforme agréée** (PA, ex-PDP) :
réception obligatoire pour toutes les entreprises au 1er septembre 2026, émission obligatoire
pour les TPE et PME au 1er septembre 2027. Les ventes aux particuliers ne sont pas concernées
par la facture électronique mais par l'e-reporting.

Une partie des clients du restaurant sont des professionnels (repas d'équipe, note de frais).
La plateforme n'est pas encore choisie, et le restaurant pourrait en changer plus tard.

## Décision

- Un client peut se déclarer **professionnel** : table `BusinessProfiles` (1-1 avec
  l'utilisateur, facultative). Le statut est **déclaratif** : SIREN et SIRET contrôlés par
  leur clé de Luhn, n° de TVA dérivé du SIREN, sans appel à l'INSEE
  (`Domain/Policies/BusinessIdentityPolicy.cs`).
- La demande de facture se fait **au panier** (`OrderCreate.requestInvoice`). L'`InvoiceRequest`
  est créée dans la **même sauvegarde que la confirmation du paiement** et fige acheteur et
  vendeur ([ADR-0008](ADR-0008-centimes-snapshots.md)) : c'est une **outbox**. Une tâche
  Hangfire récurrente la transmet ensuite, avec relances (même logique de relecture
  idempotente que pour SumUp, [ADR-0007](ADR-0007-sumup-psp.md)).
- La transmission passe par une **interface** `IEInvoicingGateway`
  (`Integrations/EInvoicing/`). Elle reçoit un `EInvoiceDocument` neutre (sans entité EF ni
  format de plateforme), et chaque adaptateur le traduit dans le format de sa plateforme
  (Factur-X, UBL, CII).
- L'implémentation est choisie **une fois au démarrage** par `EINVOICING_PROVIDER`.
  `none` (par défaut) enregistre `NoEInvoicingGateway` : les demandes restent en attente,
  sans erreur, jusqu'au branchement d'une plateforme.
- **Pas de factory.** Une seule plateforme est active à la fois : l'injection de dépendances
  suffit (patterns Strategy et Adapter). On n'ajoutera une factory, par exemple avec les
  services à clé de .NET, que si plusieurs plateformes doivent coexister, comme pendant une
  migration de l'une à l'autre.

## Conséquences

- Brancher une plateforme revient à écrire un adaptateur, à l'ajouter au `switch` de
  `Program.cs` et à lui donner sa configuration. Ni le modèle, ni le parcours de commande, ni
  le front ne changent.
- La tâche récurrente `einvoicing-submit` n'est planifiée que si une plateforme est
  configurée.
- Hors de cette décision, en questions ouvertes : la **numérotation légale** des factures
  (suite chronologique sans trou, gérée par la plateforme ou par une séquence dédiée), le
  choix de la plateforme, l'**e-reporting** des ventes aux particuliers et le statut du reçu
  lorsqu'une facture est émise.
