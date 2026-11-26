# E05 — Paiement

Rappel de cadrage ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md),
[ADR-0007](../40-decisions/ADR-0007-sumup-psp.md)) : le **seul** moyen de paiement est la
**carte bancaire en ligne via SumUp**, géré par l'application de bout en bout. Ni paiement au
retrait, ni encaissement sur place dans l'application ; une éventuelle vente au comptoir passe
par la caisse du restaurant, hors application.

### US-E05-1 — Payer en ligne

En tant que client, je veux payer ma commande par carte en ligne afin de la retirer sans
attendre.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J3 (checkout à page hébergée SumUp, `paymentUrl`, nouvelle tentative `POST /orders/{id}/payment`, anomalie si montant ≠ total ; bascule au retrait retirée le 2026-11-26), testée contre un faux SumUp ; **paiement sandbox réel à faire** (clés et URL publique) ; écran fait (F8a : carte seule, redirection, nouvel essai depuis le suivi).

**Critères d'acceptation**
- La validation crée un *checkout* SumUp du montant calculé par le serveur, en EUR, avec une
  référence unique liée à la commande ; une tentative `Payment` est enregistrée `Pending`.
- Le client est redirigé vers le paiement SumUp (3-D Secure compris) puis vers la page de suivi.
- La commande ne passe `Confirmée` que sur **confirmation serveur** (webhook SumUp puis
  vérification par appel à l'API SumUp), jamais sur le seul retour navigateur.
- Le montant confirmé par SumUp doit égaler le total de la commande ; sinon la commande est
  mise en anomalie et le Manager alerté.
- Un échec ou un abandon laisse la commande `En attente de paiement` ; le client peut réessayer
  (nouvelle tentative) jusqu'à l'expiration ([US-E05-2](#us-e05-2--traiter-les-notifications-sumup-de-façon-sûre)).
- Paiement en ligne indisponible : la commande ne peut pas être validée (pas d'autre moyen).

### US-E05-2 — Traiter les notifications SumUp de façon sûre

En tant que restaurant, je veux que chaque paiement soit pris en compte exactement une fois
afin de ne perdre ni dupliquer aucune commande.

**Priorité :** V1
**État :** Partielle
**Avancement :** Fait en J3 : notification relue chez SumUp, traitement idempotent et sérialisé par commande, réconciliation Hangfire (`SUMUP_RECONCILE_CRON`, 2 min) et annulation après `OnlinePaymentExpiryMinutes` (30 min) ; à valider en sandbox réel.

**Critères d'acceptation**
- Une notification rejouée ou reçue en double ne produit aucun effet supplémentaire.
- Une notification n'est jamais crue sur parole : l'état est relu chez SumUp.
- Une tâche planifiée (Hangfire) réconcilie les paiements `Pending` de plus de N minutes avec
  l'état SumUp.
- Une commande `En attente de paiement` sans paiement réussi après 30 min (paramètre) est
  annulée automatiquement.

### US-E05-3 — ~~Marquer une commande « payée » sur place~~

En tant que Manager, je veux indiquer qu'une commande a été encaissée sur la caisse certifiée et
par quel moyen afin de savoir ce qui reste à encaisser.

**Priorité :** V1
**État :** Abandonnée
**Abandonnée :** plus d'encaissement sur place dans l'application ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md), 2026-11-26).

**Critères d'acceptation**
- Moyens enregistrables : carte (terminal), espèces, autre ; **plusieurs** moyens et montants
  possibles pour une même commande (paiement mixte), dont la somme doit égaler le total.
- L'enregistrement est horodaté et attribué au Manager.
- Ce marquage est **déclaratif** : il ne constitue pas une vente au sens fiscal (cf.
  [conformité](../20-regles-metier/04-conformite-legale.md)).
- Une commande ne peut pas passer `Retirée` sans être payée.

### US-E05-4 — Rembourser une commande payée en ligne

En tant que Manager, je veux rembourser une commande payée en ligne afin de gérer une
annulation ou une erreur.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J3 : remboursement total automatique à l'annulation par le Manager (le client n'annule plus, ADR-0013) et pour un paiement arrivé sur une commande qui ne l'attendait plus ; `POST /manage/orders/{id}/refund` pour relancer ou décider (non-venue) ; échec signalé en anomalie ; bouton « Rembourser » au back-office fait (F5).

**Critères d'acceptation**
- Le remboursement est total en V1, déclenché via l'API SumUp, et tracé (`Refunded`).
- Il est automatique lorsque le Manager annule une commande payée (à la demande du client,
  par téléphone ou sur place, ou sur imprévu).
- Un remboursement partiel est une V2.

### US-E05-5 — Délivrer un justificatif de paiement en ligne

En tant que client ayant payé en ligne, je veux un justificatif de mon achat afin de le
présenter (note de frais).

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J3 (`GET /orders/{id}/receipt`, commande payée uniquement) ; contenu à valider (question 2) ; page faite (F8b : `/commandes/:id/recu`, imprimable).

**Critères d'acceptation**
- Une page « reçu » de la commande, accessible depuis l'historique, affiche : restaurant
  (raison sociale, adresse, SIRET, n° TVA), date, numéro, lignes, total HT / TVA par taux / TTC,
  moyen de paiement.
- Le contenu exact est à valider avec l'expert-comptable (question ouverte n° 2).

### US-E05-6 — Demander une facture professionnelle

En tant que client professionnel, je veux demander une facture au nom de mon entreprise afin de
la recevoir en facture électronique.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite le 2026-11-26 : profil professionnel (`GET/PUT/DELETE /users/me/business-profile`), `OrderCreate.requestInvoice`, demande enregistrée à la confirmation du paiement, suivi `GET /orders/{id}/invoice-request`, liste et relance Manager (`/manage/invoice-requests`) ; transmission derrière `IEInvoicingGateway` ([ADR-0014](../40-decisions/ADR-0014-facturation-electronique-interface.md)), **aucune plateforme agréée branchée** : les demandes restent `Pending`. Écrans faits : `/compte/professionnel`, case « facture » à la validation, statut sur le reçu ; liste Manager à faire.

**Critères d'acceptation**
- Un client devient professionnel en renseignant SIREN, raison sociale, adresse de facturation
  et, facultativement, SIRET et n° de TVA. Le SIREN (et le SIRET) est contrôlé par sa clé, sans
  appel externe ; le n° de TVA doit correspondre au SIREN et en est calculé s'il est omis. Il
  peut redevenir particulier.
- À la validation, un client professionnel voit une case « Je souhaite une facture », cochée par
  défaut. Demander une facture sans profil professionnel est refusé.
- La demande n'est créée qu'à la **confirmation du paiement**, une seule par commande (une
  notification SumUp rejouée n'en crée pas d'autre). Elle fige acheteur et vendeur
  ([ADR-0008](../40-decisions/ADR-0008-centimes-snapshots.md)).
- Elle est transmise à la plateforme agréée par une tâche récurrente ; un échec technique est
  retenté (10 fois au plus), puis un Manager peut la relancer.
- Le client voit où en est sa facture depuis le reçu ; seul le Manager voit les erreurs.
- Numérotation légale des factures, choix de la plateforme et e-reporting : questions ouvertes
  16 à 18.

