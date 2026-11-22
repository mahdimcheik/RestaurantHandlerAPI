# ADR-0010 — Flux sans paiement pour les professeurs non payables

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

Un professeur n'est payable que s'il est **vérifié documentairement** et **confirmé côté
bancaire**. Ces deux conditions prennent du temps : revue humaine des pièces d'un côté,
parcours d'inscription chez le prestataire de l'autre.

Que faire d'un élève qui veut réserver chez un professeur qui n'a pas franchi ces deux sas ?

- **Bloquer la réservation** : l'offre est invisible tant que le dossier n'est pas complet,
  ce qui décourage les nouveaux professeurs — or l'acquisition côté offre est le goulot
  d'étranglement.
- **Encaisser quand même** : la plateforme détient de l'argent pour un professeur qu'elle ne
  peut pas payer, sans mécanisme de sortie. C'est une dette de la plateforme envers lui, et le
  risque le plus grave.

## Décision

Un troisième chemin : la commande est créée avec une **exigence de paiement figée à `faux`**,
déterminée à partir de l'état du professeur au moment de la création.

Dans ce mode :

- **aucun argent ne circule** : pas de page de paiement, pas d'échéance, pas d'encaissement,
  pas d'écriture au grand livre ;
- le professeur **accepte ou refuse manuellement** sous 48 heures ;
- sans réponse, la réservation est automatiquement refusée et le créneau libéré ;
- une tâche fait ensuite passer la réservation à l'état terminé, pour que l'élève puisse
  déposer un avis.

## Conséquences

**Ce qu'on gagne**

- Un professeur peut travailler dès son inscription, ce qui raccourcit son délai de premier
  cours.
- La plateforme ne détient jamais d'argent qu'elle ne peut pas reverser.
- Le parcours d'acceptation manuelle donne un signal utile : un professeur qui ne répond pas
  en 48 heures est probablement inactif.

**Ce que ça coûte**

- **Deux parcours de réservation à maintenir et à tester**, avec des états différents.
- L'élève ne comprend pas spontanément pourquoi il paie chez un professeur et pas chez un
  autre : l'interface doit l'expliquer, pas seulement le subir.
- Le cours a lieu **hors plateforme** financièrement : ni commission, ni facture, ni recours
  en cas de litige. Il faut le dire clairement à l'élève.
- L'exigence de paiement est **figée à la création** : un professeur confirmé entre-temps ne
  transforme pas rétroactivement une commande existante. C'est volontaire — une commande dont
  les règles changent en cours de route serait pire.

**Ce qu'on surveille**

La part de réservations en flux sans paiement. Si elle est importante, c'est que le sas de
vérification est trop lent — et le problème est là, pas dans ce mécanisme.

## État dans le code

`Order.PaymentRequired` ([`Order.cs`](../../RestaurantHandler/Domain/Entities/Order/Order.cs)),
`HardCode.TEACHER_RESPONSE_WINDOW` (48 h), tâches `expire-stale-no-payment-reservations` et
`complete-no-payment-reservations` ([`Scheduler.cs`](../../RestaurantHandler/Utilities/Scheduler.cs)).
