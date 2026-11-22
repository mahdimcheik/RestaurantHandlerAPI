# E08 — Portefeuille & versements

> Statut : **Stable** · Invariants : [I5 à I9](../20-specs-fonctionnelles/01-regles-metier.md#2-invariants--les-règles-qui-ne-doivent-jamais-être-violées)

## US-E08-01 — Ouvrir mon compte de paiement

**En tant que** professeur
**je veux** relier un compte bancaire à la plateforme
**afin de** pouvoir être payé.

**Priorité :** Doit

**État :** Partielle · **Code :** [`StripeConnectService`](../../RestaurantHandler/Services/Payments/StripeConnectService.cs)
Compte Express, reprise d'un compte existant, confirmation sur
`DetailsSubmitted && PayoutsEnabled` (webhook `account.updated`) conformes. **Écart
critique :** `POST /Payment/connect/account` est anonyme et désigne le professeur par une
adresse e-mail fournie dans la requête — un tiers peut ouvrir le parcours d'un professeur
non confirmé et y saisir ses propres coordonnées bancaires.

**Critères d'acceptation**

- Un parcours d'inscription hébergé chez le prestataire est ouvert depuis mon espace ; la
  plateforme ne collecte aucune coordonnée bancaire.
- Un parcours interrompu peut être repris sans repartir de zéro.
- Mon compte est marqué confirmé lorsque le dossier est **soumis** et les versements
  **autorisés** — les deux conditions, pas une seule.
- Tant que je ne suis pas confirmé, mes réservations relèvent du flux sans paiement et mon
  espace l'explique.

## US-E08-02 — Consulter mon solde

**En tant que** professeur
**je veux** voir ce que j'ai gagné, ce que je dois et ce qui est en route
**afin de** piloter ma trésorerie.

**Priorité :** Doit

**État :** Partielle · **Code :** [`TeacherWalletService`](../../RestaurantHandler/Services/TeacherWalletService.cs)
`GET /TeacherWallets/teacher/{teacherId}/balance`. Écarts : ouvert à tout utilisateur
connecté, sans contrôle de propriété ; signalement des lignes gelées par un litige à
vérifier.

**Critères d'acceptation**

- Quatre valeurs distinctes : **disponible**, **dette**, **solde** (disponible − dette) et
  **versements en cours**.
- Les lignes gelées par un litige sont exclues du disponible et signalées.
- Le calcul exclut les lignes archivées. Voir
  [invariant I9](../20-specs-fonctionnelles/01-regles-metier.md#i9--toute-somme-de-portefeuille-exclut-les-lignes-archivées).
- Le solde peut être **négatif** et l'interface l'assume, avec une explication.

## US-E08-03 — Consulter l'historique de mon portefeuille

**En tant que** professeur
**je veux** parcourir mes écritures
**afin de** comprendre chaque mouvement.

**Priorité :** Doit

**État :** Partielle · **Code :** [`TeacherWalletService`](../../RestaurantHandler/Services/TeacherWalletService.cs)
Liste paginée et détail présents. **Écarts :** la liste
(`POST /TeacherWallets/teacher/{teacherId}/transactions`) est **anonyme** et le détail est
ouvert à tout utilisateur connecté, sans contrôle de propriété.

**Critères d'acceptation**

- Liste paginée, triable et filtrable par type, statut, date et réservation.
- Chaque ligne indique son type — encaissement, remboursement, versement, pénalité,
  récupération — avec un libellé compréhensible, pas un code.
- Le détail d'une ligne renvoie vers la réservation d'origine.
- L'accès est restreint à **mes propres** écritures ; un professeur ne peut pas consulter le
  portefeuille d'un autre.

## US-E08-04 — Être payé automatiquement

**En tant que** professeur
**je veux** recevoir mon argent sans rien demander
**afin de** ne pas avoir à réclamer.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`JobChron`](../../RestaurantHandler/Services/JobChron.cs)
`PayoutTeacherBalances`, toutes les 15 minutes. Ne sélectionne que les réservations
`PAYABLE`.

**Critères d'acceptation**

- Une tâche récurrente sélectionne les crédits non versés dont la réservation est **versable**
  — créneau terminé, fenêtre de litige écoulée, ni annulée, ni refusée, ni en litige.
- Les lignes sont groupées **par réservation** et donnent lieu à **un transfert par
  réservation**. Voir
  [invariant I6](../20-specs-fonctionnelles/01-regles-metier.md#i6--un-versement--une-réservation).
- Le versement est écrit en base **avant** l'appel au prestataire, avec son montant net et la
  dette appliquée, puis marqué payé à l'acquittement.
- Un versement resté en attente est **rejoué tel quel**, jamais recalculé.
- Un échec isolé — solde de la plateforme insuffisant, par exemple — ne bloque que sa propre
  réservation, rejouée au cycle suivant.
- Un professeur sans compte de paiement n'est pas versé : son solde s'accumule jusqu'à
  l'ouverture du compte.

## US-E08-05 — Recouvrer une dette par compensation

**En tant que** plateforme
**je veux** déduire la dette d'un professeur de ses versements suivants
**afin de** récupérer ce qu'il doit sans prélèvement.

**Priorité :** Doit · **Dépend de :** US-E09-04

**État :** Implémentée · **Code :** [`JobChron`](../../RestaurantHandler/Services/JobChron.cs), [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs)
Compensation FIFO, scission avec archivage, versement à zéro sans appel externe. La
présentation brut / dette / net relève du front (`TeacherPayout.DebtApplied` est
disponible).

**Critères d'acceptation**

- Seules les écritures de type **pénalité** et **récupération** constituent une dette. Voir
  [invariant I8](../20-specs-fonctionnelles/01-regles-metier.md#i8--cest-le-type-pas-le-signe-qui-fait-la-dette).
- La dette ouverte est imputée **de la plus ancienne à la plus récente**.
- Un versement intégralement absorbé vaut 0 € et **ne déclenche aucun appel** au prestataire.
- Une dette supérieure au versement est **scindée** : la ligne d'origine est archivée et
  remplacée par deux lignes qui somment à l'identique, l'une réglée, l'autre encore due.
- Le professeur voit clairement, sur son versement, le montant brut, la dette déduite et le
  net transféré.

## US-E08-06 — Encadrer la dette non recouvrable

**En tant que** plateforme
**je veux** empêcher qu'une dette croisse indéfiniment sans contrepartie
**afin de** limiter la perte sur un professeur qui cesse son activité.

**Priorité :** Devrait · **À arbitrer** — voir
[questions ouvertes](../50-backlog/02-questions-ouvertes.md)

**État :** Absente

**Critères d'acceptation**

- Au-delà d'un plafond de dette, les créneaux du professeur ne sont plus réservables et il en
  est informé avec le montant à régulariser.
- L'administration dispose d'une liste des professeurs endettés, triée par ancienneté et
  montant.
- Le plafond est un paramètre de plateforme, pas une valeur en dur.
