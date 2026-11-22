# ADR-0003 — Un seul stockage objet S3

**Statut :** Accepté (repris du socle)
**Date :** 2026-11-22

## Contexte

Le socle dispose d'une abstraction unique, `StorageService`
(`RestaurantHandler/Integrations/Storage/StorageService.cs`), vers un fournisseur compatible S3
(OVH Object Storage). Le restaurant en a besoin pour les photos de la carte et les avatars.

## Décision

- **Un seul fournisseur S3, une seule classe** ; le fournisseur est un détail de configuration
  (`S3_*`).
- Les images **publiques** (photos de produits, catégories, avatars) sont rangées sous des
  préfixes ouverts en lecture anonyme, sans listing, avec des clés immuables
  (`{dossier}/{aaaa}/{mm}/{guid}{ext}`) mises en cache longtemps.
- Tout autre fichier est privé et servi par lien signé.
- Chaque upload est validé : taille, extension, signature binaire.

## Conséquences

- Une image de carte a une URL stable, cachable par le navigateur et le service worker.
- Risque surveillé : ranger un fichier privé sous un préfixe public le rend public ; les
  dossiers publics ne sont écrits que via des constantes dédiées.
