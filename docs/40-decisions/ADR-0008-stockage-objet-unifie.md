# ADR-0008 — Un seul fournisseur de stockage objet

**Statut :** Accepté
**Application :** Appliquée (lot 5, 2026-09-17)
**Date :** 2026-09-12

## Contexte

Le code actuel empile **trois** piles de stockage, avec des chemins de code distincts selon la
fonctionnalité : les avatars passent par l'une, les factures et les avis par une autre, les
médias génériques par une troisième. Chacune a sa configuration, ses variables, sa gestion
d'erreur et son mode de défaillance.

S'y ajoute un contrôleur de fichiers dont le service dont il dépend **n'est jamais
enregistré** : ces opérations échouent à l'exécution.

Cette accumulation n'est le résultat d'aucune décision : c'est une sédimentation.

## Décision

**Un seul fournisseur compatible S3, derrière une seule abstraction**, avec deux espaces de
visibilité :

- **public** — avatars, images de profil : servis directement ;
- **privé** — pièces justificatives, factures : servis par **lien signé à durée limitée**.

Le fournisseur est un détail de configuration ; le code ne connaît que l'abstraction.

## Conséquences

**Ce qu'on gagne**

- Un seul jeu de variables de configuration, un seul mode de défaillance, un seul endroit où
  vérifier les types de fichiers et les tailles.
- Le fournisseur devient interchangeable sans toucher au code applicatif.
- La règle de visibilité est appliquée **au même endroit** pour tous les fichiers, au lieu
  d'être réinventée par fonctionnalité.

**Ce que ça coûte**

- Une reprise de données est nécessaire si les fichiers existants doivent être conservés.
- L'abstraction doit rester **mince** : dès qu'elle cherche à couvrir toutes les
  particularités de tous les fournisseurs, elle devient plus coûteuse que le problème.

**Ce qu'on surveille**

Les fichiers privés ne doivent **jamais** être servis depuis un répertoire statique : c'est la
régression la plus facile à introduire.

## Révision (2026-09-28) — MinIO seul, tout par lien signé

Les deux espaces de visibilité sont abandonnés : **un seul compartiment MinIO privé**
(auto-hébergé sur le VPS, API publique `https://files.bonprof.fr`), et **chaque fichier —
avatars compris — n'est servi que par lien signé**. Plus de compartiment public, d'URL publique,
d'ACL ni de préfixe de visibilité dans les clés : une seule règle, aucune erreur de rangement
possible. Contrepartie : un lien d'avatar expire (`SIGNED_URL_MINUTES`, 60 par défaut) et change
à chaque réponse, donc il n'est pas mis en cache par le navigateur d'une réponse à l'autre.

## Révision (2026-09-30) — avatars publics par préfixe

Les avatars redeviennent publics, **dans le même compartiment** : le préfixe `avatars/` est
ouvert en lecture anonyme (`mc anonymous set download <alias>/bonprof/avatars`, `s3:GetObject`
seul, pas de listing), tout le reste reste privé et signé. Un avatar a ainsi une URL stable,
mise en cache (`Cache-Control: public, max-age=31536000, immutable` — la clé porte un GUID et
n'est jamais réécrite). Risque surveillé : ranger un fichier privé sous `avatars/` le rendrait
public ; le dossier n'est écrit que via la constante `StorageService.AvatarFolder`.

## État dans le code

Une seule classe, [`StorageService`](../../RestaurantHandler/Integrations/Storage/StorageService.cs) (singleton, sans interface), seule enregistrée dans
[`Program.cs`](../../RestaurantHandler/Program.cs) : `Upload` (fichier client, validé), `UploadBytes`
(contenu produit par l'API), `Delete`, et `Url(clé)` qui renvoie une URL publique
non signée pour une clé `avatars/…`, signe localement (aucun appel réseau) toute autre clé, et
renvoie `null` pour une clé invalide.

- **Fournisseur :** MinIO, via les variables `S3_*`. `S3_SERVICE_URL` doit être l'hôte public
  (`https://files.bonprof.fr`) : la signature SigV4 lie l'hôte.
- **Clés :** `{dossier}/{aaaa}/{mm}/{guid}{ext}`, dossier assaini, nom d'origine jamais
  repris ; `StorageService.IsValidKey` refuse toute traversée.
- **Validation :** taille, liste d'extensions, puis signature binaire
  ([`FileSignatures`](../../RestaurantHandler/Utilities/FileSignatures.cs)).
- **Reprise :** aucune. Les clés `public/…` / `private/…` du lot 5 ne pointent plus sur rien ;
  un avatar se re-téléverse, une pièce justificative se renvoie.
