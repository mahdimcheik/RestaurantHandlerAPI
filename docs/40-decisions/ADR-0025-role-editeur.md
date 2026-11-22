# ADR-0025 — Rôle Éditeur : des profs rédigent leurs propres articles

**Statut :** Accepté
**Application :** Appliquée (branche `feature/editor-role`, 2026-11-19), non vérifiée dans le navigateur
**Date :** 2026-11-19

## Contexte

Les articles ([ADR-0018](ADR-0018-articles-scientifiques.md)) n'étaient rédigés que par
l'administration. Certains professeurs veulent publier sur leur discipline, et la plateforme y
gagne du contenu et des profils mis en avant. Il faut leur ouvrir l'écriture sans leur donner la
main sur le reste des articles ni sur la modération.

## Décision

- **Un rôle Identity de plus, `Editor`** (`HardCode.ROLE_EDITOR`, migration `AddEditorRole`),
  **cumulé** avec `Teacher`. Ce n'est pas un espace : le prof éditeur reste dans
  `/dashboard/teacher`, où s'ajoute « Mes articles ».
- **Attribué par l'administration**, et à un professeur seulement :
  `PATCH /usermanagement/users/{id}/editor?enabled=true|false` (admin et super-admin), idempotent,
  qui renvoie les rôles du compte.
- **Publication directe**, sans relecture : l'éditeur crée, modifie, publie, dépublie et archive
  **ses** articles (`Article.AuthorId`). Il ne voit pas les autres (`POST /articles/mine`) ; un
  article qui n'est pas le sien répond 404, comme un article inexistant.
- **L'administration garde la main** sur tous les articles, ceux des éditeurs compris, ainsi que
  sur la modération des commentaires et les statistiques.
- **Signature imposée** : un éditeur signe de son prénom et de son nom, le nom saisi est ignoré.
  L'admin garde un nom d'auteur libre.
- **Lien vers le profil** : `ArticleSummary.AuthorProfileId` porte l'identifiant de l'auteur
  quand il est professeur, pour lier son profil public. Il reste nul pour un article
  d'administration : aucun compte admin n'est exposé.
- **Encadré « À propos de l'auteur »** en bas de la page publique : `ArticleDetails.Author`
  (nom, titre, photo, description du profil) n'est rempli que par `GET /articles/slug/{slug}`,
  pour un auteur prof non archivé. Il est lu hors du cache de l'article, pour qu'une modification
  du profil apparaisse aussitôt, et la description passe par `RichTextSanitizer`, comme les
  sections. Depuis, la description du profil est aussi nettoyée à l'enregistrement
  (inscription, mise à jour du profil, création et modification par un admin) : le nettoyage
  à la lecture ne fait plus que couvrir les descriptions enregistrées avant.
- **Le rôle est relu en base à chaque écriture**, pas seulement dans le jeton. Un retrait coupe
  l'écriture tout de suite, alors que le jeton d'accès porte encore le rôle jusqu'à son
  renouvellement (`TOKEN_VALIDITY_MINUTES`) ; seul le menu du front attend ce renouvellement.
- **Retirer le rôle ne touche pas aux articles** : ils restent publiés et l'admin peut les
  reprendre ou les archiver.

## Conséquences

- Pas de circuit de relecture : un contenu inapproprié est retiré après coup par un admin
  (dépublier ou archiver). Une relecture (statut « en revue ») pourra s'ajouter sans changer le
  rôle.
- `[Authorize(Roles = "Admin,SuperAdmin,Editor")]` filtre à l'entrée, la propriété est vérifiée
  dans `ArticleService` ; toute nouvelle écriture d'article doit passer par `CanWriteAsync`.
