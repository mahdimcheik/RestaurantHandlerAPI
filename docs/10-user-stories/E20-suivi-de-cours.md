# E20 — Suivi de cours

> Statut : **Brouillon**
>
> Le professeur suit la progression de chacun de ses élèves sur des **tableaux** à la manière
> de Trello ou de Nextcloud Deck : des **listes** ordonnées (par défaut *À faire*, *En cours*,
> *Fait*) qui contiennent des **cartes** — un objectif, un exercice, un point à revoir.
> Un tableau est **adossé à une relation réelle** : le professeur et l'élève partagent au
> moins une réservation (invariant [I15](../20-specs-fonctionnelles/01-regles-metier.md)).
> Le professeur écrit, l'élève lit. Décision structurante :
> [ADR-0014](../40-decisions/ADR-0014-tableaux-de-suivi.md).

## US-E20-01 — Lister mes élèves

**En tant que** professeur
**je veux** voir la liste des élèves avec qui j'ai au moins une réservation
**afin de** choisir pour qui ouvrir un tableau de suivi.

**Priorité :** Doit · **Voir aussi :** US-E06-09

**État :** Implémentée
Aucun point d'accès « mes élèves » : un professeur ne voit ses élèves qu'au travers de ses
réservations.

**Critères d'acceptation**

- Étant donné un professeur connecté, quand il consulte ses élèves, alors il voit chaque élève
  ayant au moins une réservation sur l'un de ses créneaux, **une seule fois**, quel que soit le
  nombre de réservations.
- La liste est paginée, triable et filtrable par nom côté serveur.
- Chaque élève affiche le nombre de tableaux actifs que le professeur lui a ouverts.
- Un élève qui n'a jamais réservé auprès de ce professeur n'apparaît **pas**.
- Un élève ou un visiteur anonyme qui appelle ce point d'accès est refusé.

## US-E20-02 — Créer un tableau pour un élève

**En tant que** professeur
**je veux** créer un tableau de suivi pour l'un de mes élèves
**afin d'** organiser ce qu'il doit travailler.

**Priorité :** Doit · **Dépend de :** US-E20-01

**État :** Implémentée

**Critères d'acceptation**

- Étant donné un élève avec qui le professeur partage une réservation, quand le professeur
  crée un tableau en lui donnant un titre, alors le tableau est créé avec **trois listes** dans
  cet ordre : *À faire*, *En cours*, *Fait*.
- Un professeur peut ouvrir **plusieurs** tableaux pour le même élève (une matière, un projet,
  une période…).
- Le titre est obligatoire et plafonné à `BOARD_TITLE_MAX_LENGTH`.
- Étant donné un élève **sans** réservation partagée, quand le professeur tente de créer un
  tableau pour lui, alors la création est refusée et rien n'est créé.
- Étant donné un élève inexistant ou banni, alors la création est refusée.
- Un élève ne peut pas créer de tableau.

## US-E20-03 — Lister mes tableaux

**En tant que** professeur
**je veux** voir tous mes tableaux, regroupés par élève
**afin de** passer rapidement de l'un à l'autre.

**Priorité :** Doit · **Dépend de :** US-E20-02

**État :** Implémentée

**Critères d'acceptation**

- La liste ne contient que les tableaux **du professeur connecté**, non archivés par défaut.
- Elle se filtre par élève et se trie par date de dernière modification.
- Chaque tableau indique son élève, son titre et le nombre de cartes actives.
- Les tableaux archivés restent consultables via un filtre explicite.
- Un tableau d'un autre professeur n'apparaît jamais, et y accéder directement répond
  « introuvable ».

## US-E20-04 — Renommer ou archiver un tableau

**En tant que** professeur
**je veux** renommer un tableau ou l'archiver quand le suivi est terminé
**afin de** garder une liste de tableaux à jour.

**Priorité :** Doit · **Dépend de :** US-E20-02

**État :** Implémentée

**Critères d'acceptation**

- Le professeur propriétaire peut modifier le titre, dans les mêmes limites qu'à la création.
- Archiver un tableau le retire des listes par défaut **sans détruire** ses listes ni ses
  cartes ; il peut être désarchivé.
- Un tableau archivé n'accepte plus aucune modification de ses listes ou de ses cartes tant
  qu'il n'est pas désarchivé.
- Étant donné un tableau d'un autre professeur, quand on tente de le modifier ou de
  l'archiver, alors la réponse est « introuvable » et rien ne change.

