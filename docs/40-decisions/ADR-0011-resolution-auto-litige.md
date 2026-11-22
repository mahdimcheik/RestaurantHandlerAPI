# ADR-0011 — Résolution automatique d'un litige en faveur du professeur à J+7

**Statut :** Accepté
**Application :** À faire
**Date :** 2026-09-14

## Contexte

Un litige gèle le crédit du professeur jusqu'à sa résolution. Trois voies le referment :
l'acceptation par le professeur, l'arbitrage administrateur, et — jusqu'ici — rien d'autre.

Si le professeur n'accepte pas et qu'aucun administrateur ne tranche, **le crédit reste gelé
indéfiniment**. Ce n'est pas un cas théorique : c'est le comportement par défaut d'un dossier
que personne n'ouvre, et il ne se signale de lui-même nulle part.

Trois sorties étaient possibles :

| Option | Conséquence |
|---|---|
| Gel indéfini | l'argent d'un professeur immobilisé à vie par un litige oublié |
| Par défaut pro élève | un élève obtiendrait un cours gratuit en se taisant |
| Par défaut pro professeur | cohérent avec le principe directeur du modèle de confiance |

## Décision

**Sans acceptation ni arbitrage, un litige est résolu en faveur du professeur au bout de sept
jours**, par une tâche horaire. L'administration est relancée au bout de **cinq** jours.

Trois conséquences en découlent, et elles sont voulues :

- la résolution automatique emprunte **exactement le même chemin** que l'arbitrage manuel —
  même répartition, même transition, même clôture ; seul l'auteur change, et il est vide ;
- la ligne d'historique produite porte donc un auteur « système », ce qui rend la décision
  automatique lisible comme telle dans le journal d'audit ;
- la relance est horodatée sur le litige, ce qui la rend idempotente : une tâche rejouée, ou
  deux instances concurrentes, ne relancent pas deux fois.

La réservation sort du litige à l'état **versable**, jamais terminée : c'est la tâche de
versement qui la terminera, une fois l'argent réellement parti. Poser terminée ici la sortirait
de sa sélection, et le professeur ne serait jamais payé — le gel aurait simplement changé de nom.

## Conséquences

**Ce qu'on gagne**

- L'immobilisation d'un crédit est **bornée** : sept jours, quoi qu'il arrive.
- Le principe directeur reste entier de bout en bout — *par défaut, le professeur est payé*,
  y compris quand la plateforme elle-même ne fait rien.
- L'élève qui se tait ne gagne toujours rien : ouvrir un litige puis l'abandonner ne produit
  aucun remboursement.

**Ce que ça coûte**

- **Un dossier légitime mal arbitré est perdu pour l'élève.** Sept jours sont courts pour une
  administration en congés, et l'élève lésé n'a aucun recours applicatif ensuite.
- La relance à J+5 devient une obligation d'exploitation, pas un confort : sans elle, la
  décision par défaut tomberait sans que personne ne l'ait vue venir.

**Ce qu'on surveille**

Le nombre de litiges résolus automatiquement. Un chiffre non nul est déjà un signal : il
signifie qu'un dossier est passé à travers la file d'arbitrage. S'il devient régulier, c'est la
capacité d'arbitrage qu'il faut augmenter, pas le délai.

La supervision fixe d'ailleurs son seuil d'alerte à **cinq** jours d'ouverture — c'est-à-dire au
moment de la relance, et non à l'échéance : une alerte qui arrive en même temps que la décision
n'est plus une alerte. Voir [tâches planifiées](../30-specs-techniques/06-jobs-planifies.md).

## État dans le code

Aucune tâche de résolution automatique ni de relance n'est enregistrée dans
[`Scheduler.cs`](../../RestaurantHandler/Utilities/Scheduler.cs), et `Dispute` ne porte pas d'horodatage de
relance ([`Dispute.cs`](../../RestaurantHandler/Domain/Entities/Reservation/Dispute.cs)). Un litige non arbitré gèle
aujourd'hui le crédit indéfiniment.
