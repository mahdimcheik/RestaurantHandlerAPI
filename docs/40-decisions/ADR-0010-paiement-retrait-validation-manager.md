# ADR-0010 — Paiement au retrait soumis à validation Manager

**Statut :** Accepté
**Date :** 2026-11-22

## Contexte

Le client peut payer en ligne **ou au retrait**. Une commande non payée préparée puis jamais
retirée est une perte sèche. Options étudiées : préparation immédiate avec suivi des
non-venues, seuil de montant, ou validation par le personnel.

## Décision

Une commande « payer au retrait » entre au statut **`AValider`** et ne part en cuisine qu'après
**acceptation d'un Manager**. Sans réponse dans un délai paramétrable, elle est refusée
automatiquement. En complément : une seule commande de ce type en cours par client, et blocage
du paiement au retrait après N non-venues (levable par un Manager).

## Conséquences

- Le statut de commande et le fait d'être payée deviennent **deux informations distinctes**
  (cf. machine à états).
- Charge pour le personnel pendant le service : la file `AValider` doit être très visible
  (son, badge) dans le back-office.
- Délai supplémentaire pour le client, affiché clairement.