## US-E20-05 — Gérer les listes d'un tableau

**En tant que** professeur
**je veux** ajouter, renommer, réordonner et supprimer des listes
**afin d'** adapter le tableau à ma façon de suivre l'élève.

**Priorité :** Doit · **Dépend de :** US-E20-02

**État :** Implémentée

**Critères d'acceptation**

- Une nouvelle liste s'ajoute **en dernière position** ; son titre est obligatoire.
- Les trois listes par défaut se renomment, se déplacent et se suppriment comme les autres :
  elles n'ont aucun statut particulier.
- Le réordonnancement est appliqué en une fois : après l'opération, les positions des listes
  du tableau sont contiguës et sans doublon.
- Un tableau ne peut pas dépasser `LIST_MAX_PER_BOARD` listes ni descendre sous **une** liste.
- Étant donné une liste qui contient des cartes non archivées, quand le professeur la
  supprime, alors la suppression est **refusée** (conflit) ; il doit d'abord déplacer ou
  archiver ses cartes.
- Une liste ne peut être manipulée qu'à travers son tableau : une liste d'un autre tableau
  répond « introuvable ».

## US-E20-06 — Créer une carte

**En tant que** professeur
**je veux** ajouter une carte dans une liste, avec un titre, une importance, un cours et une
séance optionnels et une description mise en forme
**afin de** décrire précisément ce que l'élève doit travailler.

**Priorité :** Doit · **Dépend de :** US-E20-05

**État :** Implémentée

**Critères d'acceptation**

- Une carte porte :
  - un **titre**, obligatoire et plafonné à `CARD_TITLE_MAX_LENGTH` ;
  - une **importance** parmi *Basse*, *Normale*, *Haute* et *Urgente*. Par défaut : *Normale* ;
  - un **produit** (cours) optionnel ;
  - une **réservation** (séance) optionnelle ;
  - une **description** en texte enrichi, optionnelle, plafonnée à
    `CARD_DESCRIPTION_MAX_LENGTH` caractères saisis.
- La carte s'ajoute en dernière position de la liste choisie.
- La description est **nettoyée à l'écriture** : seule la mise en forme autorisée (gras,
  italique, listes, titres, liens, code) est conservée ; tout script, gestionnaire
  d'événement ou style inline est retiré.
- Étant donné un produit qui n'appartient pas au professeur, quand il l'attache à la carte,
  alors la création est refusée.
- Étant donné une réservation qui ne relie pas **ce professeur à l'élève du tableau**, quand
  elle est attachée, alors la création est refusée.
- Étant donné un tableau archivé, alors la création est refusée.

## US-E20-07 — Modifier une carte

**En tant que** professeur
**je veux** modifier n'importe quel champ d'une carte
**afin de** la tenir à jour au fil des séances.

**Priorité :** Doit · **Dépend de :** US-E20-06

**État :** Implémentée

**Critères d'acceptation**

- Tous les champs de la création sont modifiables, avec les **mêmes contrôles** (propriété du
  produit, réservation du couple, plafonds, nettoyage).
- Retirer le produit ou la réservation est permis : les deux restent optionnels.
- La date de dernière modification de la carte est mise à jour par le serveur.
- Une carte d'un tableau d'un autre professeur répond « introuvable ».

## US-E20-08 — Déplacer une carte

**En tant que** professeur
**je veux** déplacer une carte à l'intérieur d'une liste ou vers une autre liste
**afin de** refléter l'avancement de l'élève.

**Priorité :** Doit · **Dépend de :** US-E20-06

**État :** Implémentée

**Critères d'acceptation**

- Étant donné une carte et une position cible dans une liste **du même tableau**, quand le
  professeur la déplace, alors la carte occupe cette position et les autres cartes des listes
  source et cible sont décalées, **sans trou ni doublon**.
- Le déplacement est atomique : en cas d'échec, aucune position n'a changé.
- Une position au-delà de la fin de la liste place la carte en dernier.
- Étant donné une liste cible d'un **autre** tableau, alors le déplacement est refusé.
- Deux déplacements concurrents sur le même tableau aboutissent à un ordre cohérent, jamais à
  deux cartes à la même position.

## US-E20-09 — Archiver une carte

**En tant que** professeur
**je veux** archiver une carte devenue inutile
**afin d'** alléger le tableau sans perdre l'historique.

**Priorité :** Doit · **Dépend de :** US-E20-06

**État :** Implémentée

**Critères d'acceptation**

