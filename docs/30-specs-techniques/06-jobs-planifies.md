# Tâches planifiées

> Statut : **Stable** · Flux : [flux d'argent](../20-specs-fonctionnelles/04-flux-argent.md)

## Le principe fondateur

**Des tâches récurrentes qui interrogent la base, jamais une tâche programmée par
réservation.**

Une première version programmait un travail par réservation dans une table en mémoire. Un
redémarrage ou un déploiement perdait le versement — c'est-à-dire de l'argent. Cette table a
été retirée ; l'ancienne tâche `pay-passed-reservations` est désenregistrée au démarrage. La base
de données est la **seule** source de vérité : chaque cycle recalcule ce qu'il y a à faire à
partir de l'état persisté.

Conséquences directes :

- un cycle manqué est rattrapé au suivant, sans intervention ;
- une tâche est **rejouable sans effet de bord** ;
- redéployer pendant un traitement d'argent est sans danger.

## Catalogue

| Tâche | Fréquence | Objet |
|---|---|---|
| Libération des commandes impayées | 5 min | expire la session, annule les réservations, libère les créneaux des commandes dont l'échéance est passée |
| Expiration des demandes sans réponse | 5 min | refuse automatiquement les réservations en flux sans paiement laissées sans réponse pendant 48 h |
| Passage en versable | 15 min | rend versables les réservations payées dont la fenêtre de contestation est écoulée |
| Versement des soldes | 15 min | un transfert par réservation versable, dette déduite |
| Rejeu des versements en attente | 15 min | rejoue **tel quel** un versement figé mais non acquitté |
| Exécution des remboursements | 15 min | exécute les remboursements enregistrés en attente |
| Clôture des réservations sans paiement | 15 min | fait passer à l'état terminé les réservations du flux sans paiement, pour ouvrir le droit à l'avis |
| Résolution automatique des litiges | 1 h | tranche en faveur du professeur les litiges ouverts depuis plus de 7 jours ; relance l'administration avant l'échéance |
| Purge des créneaux passés | quotidienne | archive les créneaux passés sans réservation vivante |
| Régénération du plan de site | quotidienne | reconstruit le plan de site incluant les fiches professeurs |
| Réconciliation des frais | différée, avec réessais | renseigne frais et net une fois l'opération réglée chez le prestataire |
| Livraison des e-mails | à la demande, avec réessais | envoi effectif, 5 tentatives à délais croissants |

## Garanties par tâche d'argent

### Versement des soldes

```
pour chaque réservation versable, du plus ancien au plus récent :
    calculer le net = crédits − dette imputée (la plus ancienne d'abord)
    ÉCRIRE EN BASE le versement EN ATTENTE
        montant net, dette appliquée,
        rattachement des lignes réglées,
        écriture de versement négative
    si net = 0 :
        marquer payé          (compensation, aucun appel externe)
    sinon :
        APPELER le prestataire   clé = payout_{réservation}
        marquer payé + enregistrer l'identifiant de transfert
        réservation → terminée
```

**Trois protections superposées :**

1. **Le rattachement en base** empêche de refaire le travail : seules les lignes non
   rattachées sont candidates, et le rattachement est posé dans la même transaction.
2. **La clé d'idempotence** protège le cas « l'appel externe réussit mais l'enregistrement
   local échoue ». Elle est dérivée de la **réservation**, donc stable.
3. **Le figement avant l'appel** garantit qu'un rejeu envoie exactement les mêmes paramètres,
   ce que le prestataire exige.

**Traitement des échecs :** un solde de plateforme insuffisant est **transitoire** — la
réservation est reprise au cycle suivant, sans bloquer les autres. Un échec définitif est
journalisé et signalé.

### Exécution des remboursements

Même schéma : ligne enregistrée en attente au moment de l'annulation, exécutée par le cycle,
clé dérivée de la réservation. Un échec est repris au cycle suivant.

### Libération des commandes impayées

Traitement idempotent : une commande déjà libérée est ignorée. La session de paiement est
expirée chez le prestataire **avant** de vider le panier, pour qu'aucun lien ne survive.

## Règles transverses

| Règle | Énoncé |
|---|---|
| **Aucun déclenchement manuel** d'un mouvement d'argent | y compris par un administrateur |
| **Une tâche est idempotente** | la relancer deux fois donne le même résultat |
| **Une tâche traite un lot borné** | et poursuit au cycle suivant : jamais de traitement illimité |
| **Une tâche ne dépend pas de son cycle précédent** | elle recalcule tout depuis la base |
| **Un échec unitaire n'interrompt pas le lot** | il est journalisé et repris |
| **Chaque exécution est journalisée** | nombre traité, en échec, durée |
| **Concurrence maîtrisée** | deux instances ne traitent jamais la même ligne : le verrou est le rattachement en base |

## Exploitation

- **Le tableau de bord exige un administrateur authentifié** et est monté après
  l'authentification. C'est une correction de sécurité majeure par rapport au code actuel.
- **Aucun argument de tâche ne contient de contenu sensible.** Les arguments sont persistés
  en clair et visibles dans ce tableau de bord : on n'y passe que des identifiants, jamais un
  corps d'e-mail rendu ni un lien de réinitialisation.
- Le stockage des tâches partage la base applicative, avec un délai de commande élargi.
- Une tâche en échec répété **remonte en supervision**, elle ne se contente pas d'attendre.
- Les tâches obsolètes sont explicitement retirées à l'enregistrement, pour qu'un ancien
  planificateur ne survive pas à un déploiement.

## Supervision

| Indicateur | Seuil d'alerte |
|---|---|
| Versements restés en attente | plus d'un cycle |
| Remboursements en attente | plus de deux cycles |
| Commandes expirées non libérées | toute occurrence |
| Litiges ouverts depuis plus de 5 jours | toute occurrence |
| E-mails en échec définitif | toute occurrence |
| Durée d'une tâche d'argent | au-delà de son intervalle |

## État dans le code

Tâches récurrentes enregistrées au démarrage par `HangfireJobsBootstrapper`
([`Scheduler.cs`](../../RestaurantHandler/Utilities/Scheduler.cs)) ; implémentation dans
[`JobChron`](../../RestaurantHandler/Services/JobChron.cs) sauf mention contraire.

| Identifiant Hangfire | Cron | Méthode | Tâche du catalogue |
|---|---|---|---|
| `clean-stale-orders` | `*/5 * * * *` | `CleanStalePendingOrders` | libération des commandes impayées |
| `expire-stale-no-payment-reservations` | `*/5 * * * *` | `ExpireStaleNoPaymentReservations` | expiration des demandes sans réponse |
| `payout-teacher-balances` | `*/15 * * * *` | `PayoutTeacherBalances` (appelle d'abord `RetryPendingPayouts`, puis `MarkReservationsPayable`) | rejeu des versements, passage en versable **et** versement |
| `process-pending-refunds` | `*/15 * * * *` | `ProcessPendingRefunds` | exécution des remboursements |
| `complete-no-payment-reservations` | `*/15 * * * *` | `CompleteNoPaymentReservations` | clôture des réservations sans paiement |
| `DailyCleanSlots` | quotidien | `SlotCleaningJob.CleanSlots` ([`Scheduler.cs`](../../RestaurantHandler/Utilities/Scheduler.cs)) | purge des créneaux passés |

Tâches différées : `PaymentService.BackfillStripeFeesAsync` (10 essais) et
`MailService.DeliverAsync` (5 essais à 30 s, 1, 2 et 5 min). Tâches retirées au démarrage :
`pay-passed-reservations`, `delete-passed-jobs`.

**Absentes :** résolution automatique des litiges, régénération du plan de site.

**Écarts avec les règles transverses :**

- **Transfert manuel** exposé par l'API, contrairement à la première règle (voir
  [sécurité](04-securite.md#failles-et-écarts-actuels)).
- **Lots non bornés** : aucune tâche ne limite le nombre de lignes traitées par cycle.
- **Journalisation** par `Console.WriteLine`, sans durée ni nombre d'échecs.
- **Tableau de bord** public et arguments d'e-mail sensibles (voir ci-dessus).
- **Supervision** : aucun indicateur n'est exposé.
