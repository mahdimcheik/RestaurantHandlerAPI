# ADR-0003 — Un seul stockage objet S3

**Statut :** Accepté (repris du socle)
**Date :** 2026-11-22

## Contexte

Le socle dispose d'une abstraction unique, `StorageService`
(`RestaurantHandler/Integrations/Storage/StorageService.cs`), vers un fournisseur compatible S3
(OVH Object Storage). Greenadine en a besoin pour les photos de la carte et les avatars.

## Décision

- **Un seul fournisseur S3, une seule classe** ; le fournisseur est un détail de configuration
  (`S3_*`).
- Les images **publiques** (photos de produits, catégories, avatars) sont rangées sous des
  préfixes ouverts en lecture anonyme, sans listing, avec des clés immuables
  (`{dossier}/{aaaa}/{mm}/{guid}{ext}`) mises en cache longtemps.
- Les photos de la carte et le logo sont réencodés en WebP et déclinés en quatre tailles
  (plus grand côté 160, 320, 640 et 1200 px, jamais agrandies), rangées côte à côte :
  `catalog/{aaaa}/{mm}/{guid}-w{taille}.webp`. La base ne garde que la clé `-w1200` ; le client
  (loader de `NgOptimizedImage`, `core/images/catalog-image-loader.ts`) en déduit les autres
  pour le `srcset`, et le navigateur prend la plus petite qui suffit à l'écran. Les photos
  envoyées avant (clé sans suffixe) restent en taille unique. Les deux listes de tailles
  (`CatalogImageService.Widths`, `CATALOG_IMAGE_WIDTHS`) doivent rester identiques.
- Tout autre fichier est privé et servi par lien signé.
- Chaque upload est validé : taille, extension, signature binaire.

## Conséquences

- Une image de carte a une URL stable, cachable par le navigateur et le service worker.
- Risque surveillé : ranger un fichier privé sous un préfixe public le rend public ; les
  dossiers publics ne sont écrits que via des constantes dédiées.
