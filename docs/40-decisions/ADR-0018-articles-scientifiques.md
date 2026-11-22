# ADR-0018 — Articles scientifiques : corps en jsonb, images publiques, slug figé

**Statut :** Accepté
**Application :** Appliquée (branche `feature/perfs`, 2026-10-18)
**Date :** 2026-10-18

## Contexte

Les admins (et, depuis [ADR-0025](ADR-0025-role-editeur.md), les profs éditeurs pour leurs propres articles) publient des articles scientifiques (titre, sous-titre, sections faites d'un titre,
d'un texte enrichi et d'une image, références), commentés par les utilisateurs connectés. Les
pages sont rendues par le serveur du front et doivent être indexables. Trois questions :

1. **Comment stocker le corps**, une liste ordonnée de sections de forme variable ?
2. **Comment servir les images** à des pages publiques mises en cache ?
3. **Quelle adresse** donner à un article, et peut-elle changer ?

## Décision

**Le corps vit dans une colonne jsonb** (`Articles.Content`), mappée par EF Core
(`OwnsOne(...).ToJson()`, [`ArticleConfiguration`](../../RestaurantHandler/Persistence/Configurations/ArticleConfiguration.cs)) :
sections et références sont lues et écrites d'un bloc, sans table de jointure ni réordonnancement
ligne à ligne. Titre, sous-titre, langue, statut, tags (`text[]`, index GIN) et dates restent des
colonnes : la liste, le tri, le filtre par tag et les métadonnées SEO ne lisent jamais le jsonb.
Le HTML de chaque section passe par `RichTextSanitizer` à l'écriture (ADR-0014), sa longueur se
mesure sur le texte (`HardCode.ARTICLE_SECTION_TEXT_MAX_LENGTH`).

**Les images sont publiques** : le dossier `articles/` rejoint `avatars/` dans
`StorageService.IsPublic` (ADR-0008). Un lien signé expirerait dans une page en cache ou dans
l'aperçu d'un réseau social. Chaque image est décodée puis ré-encodée en webp par
`ArticleImageService` (largeur 1600 px, couverture recadrée en 1200 × 630), ce qui écarte tout
fichier qui n'est pas une image et retire les métadonnées EXIF. Seules des clés `articles/…`
sont acceptées dans un article ; une image retirée est supprimée du stockage si aucun autre
article ne la référence.

**Le slug est figé à la première publication.** Calculé depuis le titre (`SlugGenerator`, suffixe
`-2`, `-3`… en cas de collision, index unique archivés compris), il reste modifiable tant que
l'article n'a jamais été publié. Dépublier ne le libère pas : l'adresse a pu être indexée.

**Commentaires** : table `ArticleComments`, un seul niveau de réponse (le parent est toujours
racine), publication immédiate. L'auteur ou un admin supprime (les réponses partent avec), un
admin masque via `ArchivedAt`.

**Cache** : liste, détail, tags et plan des articles publiés passent par `HybridCache` sous le
tag `CacheKeys.ARTICLES_TAG` (ADR-0017), invalidé par toute écriture d'article ou de
commentaire. Une recherche libre n'est pas mise en cache.

## Conséquences

- Pas de requête SQL sur le contenu d'une section ; une recherche plein texte demanderait une
  colonne dédiée.
- Une image d'article est lisible par quiconque connaît son URL : n'y mettre que du contenu
  destiné à être publié.
- Renommer l'adresse d'un article publié demanderait une redirection, non prévue.
