# ADR-0012 — Code de présence détenu par l'élève, sans enjeu par construction

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-14

## Contexte

Il n'existe **aucune preuve fiable** qu'un cours en présentiel a eu lieu. Et toute action de
confirmation détenue par une seule partie devient un **droit de veto** pour cette partie :

- si l'élève doit confirmer pour que le professeur soit payé, l'élève malhonnête refuse et
  obtient un cours gratuit ;
- si le professeur détient le code, il se confirme lui-même et le mécanisme ne prouve rien.

Trois options se présentaient : conserver le code tel qu'il est (détenu par l'élève, saisi par
le professeur), passer à un échange mutuel, ou le supprimer entièrement.

## Décision

**Le code reste détenu par l'élève et saisi par le professeur — et il n'est jamais une condition
de paiement.**

C'est la seconde moitié qui compte. Le versement est déclenché par le temps : une réservation
devient versable parce que le créneau est terminé et la fenêtre de contestation écoulée, que le
code ait été saisi ou non. La saisie ne fait que **raccourcir le trajet** en posant l'état livré ;
son absence ne bloque rien.

Le mécanisme est donc **sans enjeu par construction**, et c'est précisément ce qui le rend
acceptable : puisqu'il n'ouvre aucun droit, sa direction importe peu, et personne ne détient de
veto. Un échange mutuel doublerait la friction pour zéro gain.

Ce qu'il apporte réellement est ailleurs : un **horodatage** de saisie, conservé, exploitable
comme élément d'appréciation en cas de litige. Supprimer le code reviendrait à perdre ce signal.

Trois conséquences de conception en découlent :

- le code n'est produit qu'à la **confirmation du paiement** — seul un cours payé peut être
  contesté, donc seul un cours payé a besoin de ce signal ;
- il n'est visible que de l'**élève propriétaire**, et seulement à partir du début du créneau :
  un professeur qui le verrait pourrait le saisir seul ;
- la saisie se **verrouille temporairement** après un nombre limité d'essais erronés, et la date
  de déverrouillage est renvoyée. Le verrou n'est pas là pour protéger de l'argent — il n'y en a
  pas en jeu — mais pour qu'un code deviné par énumération ne produise pas une trace fausse.

## Conséquences

**Ce qu'on gagne**

- Aucune partie ne détient de veto sur le paiement de l'autre.
- Un horodatage utile à l'arbitrage, obtenu sans rien exiger de personne.
- Une trésorerie accélérée pour les professeurs dont les élèves jouent le jeu.

**Ce que ça coûte**

- Le signal est **faible** : un élève complaisant peut donner son code sans que le cours ait eu
  lieu, et l'horodatage ne vaut alors rien. Il ne doit jamais être présenté comme une preuve.
- Un mécanisme visible dans l'interface pour un bénéfice que l'utilisateur ne perçoit pas — d'où
  l'obligation d'écrire noir sur blanc, sur l'écran, qu'il **n'est pas obligatoire** et ne
  conditionne aucun remboursement.

**Ce qu'on surveille**

Le taux de saisie. S'il s'effondre, le mécanisme ne produit plus de signal et ne justifie plus
sa place dans le parcours.

**Ce qui le remplacerait.** En distanciel, un événement « a rejoint l'appel » fourni par un
prestataire de visioconférence intégré serait une preuve autrement solide. Elle n'est pas
disponible en v1 ; c'est elle qui rendrait cet ADR caduc pour ce type de créneau.

## État dans le code

Depuis le lot 4 : le code est généré par le webhook `checkout.session.completed`
([`CheckoutWebhookService`](../../RestaurantHandler/Services/Payments/CheckoutWebhookService.cs)) ; il n'est exposé que par le détail
de la réservation, à l'élève propriétaire et à partir du début du créneau ; la saisie
(`ReservationService.ValidateReservationAsync`) n'est acceptée qu'en flux payant, du début du
créneau à l'échéance de contestation. Verrouillage : `HardCode.VALIDATION_CODE_MAX_ATTEMPTS`
(5) essais, puis `VALIDATION_CODE_LOCKOUT` (15 min). L'horodatage de saisie est la ligne
d'historique `ACCEPTED → DELIVERED`.
