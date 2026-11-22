# E11 — Litiges

> Statut : **Stable** · Modèle de confiance : [E10](E10-preuve-de-realisation.md)
>
> Le litige est le **seul** mécanisme qui bloque le versement d'un professeur.

## Principes

- **Les preuves ne sont pas fiables.** L'administrateur tranche au jugement, pas sur des
  justificatifs qui n'existent pas. On ne demande donc aucune pièce à l'élève.
- **Le professeur et l'administrateur sont notifiés en même temps.** Pas de phase
  séquentielle « le professeur d'abord, l'administrateur ensuite ».
- **Seul l'élève peut ouvrir un litige.** Le professeur, lui, est payé par défaut : il n'a
  rien à contester.
- **La pression de long terme passe par les avis**, pas par l'arbitrage. Voir
  [E12](E12-avis-reputation.md).

---

## US-E11-01 — Contester une réservation

**En tant qu'** élève
**je veux** signaler que le cours ne s'est pas déroulé comme prévu
**afin de** ne pas payer un cours que je n'ai pas eu.

**Priorité :** Doit

**État :** Partielle · **Code :** [`DisputeService`](../../RestaurantHandler/Services/DisputeService.cs)
`POST /Dispute/student/open` : fenêtre de contestation, propriété, gel par transition
et notification simultanée du professeur et des administrateurs conformes. Écarts : motif en
texte libre, sans liste ; erreurs levées en `Exception` générique. Le flux sans paiement est
refusé et l'unicité est tenue en base (lot 4).

**Critères d'acceptation**

- Accessible depuis le détail de ma réservation, avec un **motif obligatoire** choisi dans une
  liste puis précisé en texte libre.
- Ouverture autorisée **uniquement entre la fin du créneau et l'échéance de contestation**.
  Avant le cours, c'est une annulation ; après l'échéance, c'est trop tard et le message
  l'explique.
- L'ouverture passe immédiatement la réservation en litige, ce qui **gèle le crédit** du
  professeur : la tâche de versement l'exclut.
- Le professeur **et** l'administration sont notifiés au même instant.
- Un seul litige par réservation.
- Aucune pièce justificative n'est demandée.

## US-E11-02 — Accepter le litige

**En tant que** professeur
**je veux** reconnaître le litige en un clic
**afin de** clore la situation sans attendre un arbitrage.

**Priorité :** Doit · **Dépend de :** US-E11-01

**État :** Absente
Aucune action d'acceptation par le professeur : seul l'administrateur peut clore.

**Critères d'acceptation**

- L'action est proposée sur tout litige ouvert me concernant.
- Elle déclenche le remboursement intégral de l'élève et clôt le litige en sa faveur, **sans
  intervention administrative**.
- Le crédit étant gelé et jamais versé, le rembourser ne produit **aucun mouvement à
  rebours** : rien à récupérer, rien à annuler.
- La réservation passe annulée et l'élève est notifié.

## US-E11-03 — Arbitrer un litige

**En tant qu'** administrateur
**je veux** trancher un litige que le professeur n'a pas accepté
**afin de** débloquer l'argent dans un sens ou dans l'autre.

**Priorité :** Doit · **Dépend de :** US-E11-01

**État :** Partielle · **Code :** [`DisputeService`](../../RestaurantHandler/Services/DisputeService.cs)
`POST /Dispute/admin/close` : trois issues, plafond du remboursement, refus d'un litige
déjà résolu, notification des deux parties conformes. Écarts : note de résolution
facultative (valeur par défaut) ; calcul de l'issue partielle différent des
[règles métier](../20-specs-fonctionnelles/01-regles-metier.md#résolution-dun-litige).

**Critères d'acceptation**

- Trois issues : en faveur du professeur, en faveur de l'élève, ou **partielle** avec un
  montant remboursé inférieur au montant de la réservation.
- Le montant remboursé ne peut pas excéder le montant de la réservation.
- En faveur du professeur : le gel est levé, la réservation passe **versable** et le crédit
  part au cycle suivant ; seul le versement la fait passer terminée.
- En faveur de l'élève : remboursement, réservation annulée, crédit du professeur annulé.
- Une note de résolution est obligatoire et conservée.
- Les deux parties sont notifiées de l'issue et du motif.
- Un litige déjà résolu ne peut pas être rouvert par cette action.

## US-E11-04 — Débloquer un litige que personne ne traite

**En tant que** plateforme
**je veux** trancher par défaut en faveur du professeur après un délai
**afin qu'** un litige oublié n'immobilise pas son argent indéfiniment.

**Priorité :** Devrait · **À arbitrer** — voir
[questions ouvertes](../50-backlog/02-questions-ouvertes.md)

**État :** Absente
Voir [ADR-0011](../40-decisions/ADR-0011-resolution-auto-litige.md).

**Critères d'acceptation**

- Sans acceptation du professeur ni arbitrage, le litige est résolu **en faveur du
  professeur** au bout de 7 jours.
- L'administration est relancée **avant** l'échéance.
- La résolution automatique est identifiée comme telle dans l'historique, avec un auteur
  « système ».
- Les deux parties en sont informées.

## US-E11-05 — Consulter mes litiges

**En tant qu'** élève, professeur ou administrateur
**je veux** parcourir les litiges qui me concernent
**afin de** suivre leur traitement.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`DisputeService`](../../RestaurantHandler/Services/DisputeService.cs), [`DisputeController`](../../RestaurantHandler/Controllers/DisputeController.cs)
Listes paginées par rôle et détail. Le filtre par motif est impossible tant que le
motif est un texte libre.

**Critères d'acceptation**

- Liste paginée, filtrable par statut, motif et date, et triable.
- L'élève voit les siens, le professeur les siens, l'administrateur tous.
- Le détail affiche le motif, le statut, l'issue le cas échéant, la note de résolution, et
  un lien vers la réservation.
- L'élève et le professeur y accèdent en **lecture seule**, hormis l'action d'acceptation du
  professeur.

## US-E11-06 — Signaler les comportements répétés

**En tant qu'** administrateur
**je veux** voir l'historique de litiges d'un élève et d'un professeur
**afin de** repérer les abus.

**Priorité :** Pourrait

**État :** Absente

**Critères d'acceptation**

- Le dossier de litige affiche le nombre de litiges ouverts par cet élève et subis par ce
  professeur, avec leurs issues.
- Un seuil configurable déclenche un signalement dans le back-office.
- Aucune sanction n'est automatique : la suspension reste une décision humaine.
