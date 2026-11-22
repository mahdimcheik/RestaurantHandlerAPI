# E05 — Paiement

Rappel de cadrage ([ADR-0006](../40-decisions/ADR-0006-caisse-certifiee-tierce-v1.md),
[ADR-0007](../40-decisions/ADR-0007-sumup-psp.md)) :
- **en ligne** : SumUp, l'application gère le paiement de bout en bout ;
- **sur place** (comptoir, retrait) : la vente est **encaissée et enregistrée sur la caisse
  certifiée tierce** ; l'application n'enregistre qu'un **marquage « payée »** pour son suivi.

### US-E05-1 — Payer en ligne

En tant que client, je veux payer ma commande par carte en ligne afin de la retirer sans
attendre.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J3 (checkout à page hébergée SumUp, `paymentUrl`, nouvelle tentative `POST /orders/{id}/payment`, bascule `POST /orders/{id}/pay-on-site`, anomalie si montant ≠ total), testée contre un faux SumUp ; **paiement sandbox réel à faire** (clés et URL publique) ; écran en J4.

**Critères d'acceptation**
- La validation crée un *checkout* SumUp du montant calculé par le serveur, en EUR, avec une
  référence unique liée à la commande ; une tentative `Payment` est enregistrée `Pending`.
- Le client est redirigé vers le paiement SumUp (3-D Secure compris) puis vers la page de suivi.
- La commande ne passe `Confirmée` que sur **confirmation serveur** (webhook SumUp puis
  vérification par appel à l'API SumUp), jamais sur le seul retour navigateur.
- Le montant confirmé par SumUp doit égaler le total de la commande ; sinon la commande est
  mise en anomalie et le Manager alerté.
- Un échec ou un abandon laisse la commande `En attente de paiement` ; le client peut réessayer
  (nouvelle tentative) ou passer au paiement au retrait.

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

### US-E05-3 — Marquer une commande « payée » sur place

En tant que Manager, je veux indiquer qu'une commande a été encaissée sur la caisse certifiée et
par quel moyen afin de savoir ce qui reste à encaisser.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`POST /manage/orders/{id}/payments`, paiement mixte, auteur, garde avant `Retirée`) ; écran en J5.

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
**Avancement :** API faite en J3 : remboursement total automatique à l'annulation (client ou Manager) et pour un paiement arrivé sur une commande qui ne l'attendait plus ; `POST /manage/orders/{id}/refund` pour relancer ou décider (non-venue) ; échec signalé en anomalie.

**Critères d'acceptation**
- Le remboursement est total en V1, déclenché via l'API SumUp, et tracé (`Refunded`).
- Il est automatique lorsque le client annule avant préparation.
- Un remboursement partiel est une V2.

### US-E05-5 — Délivrer un justificatif de paiement en ligne

En tant que client ayant payé en ligne, je veux un justificatif de mon achat afin de le
présenter (note de frais).

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J3 (`GET /orders/{id}/receipt`, commande payée uniquement) ; contenu à valider (question 2) ; page en J4.

**Critères d'acceptation**
- Une page « reçu » de la commande, accessible depuis l'historique, affiche : restaurant
  (raison sociale, adresse, SIRET, n° TVA), date, numéro, lignes, total HT / TVA par taux / TTC,
  moyen de paiement.
- Le contenu exact est à valider avec l'expert-comptable (question ouverte n° 2).
