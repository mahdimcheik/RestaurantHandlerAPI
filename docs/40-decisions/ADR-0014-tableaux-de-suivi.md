# ADR-0014 — Tableaux de suivi : rattachement, ordre des cartes et texte enrichi

**Statut :** Accepté
**Application :** Appliquée (lot 9, 2026-10-01)
**Date :** 2026-10-01

## Contexte

Les professeurs veulent suivre chaque élève entre les séances autrement que par la
conversation d'une réservation, qui se perd dans le fil des messages. Le besoin exprimé est
celui d'un tableau à la Trello ou Nextcloud Deck : des listes ordonnées, des cartes qu'on
déplace, une description mise en forme ([E20](../10-user-stories/E20-suivi-de-cours.md)).

Quatre questions structurent le modèle :

1. **À quel élève** un professeur peut-il ouvrir un tableau ? Le modèle ne connaît aucune
   relation professeur-élève explicite ; la seule qui existe, et qui protège déjà la lecture
   d'un profil d'élève, est la **réservation partagée**
   ([`ReservationAccess`](../../RestaurantHandler/Utilities/ReservationAccess.cs)).
2. **Comment représenter l'ordre** des listes et des cartes, sachant qu'il change à chaque
   glisser-déposer ?
3. **Comment accepter du HTML** venu d'un éditeur riche, alors que le dépôt n'a encore aucune
   bibliothèque de nettoyage ([D-22](../50-backlog/03-dette-technique.md)) ?
4. **Que garder** quand une carte ou un tableau n'est plus utile ?

## Décision

**Rattachement par réservation partagée, vérifié à la création.** Un tableau n'est créé que
pour un élève qui a au moins une réservation sur un créneau du professeur
([I15](../20-specs-fonctionnelles/01-regles-metier.md#i15--un-tableau-de-suivi-relie-un-professeur-et-un-élève-qui-partagent-une-réservation)).
La relation n'est pas revérifiée ensuite. Un produit attaché à une carte appartient au
professeur ; une réservation attachée relie ce professeur à cet élève. L'élève lit, seul le
professeur écrit ; l'administration n'y a pas accès.

**Positions entières contiguës, réécrites par le serveur.** Chaque carte porte sa position dans
sa liste, chaque liste sa position dans son tableau, à partir de 0 et sans trou. Le client
n'envoie jamais un ordre complet de cartes : il envoie une **intention** —
`move { listId, position }` — et le serveur décale les voisines dans une seule transaction,
sous verrou sur le tableau. Le réordonnancement des listes, lui, reçoit l'ordre complet : il
y en a au plus `LIST_MAX_PER_BOARD`.

**Nettoyage du HTML à l'écriture, par liste blanche.** La description d'une carte passe par
une bibliothèque de nettoyage dédiée (`HtmlSanitizer`) avant d'être stockée. Sont admis :
paragraphes, sauts de ligne, gras, italique, souligné, barré, titres `h1` à `h3`, listes
ordonnées ou non, citations, blocs de code, liens `http`/`https`/`mailto` (forcés en
`rel="noopener noreferrer"` et `target="_blank"`), et l'attribut `class` limité aux valeurs
`ql-*` par lesquelles Quill encode indentation, alignement et blocs de code. Tout le reste — scripts, gestionnaires
d'événements, styles en ligne, images, iframes — est retiré, pas échappé. Le plafond
`CARD_DESCRIPTION_MAX_LENGTH` s'applique au texte, sans les balises. Le client nettoie
**aussi** à l'affichage : les deux ne se remplacent pas.

**Archivage plutôt que suppression** pour les tableaux et les cartes. Une liste, simple
contenant, se supprime physiquement mais seulement **vide**.

## Conséquences

**Ce qu'on gagne**

- Aucune nouvelle notion de « relation » à maintenir : le suivi s'appuie sur la même règle
  que la lecture d'un profil d'élève, déjà testée.
- Un ordre toujours lisible et vérifiable (`position` = rang), et des déplacements
  concurrents qui ne produisent jamais deux cartes au même rang.
- Une première bibliothèque de nettoyage dans le dépôt, réutilisable pour fermer D-22 sur les
  autres champs.
- Une carte archivée par erreur se restaure.

**Ce que ça coûte**

- Un déplacement réécrit jusqu'à toutes les positions de deux listes. C'est négligeable aux
  volumes d'un tableau de suivi ; ce ne le serait pas pour des milliers de cartes, où un rang
  fractionnaire ou lexicographique (*LexoRank*) s'imposerait.
- Un professeur ne peut pas préparer un tableau pour un élève avant sa première réservation.
- Une mise en forme collée depuis un traitement de texte (couleurs, polices) est perdue.

**Ce qu'on surveille**

- Le nombre moyen de cartes par liste : au-delà de quelques centaines, revoir la
  représentation de l'ordre.
- Les demandes d'images dans les descriptions : les admettre exigerait de passer par le
  stockage objet ([ADR-0008](ADR-0008-stockage-objet-unifie.md)), jamais par des données en
  ligne.

## État dans le code

Appliqué au lot 9 (branche `feat/lot-9-suivi`) :

- entités `Board`, `BoardList`, `BoardCard`, migration `Lot9SuiviDeCours` ;
- [`BoardService`](../../RestaurantHandler/Services/BoardService.cs) derrière `BoardsController` et
  `MyStudentsController`, rattachement par
  [`ReservationAccess.SharesReservationAsync`](../../RestaurantHandler/Utilities/ReservationAccess.cs) ;
- verrou du tableau par `SELECT … FOR UPDATE` sur sa ligne, dans la transaction de chaque
  écriture de positions (ajout et ordre des listes, création, déplacement, archivage et
  restauration de carte) ;
- [`RichTextSanitizer`](../../RestaurantHandler/Utilities/RichTextSanitizer.cs) sur `HtmlSanitizer`
  (Ganss.Xss), calé sur le HTML sémantique de Quill 2 ; une ancre à schéma refusé est
  remplacée par son texte ;
- supprimer une liste vide de cartes actives fait passer ses cartes archivées dans la
  première liste restante, où leur restauration les place.