- Une carte archivée disparaît du tableau et les positions de sa liste sont recompactées.
- Elle reste consultable via un filtre « archivées » et peut être restaurée, en dernière
  position de sa liste d'origine — ou de la première liste si celle-ci n'existe plus.
- Seul le professeur propriétaire du tableau peut archiver ou restaurer une carte.

## US-E20-10 — Filtrer les cartes d'un tableau

**En tant que** professeur ou élève
**je veux** filtrer les cartes d'un tableau par importance ou par cours
**afin de** me concentrer sur ce qui compte.

**Priorité :** Pourrait · **Dépend de :** US-E20-06

**État :** Implémentée

**Critères d'acceptation**

- Les filtres « importance » (une ou plusieurs valeurs) et « produit » se combinent.
- Le filtrage n'altère **pas** l'ordre ni les positions enregistrées des cartes.
- Un filtre sans résultat affiche des listes vides, pas une erreur.

## US-E20-11 — Consulter mes tableaux en tant qu'élève

**En tant qu'** élève
**je veux** consulter les tableaux que mes professeurs ont ouverts pour moi
**afin de** savoir ce que je dois travailler.

**Priorité :** Devrait · **Dépend de :** US-E20-02

**État :** Implémentée

**Critères d'acceptation**

- L'élève voit **uniquement** les tableaux dont il est l'élève, non archivés, regroupés par
  professeur.
- Il voit les listes, les cartes et leur description dans le même ordre que le professeur,
  ainsi que les pièces jointes des cartes, qu'il peut ouvrir et télécharger (US-E20-12).
- Toute tentative d'écriture de sa part (tableau, liste, carte, déplacement) est refusée.
- Un tableau ouvert pour un autre élève répond « introuvable ».
- Un tableau archivé par le professeur disparaît de sa vue.

## US-E20-12 — Joindre des fichiers à une carte

**En tant que** professeur
**je veux** joindre des images et des PDF à une carte
**afin de** donner à l'élève un énoncé, une fiche ou une copie corrigée sans passer par la
messagerie.

**Priorité :** Devrait · **Dépend de :** US-E20-06 · **Décision :**
[ADR-0015](../40-decisions/ADR-0015-pieces-jointes-cartes.md)

**État :** Implémentée

**Critères d'acceptation**

- Le professeur propriétaire joint **un fichier par envoi** à une carte active d'un tableau
  non archivé. Le fichier est accepté s'il est un **JPEG, PNG, WebP ou PDF**, reconnu par sa
  signature et non par son seul nom ; sinon l'envoi est refusé.
- Un fichier vide est refusé ; un fichier de plus de `ATTACHMENT_MAX_SIZE_BYTES`
  (10 Mo) est refusé.
- Une carte porte au plus `CARD_ATTACHMENT_MAX_PER_CARD` (10) pièces jointes ; au-delà,
  l'envoi est refusé et rien n'est stocké.
- Chaque pièce jointe garde son nom d'origine, son type, sa taille et sa date d'envoi. Le
  type est déduit du contenu reconnu, jamais de ce que déclare le client.
- La carte complète liste ses pièces jointes, chacune avec un **lien de téléchargement
  temporaire** ; le tableau indique seulement leur nombre sur chaque carte.
- L'élève du tableau voit et télécharge les pièces jointes d'une carte active ; il ne peut ni
  en ajouter ni en supprimer.
- Étant donné une carte d'un tableau d'un autre professeur, alors l'envoi répond
  « introuvable » ; étant donné un tableau archivé, alors il est refusé.
- Étant donné une carte archivée, ses pièces jointes restent consultables par le professeur
  et sont conservées à la restauration.

## US-E20-13 — Supprimer une pièce jointe

**En tant que** professeur
**je veux** retirer une pièce jointe d'une carte
**afin de** corriger une erreur ou remplacer un document périmé.

**Priorité :** Devrait · **Dépend de :** US-E20-12

**État :** Implémentée

**Critères d'acceptation**

- Le professeur propriétaire supprime une pièce jointe d'une carte active d'un tableau non
  archivé ;
  elle disparaît **définitivement**, fichier stocké compris — pas d'archivage. Une carte
  archivée est en lecture seule : on ne peut ni y joindre ni y retirer de fichier.
- Un lien de téléchargement déjà délivré cesse de fonctionner au plus tard à son expiration.
- Une pièce jointe d'une autre carte ou d'un autre tableau répond « introuvable ».
- Toute tentative de suppression par l'élève est refusée.
