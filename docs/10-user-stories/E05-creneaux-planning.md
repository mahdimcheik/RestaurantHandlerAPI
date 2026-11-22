# E05 — Créneaux & planning

> Statut : **Stable** · Conventions : [README](README.md)

## US-E05-01 — Créer un créneau

**En tant que** professeur
**je veux** ouvrir une plage horaire dans mon agenda
**afin qu'** un élève puisse la réserver.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs)
Fin après début, créneau futur, chevauchement refusés. Le message de chevauchement
ne désigne pas le créneau en conflit ; plusieurs messages contiennent des caractères
corrompus (`cr�neau`).

**Critères d'acceptation**

- Champs : date et heure de début, date et heure de fin, type (présentiel, visio,
  indifférent).
- La fin est postérieure au début.
- Un créneau **dans le passé** est refusé.
- Un créneau qui **chevauche** un autre créneau du même professeur est refusé, avec un
  message désignant le créneau en conflit.
- La création se fait au clic sur une plage libre de l'agenda.

## US-E05-02 — Modifier un créneau

**En tant que** professeur
**je veux** déplacer ou redimensionner un créneau
**afin d'** ajuster mes disponibilités.

**Priorité :** Doit · **Dépend de :** US-E05-01

**État :** Partielle · **Code :** [`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs)
**Écart de sécurité :** le créneau modifié est recherché par identifiant seul — ni
propriété ni réservation vivante ne sont vérifiées. Un professeur peut modifier le créneau
d'un autre, y compris un créneau réservé.

**Critères d'acceptation**

- Le déplacement et le redimensionnement se font directement dans l'agenda.
- Les mêmes règles de chevauchement et d'antériorité s'appliquent.
- Un créneau portant une **réservation vivante** n'est pas modifiable : il faut d'abord
  traiter la réservation.

## US-E05-03 — Supprimer un créneau

**En tant que** professeur
**je veux** retirer un créneau
**afin de** fermer une disponibilité.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs)
Propriété, réservation vivante (en attente ou acceptée) et créneau passé contrôlés ;
suppression logique.

**Critères d'acceptation**

- Une confirmation est demandée.
- Un créneau portant une réservation vivante n'est pas supprimable ; le message explique
  qu'il faut annuler la réservation, avec ses conséquences financières.
- La suppression est logique.

## US-E05-04 — Consulter mon agenda

**En tant que** professeur
**je veux** voir mes créneaux et leurs réservations dans un agenda
**afin de** piloter mon activité.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs)
`POST /Slots/teacher/my-slots`, chargé par période. Présentation et avertissement
relèvent du front.

**Critères d'acceptation**

- Vue semaine sur grand écran, vue jour sur mobile.
- Les créneaux sont distingués visuellement : libre, réservé en attente, confirmé, passé.
- Le clic sur un créneau réservé ouvre le détail de la réservation et ses actions.
- Le chargement ne porte que sur la plage affichée.
- Si je ne suis ni vérifié ni confirmé côté paiement, un avertissement explique que mes
  réservations passeront par le **flux sans paiement**.

## US-E05-05 — Consulter les créneaux disponibles d'un professeur

**En tant qu'** élève, même non connecté
**je veux** voir les créneaux libres d'un professeur sur une période
**afin de** choisir quand réserver.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`SlotsService`](../../RestaurantHandler/Services/SlotsService.cs), [`SlotsController`](../../RestaurantHandler/Controllers/SlotsController.cs)
`POST /Slots/teacher/{teacherId}/available-slots`. `POST /Slots/student` est un second
point d'accès anonyme au nom trompeur (`GetReservationsByStudent`).

**Critères d'acceptation**

- Seuls les créneaux **réservables** sont visibles ; les créneaux occupés n'apparaissent pas.
- La période est navigable.
- Un créneau bloqué par la commande en attente d'un autre élève n'est pas proposé.
- Aucune information personnelle sur les autres élèves n'est exposée.

## US-E05-06 — Purger les créneaux passés

**En tant que** plateforme
**je veux** supprimer automatiquement les créneaux passés inutiles
**afin de** garder l'agenda et les requêtes de recherche performants.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`Scheduler`](../../RestaurantHandler/Utilities/Scheduler.cs)
`SlotCleaningJob` supprime **physiquement** (pas logiquement) les créneaux passés
sans réservation ou avec une réservation en attente, en comparant le statut par son nom et
en heure locale.

**Critères d'acceptation**

- Une tâche quotidienne supprime les créneaux passés **sans réservation**, ou dont les
  seules réservations sont restées en attente.
- Un créneau portant une réservation confirmée, livrée, versable, en litige ou terminée
  n'est **jamais** supprimé : il porte une trace comptable.

## US-E05-07 — Créer des créneaux récurrents

**En tant que** professeur
**je veux** répéter un créneau chaque semaine sur les jours de mon choix jusqu'à une date donnée
**afin de** ne pas ressaisir mes disponibilités habituelles une par une.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`SlotsService.AddSlotsBatchByTeacherAsync`](../../RestaurantHandler/Services/SlotsService.cs)
Les occurrences sont calculées par le client en heure locale (le changement d'heure ne décale
pas les créneaux) et envoyées à `POST /slots/teacher/add-batch`. Les créneaux créés sont
indépendants : ils se modifient et se suppriment un par un.

**Critères d'acceptation**

- Dans la modale de création, « Répéter chaque semaine » propose les jours de la semaine
  (le jour cliqué est pré-coché) et une date de fin, à un mois par défaut et six mois au plus.
- Une occurrence passée, invalide ou qui chevauche un créneau existant (ou une autre occurrence
  du lot) est ignorée et signalée ; les autres sont créées.
- Un lot vide, de plus de 200 créneaux ou qui s'étend sur plus de six mois est refusé (400).

## US-E05-08 — Dupliquer un jour ou une semaine de créneaux

**En tant que** professeur
**je veux** copier les créneaux d'un jour ou d'une semaine et les coller sur un autre jour ou une autre semaine
**afin de** reproduire rapidement un planning type.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`SlotsService.AddSlotsBatchByTeacherAsync`](../../RestaurantHandler/Services/SlotsService.cs)
Copier et coller se font dans le calendrier du front ; le collage réutilise
`POST /slots/teacher/add-batch`, où chaque créneau peut porter son propre type.

**Critères d'acceptation**

- Chaque en-tête de jour (vues jour et semaine) porte « Copier » et « Coller » : un jour se copie
  et se colle sur un autre jour, y compris dans la même semaine.
- En vue semaine, la barre d'outils propose « Copier la semaine » et « Coller la semaine ».
- Seuls les créneaux sont copiés, avec leurs heures et leur type, sans les réservations ; ils sont
  recréés aux mêmes heures locales.
- Les créneaux passés ou en chevauchement sont ignorés et signalés ; les autres sont créés.
