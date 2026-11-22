# ADR-0015 — Pièces jointes des cartes de suivi : fichiers stockés, pas d'image en ligne

**Statut :** Accepté
**Application :** Appliquée (lot 10, 2026-10-03)
**Date :** 2026-10-03

## Contexte

Les professeurs veulent donner à l'élève, sur une carte de suivi, un énoncé photographié, une
fiche d'exercices ou une copie corrigée ([US-E20-12](../10-user-stories/E20-suivi-de-cours.md#us-e20-12--joindre-des-fichiers-à-une-carte)).
Aujourd'hui une carte ne porte qu'une description en texte enrichi, dont les images sont
retirées au nettoyage ([ADR-0014](ADR-0014-tableaux-de-suivi.md)). L'ADR-0014 l'anticipait :
admettre des images exigerait de passer par le stockage objet
([ADR-0008](ADR-0008-stockage-objet-unifie.md)), jamais par des données en ligne.

Trois questions :

1. **Où vit le fichier** : dans le HTML de la description (image en ligne) ou à côté de la
   carte (pièce jointe) ?
2. **Quel modèle** : une nouvelle table autonome, une clé de plus sur `Document`, ou un
   sous-type de `Document` ?
3. **Quels fichiers** : la liste blanche globale `ALLOWED_EXTENSIONS` admet aussi `.docx`,
   `.xlsx`, `.csv`, `.txt`, `.mp4` et `.gif`, jusqu'à `MAX_UPLOAD_SIZE_MB` (25 Mo).

## Décision

**Pièce jointe à côté de la carte, pas d'image dans la description.** La liste blanche du
nettoyage HTML ne change pas : aucune balise `img` n'y entre. Une image en ligne obligerait à
faire réécrire par le serveur des URL signées dans du HTML stocké, à suivre quelles clés sont
encore référencées après chaque modification, et ne couvrirait pas les PDF.

**Sous-type de `Document`.** `BoardCardAttachment : Document`, dans sa propre table, sur le
modèle de `PrivacyDocument` : il hérite du nom d'origine, de la clé de stockage, du type, de
la taille et de la date d'envoi, et ajoute la clé étrangère `BoardCardId`. `Document` ne se
charge pas d'une clé facultative de plus.

**Stockage par `StorageService`, toujours par lien signé.** Le fichier est déposé sous
`boards/{boardId}/cards/{cardId}/…` par `StorageService.Upload`, qui vérifie la taille, la
signature et déduit le type du contenu. Seule la clé est en base ; le lien est signé à chaque
lecture (`SIGNED_URL_MINUTES`) et n'est jamais persisté.

**Liste blanche et plafonds propres aux cartes**, plus stricts que la configuration globale et
vérifiés par `BoardService` **avant** l'envoi au stockage :

| Constante (`HardCode`) | Valeur |
|---|---|
| extensions admises | `.jpg`, `.jpeg`, `.png`, `.webp`, `.pdf` |
| `CARD_ATTACHMENT_MAX_SIZE_BYTES` | 10 Mo |
| `CARD_ATTACHMENT_MAX_PER_CARD` | 10 |

Le plafond par carte est contrôlé sous le verrou du tableau (`LockWritableBoardAsync`), comme
les écritures de positions : deux envois simultanés ne dépassent pas dix.

**Mêmes droits que la carte.** Écrire (ajouter, supprimer) exige d'être le professeur
propriétaire d'un tableau non archivé ; lire suit la règle de `GetCardAsync` — le propriétaire
voit les cartes archivées, l'élève seulement les actives d'un tableau actif. Ailleurs : `404`.

**Suppression physique.** Une pièce jointe ne s'archive pas : sa ligne est supprimée, puis
l'objet par `StorageService.Delete`, idempotent et silencieux en cas d'échec. Archiver une
carte ne touche pas à ses pièces jointes.

## Conséquences

**Ce qu'on gagne**

- Aucune évolution du nettoyage HTML ni de sa surface d'attaque.
- La chaîne de dépôt déjà éprouvée pour les pièces justificatives (taille, signature, type
  déduit, lien signé) sert telle quelle.
- Le tableau reste léger : il ne transporte qu'un nombre par carte, les liens ne sont signés
  qu'à l'ouverture d'une carte.

**Ce que ça coûte**

- L'image n'apparaît pas au fil du texte : la description renvoie à « la pièce jointe »
  plutôt que de la montrer à l'endroit voulu.
- Un lien signé expire : un écran resté ouvert plus de `SIGNED_URL_MINUTES` doit relire la
  carte pour en obtenir un neuf.
- Si la suppression de l'objet échoue après celle de la ligne, l'objet reste orphelin dans le
  compartiment, inaccessible sans lien signé. On l'accepte, comme pour les avatars.
- Un professeur peut stocker jusqu'à 100 Mo par carte, sans quota global par compte.

**Ce qu'on surveille**

- Le volume stocké par professeur : un quota par compte deviendrait nécessaire avant toute
  ouverture du dépôt aux élèves.
- Les demandes d'images au fil du texte : elles appelleraient un nouvel ADR, pas une
  extension silencieuse de la liste blanche.
- Les objets orphelins : un balayage du préfixe `boards/` contre la table, si leur nombre le
  justifie.

## État dans le code

Appliqué au lot 10 (branche `feat/lot-10-pieces-jointes`) :

- entité `BoardCardAttachment : Document` (table `BoardCardAttachments`, clé étrangère vers la
  carte en cascade), migration `Lot10PiecesJointesCartes` ;
- [`BoardService`](../../RestaurantHandler/Services/BoardService.cs) : `AddAttachmentAsync` contrôle
  type et taille avant tout envoi, compte sous le verrou du tableau, dépose par
  [`StorageService.Upload`](../../RestaurantHandler/Integrations/Storage/StorageService.cs) puis
  supprime l'objet si l'enregistrement échoue ; `DeleteAttachmentAsync` retire la ligne puis
  l'objet ; liens signés à chaque lecture de la carte, nombre sur chaque tuile ;
- `BoardsController` : `[RequestSizeLimit]` à 11 Mo sur l'envoi, pour que le dépassement des
  10 Mo reçoive le message de l'API et non un refus brut du serveur ;
- une carte archivée est en lecture seule pour ses pièces jointes (`404` en écriture) ;
- la suppression répond `200`, comme celle d'une liste, et non `204` ;
- les cartes n'étant jamais supprimées physiquement, la cascade de la carte vers ses pièces
  jointes ne joue pas en pratique ; si elle jouait, la ligne parente de `Documents` resterait.
