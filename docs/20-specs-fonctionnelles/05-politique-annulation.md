# Politique d'annulation

> Statut : **Stable**
>
> **Principe : le coût de l'annulation retombe sur celui qui annule.**
>
> Ce calcul vit à **un seul endroit** dans le code, partagé par l'exécution et par la
> prévisualisation affichée à l'élève : il est impossible de lui annoncer autre chose que ce
> qu'il obtiendra.

## Le barème

| Qui annule | Quand | Élève remboursé | Professeur | Plateforme |
|---|---|---|---|---|
| Élève | **≥ 48 h** avant le cours | **100 %** | crédit annulé, 0 € | **− frais du prestataire** |
| Élève | **< 48 h** avant le cours | **95 %** — montant × (1 − commission) | crédit annulé, 0 € | **+ 5 % − frais** |
| Élève | cours commencé | **refusé** — voie du litige | — | — |
| Professeur | à tout moment avant la fin du créneau | **100 %** | crédit annulé **+ pénalité de 5 %** | **+ 5 % − frais** |

## Pourquoi ce barème

**Annulation anticipée de l'élève : la plateforme absorbe.** Stripe ne restitue pas ses
frais sur un remboursement. Les rendre à l'élève coûte donc quelques euros à la plateforme —
c'est le prix du service et de la confiance. Facturer des frais sur une annulation de bonne
foi ferait fuir les élèves.

**Annulation tardive de l'élève : la commission est retenue.** À moins de 48 heures, le
créneau est difficilement recommercialisable. La plateforme garde sa commission, qui couvre
largement ses frais. Le professeur, lui, ne touche rien : il n'a pas donné le cours.

**Après le début du cours : pas d'annulation.** Le cours a commencé — s'il s'est mal passé,
c'est un litige, avec arbitrage. Permettre l'annulation ici rendrait à l'élève le droit de
veto que tout le modèle cherche à supprimer. Voir
[E10](../10-user-stories/E10-preuve-de-realisation.md).

**Annulation du professeur : aucun seuil de délai.** Un professeur qui annule reste
responsable, qu'il prévienne trois semaines ou trois heures avant. L'élève est intégralement
remboursé et le professeur porte le coût, y compris les frais du prestataire, couverts par
la pénalité forfaitaire de 5 %.

**Les frais du prestataire n'entrent pas dans le calcul.** Ils sont renseignés de façon
différée et seraient inconnus au moment du clic. Une règle qui en dépendrait serait
incalculable et la prévisualisation deviendrait mensongère.

## Exemples chiffrés

Réservation de **120,00 €**, commission 5 %, crédit du professeur **114,00 €**.

### L'élève annule 5 jours avant

| | Montant |
|---|---|
| Remboursé à l'élève | 120,00 € |
| Retenu | 0,00 € |
| Crédit du professeur annulé | −114,00 € |
| Pénalité du professeur | 0,00 € |
| Résultat plateforme | 0,00 € **moins les frais du prestataire** |

### L'élève annule 12 heures avant

| | Montant |
|---|---|
| Remboursé à l'élève | 114,00 € |
| Retenu | 6,00 € |
| Crédit du professeur annulé | −114,00 € |
| Pénalité du professeur | 0,00 € |
| Résultat plateforme | +6,00 € moins les frais |

### Le professeur annule, quel que soit le délai

| | Montant |
|---|---|
| Remboursé à l'élève | 120,00 € |
| Retenu | 0,00 € |
| Crédit du professeur annulé | −114,00 € |
| Pénalité du professeur | −6,00 € *(dette)* |
| Résultat plateforme | +6,00 € moins les frais |

Si le professeur a un solde disponible, la pénalité est déduite de son prochain versement.
S'il n'en a pas, **son solde devient négatif** et la dette est recouvrée par compensation.
Voir [E08-05](../10-user-stories/E08-wallet-versements.md).

## Le calcul

```
part professeur  = arrondi( montant × (1 − taux de commission figé) )

annulation élève :
    tardive ?     = début du créneau ≤ maintenant + 48 h
    remboursé     = tardive ? part professeur : montant
    retenu        = montant − remboursé
    annulation crédit = part professeur
    pénalité      = 0

annulation professeur :
    remboursé     = montant
    retenu        = 0
    annulation crédit = part professeur
    pénalité      = arrondi( montant × 5 % )
```

Tous les arrondis se font au centime, `MidpointRounding.AwayFromZero`. **L'annulation du
crédit utilise exactement la même formule que le crédit initial**, sinon un résidu traîne
au grand livre.

Les trois montants renvoyés sont **positifs** : c'est l'appelant qui pose le signe. Au
grand livre, l'annulation du crédit et la pénalité sont écrites en négatif.

## Prévisualisation

Avant confirmation, l'élève voit :

| Donnée | Rôle |
|---|---|
| Montant remboursé | ce qu'il récupère |
| Montant retenu | ce qu'il perd |
| Montant de la réservation | le rappel de l'assiette |
| Caractère tardif | pourquoi c'est ce montant |
| Heure limite | jusqu'à quand il aurait eu 100 % |
| Annulation possible | faux si le cours a commencé |

Le professeur voit l'équivalent avant son annulation : la pénalité, son solde actuel et son
solde après — **y compris lorsque celui-ci devient négatif**.

## Exécution

Le remboursement n'est **jamais** émis en ligne : l'annulation enregistre une ligne de
remboursement en attente et rend la main. Une tâche récurrente l'exécute, avec une clé
d'idempotence dérivée de la réservation. Un échec est réessayé au cycle suivant, sans que
l'utilisateur ait à recommencer.

## État dans le code

| Élément | Code | État |
|---|---|---|
| Barème et calcul | [`CancellationPolicy`](../../RestaurantHandler/Domain/Policies/CancellationPolicy.cs) (`ForStudent`, `ForTeacher`, `IsLate`) | Conforme, calcul isolé et sans dépendance. |
| Exécution | [`ReservationCancellationService`](../../RestaurantHandler/Services/Reservations/ReservationCancellationService.cs) | Conforme. Le refus « cours commencé » répond `409` depuis le lot 3. |
| Prévisualisation élève | `GET /Slots/student/reservation/{id}/cancellation-preview` ([`SlotsController`](../../RestaurantHandler/Controllers/SlotsController.cs)) | Conforme. |
| Prévisualisation professeur (pénalité, solde avant et après) | — | **Absente.** |
| Remboursement différé | `JobChron.ProcessPendingRefunds` ([`JobChron`](../../RestaurantHandler/Services/JobChron.cs)) | Conforme. Le délai appliqué est `DISPUTE_WINDOW` (2 jours + 2 h), pas exactement 48 h. |
