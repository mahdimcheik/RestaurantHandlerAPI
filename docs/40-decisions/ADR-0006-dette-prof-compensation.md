# ADR-0006 — Dette du professeur recouvrée par compensation

**Statut :** Accepté
**Application :** Appliquée
**Date :** 2026-09-12

## Contexte

Un professeur qui annule un cours payé doit une pénalité de 5 %. Mais **son solde est le plus
souvent vide** : professeur nouveau, ou cours déjà tous versés. Il faut donc pouvoir porter
une créance et la recouvrer plus tard.

Trois options :

1. **Prélever** sur son compte bancaire — impose un mandat de prélèvement, une relation
   contractuelle et un parcours supplémentaires ; disproportionné pour quelques euros.
2. **Renvoyer le transfert** déjà effectué — techniquement possible, mais ne s'applique qu'aux
   sommes déjà versées, et ne résout donc pas le cas de la pénalité sur un solde vide.
3. **Compenser** sur les versements futurs.

## Décision

La dette est une **écriture négative d'un type de dette** au grand livre — pénalité ou
récupération — déduite des versements suivants, **du plus ancien au plus récent**.

Deux règles en découlent :

**C'est le type, pas le signe, qui fait la dette.** Une écriture de remboursement est
négative, mais elle est toujours adossée au crédit de la même réservation : les deux
s'annulent, rien n'est parti, rien n'est dû. La recouvrer ferait payer le remboursement deux
fois au professeur. Seules les écritures de pénalité et de récupération n'ont aucun crédit
vivant en face.

**On scinde, on ne mute pas.** Une dette supérieure au versement disponible voit sa ligne
d'origine **archivée** et remplacée par deux lignes qui somment à l'identique : la part
recouvrée, rattachée au versement, et le reste dû, encore ouvert. La somme des lignes vivantes
reste la dette infligée — comptabilité d'engagement — et la somme des lignes non réglées reste
ce qui est encore dû.

## Conséquences

**Ce qu'on gagne**

- Aucun mandat de prélèvement, aucun parcours supplémentaire.
- Un versement entièrement absorbé vaut 0 € et **ne déclenche aucun appel externe** : c'est un
  règlement par compensation.
- Le grand livre reste exact dans les deux lectures : ce qui a été infligé, et ce qui reste dû.
- Un renvoi de transfert n'est jamais nécessaire.

**Ce que ça coûte**

- **Toute somme d'écritures doit exclure les lignes archivées.** C'est le piège principal de
  cette décision : l'oublier double la dette. Un test dédié le verrouille.
- Le montant d'un versement dépend désormais de la dette, alors que sa clé d'idempotence reste
  stable. Il faut donc **figer le versement en base avant l'appel** et le rejouer tel quel.
  Voir [ADR-0004](ADR-0004-idempotence-stripe.md).
- **Un professeur qui accumule une dette puis cesse d'enseigner ne la remboursera jamais.**
  C'est une fuite reconnue : aucun prélèvement, aucun renvoi de transfert.

**Ce qu'on surveille**

La dette non recouvrée, par ancienneté. La mesure d'atténuation proposée — un plafond au-delà
duquel les créneaux ne sont plus réservables — reste à arbitrer. Voir
[questions ouvertes](../50-backlog/02-questions-ouvertes.md).

## État dans le code

`HardCode.DEBT_TRANSACTION_TYPES` ([`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs)) ;
compensation FIFO et scission avec `ArchivedAt` dans `JobChron.PayoutTeacherBalances`.
