# ADR-0003 — Une seule façade de stockage, disque local ou S3

**Statut :** Accepté (repris du socle)
**Date :** 2026-11-22

## Contexte

Le socle dispose d'une abstraction unique, `StorageService`
(`RestaurantHandler/Integrations/Storage/StorageService.cs`), vers un fournisseur compatible S3
(OVH Object Storage). Greenadine en a besoin pour les photos de la carte et les avatars.

## Décision

- **Une seule façade, `StorageService`**, devant un backend `IObjectStore` choisi au démarrage par
  `STORAGE_PROVIDER` (amendement du 2026-11-28 : le client refuse de payer un stockage objet, et un
  MinIO auto-hébergé n'apporte rien pour un seul serveur) :
  - `local` : `LocalObjectStore` écrit sous `wwwroot/uploads` (volume Docker), servi en statique par
    l'API sous `/uploads` (cache immutable, `nosniff`). Tout y est public : les fichiers privés y sont
    **refusés** tant qu'aucun endpoint authentifié ne les sert ;
  - `s3` (défaut) : `S3ObjectStore`, compartiment compatible S3 (OVH), variables `S3_*`.
  Les clés sont identiques dans les deux modes ; changer de mode demande de copier les fichiers.
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

- En mode local, les fichiers partagent le disque et les sauvegardes du serveur : le volume
  `uploads` doit être archivé avec la base (cf. exploitation).

- Une image de carte a une URL stable, cachable par le navigateur et le service worker.
- Risque surveillé : ranger un fichier privé sous un préfixe public le rend public ; les
  dossiers publics ne sont écrits que via des constantes dédiées.
