# E12 — Avis & réputation

> Statut : **Stable**
>
> Un avis n'a de valeur que s'il est infalsifiable : il est **adossé à une réservation
> réellement passée**. Voir
> [invariant I12](../20-specs-fonctionnelles/01-regles-metier.md#i12--un-avis-est-adossé-à-une-réservation-réellement-passée).

## US-E12-01 — Noter un cours

**En tant qu'** élève
**je veux** noter et commenter un cours que j'ai suivi
**afin d'** aider les autres élèves à choisir.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ReviewService`](../../RestaurantHandler/Services/ReviewService.cs)
`POST /Reviews/add` : propriété, note de 1 à 5, unicité en base et recalcul conformes.
Seule une réservation terminée (`DONE`) est notable (lot 4). **Écart :** plafond du
commentaire à vérifier.

**Critères d'acceptation**

- L'avis n'est possible que sur une réservation **terminée** qui m'appartient.
- Une note de 1 à 5 est obligatoire ; le commentaire est facultatif et plafonné en longueur.
- **Un seul avis par réservation**, garanti par une contrainte d'unicité en base, pas
  seulement par un contrôle applicatif.
- Un avis sur une réservation annulée, refusée ou encore à venir est refusé.
- Le dépôt met à jour la note moyenne et le nombre d'avis du professeur.

## US-E12-02 — Consulter les avis d'un professeur

**En tant que** visiteur
**je veux** lire les avis reçus par un professeur
**afin de** me faire une opinion.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`ReviewService`](../../RestaurantHandler/Services/ReviewService.cs)
`POST /Reviews/teacher/{teacherId}`, anonyme, paginé ; filtre global sur les avis
archivés.

**Critères d'acceptation**

- La fiche publique affiche la note moyenne, le nombre d'avis et la liste paginée.
- Chaque avis affiche le prénom de l'auteur, sa note, son commentaire et sa date.
- Les avis archivés par la modération n'apparaissent pas.
- Le bloc est rendu côté serveur et alimente les données structurées de la page. Voir
  [E19](E19-transverse.md).

## US-E12-03 — Retrouver mon avis

**En tant qu'** élève
**je veux** voir l'avis que j'ai déposé sur une réservation
**afin de** vérifier ce que j'ai écrit.

**Priorité :** Pourrait

**État :** Implémentée · **Code :** [`ReviewService`](../../RestaurantHandler/Services/ReviewService.cs)
`GET /Reviews/reservation/{reservationId}/mine`.

**Critères d'acceptation**

- Le détail de la réservation affiche mon avis s'il existe, sinon propose de le déposer.
- **La modification n'est pas prévue en v1** : un avis déposé est définitif, sauf
  intervention de la modération.

## US-E12-04 — Modérer un avis

**En tant qu'** administrateur
**je veux** masquer ou supprimer un avis inapproprié
**afin de** protéger les professeurs des abus.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ReviewService`](../../RestaurantHandler/Services/ReviewService.cs)
Archivage et suppression réservés aux administrateurs, avec recalcul. Écarts :
suppression non journalisée ; notification du professeur à vérifier.

**Critères d'acceptation**

- L'archivage masque l'avis de la fiche publique sans détruire la donnée, et **recalcule** la
  note moyenne.
- La suppression définitive est réservée aux cas graves et journalisée.
- Le professeur est notifié du retrait d'un avis le concernant.
- Un professeur ne peut ni supprimer ni masquer un avis lui-même.

## US-E12-05 — Tenir la note moyenne à jour

**En tant que** plateforme
**je veux** que la note affichée soit toujours cohérente avec les avis visibles
**afin de** ne pas induire les élèves en erreur.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ReviewService`](../../RestaurantHandler/Services/ReviewService.cs)
`Teacher.AverageRating` et `ReviewsCount` recalculés après chaque mutation, avis
archivés exclus. Écart : un professeur sans avis porte `AverageRating = 0`.

**Critères d'acceptation**

- La note moyenne et le nombre d'avis sont **stockés sur le professeur** pour permettre le tri
  et le filtrage sans jointure coûteuse.
- Ils sont recalculés à chaque dépôt, archivage ou suppression, dans la même transaction.
- Le calcul ne prend en compte que les avis non archivés.
- Un professeur sans avis est affiché comme tel, jamais avec une note de zéro.
