# Décisions d'architecture

> Statut : **Stable**

Un ADR (*Architecture Decision Record*) consigne **une** décision structurante : son
contexte, ce qui a été décidé, et ce que cela coûte. Il n'est jamais réécrit après coup —
une décision qui change donne lieu à un nouvel ADR qui remplace l'ancien.

Une décision peut être **acceptée** sans être encore **appliquée** : c'est le cas de toutes
celles qui décrivent la cible du refactoring. La ligne *Application* et la section *État dans
le code* disent où en est le code ; elles, et elles seules, sont mises à jour au fil du
refactoring.

## Format

```markdown
# ADR-00xx — Titre

**Statut :** Proposé · Accepté · Remplacé par ADR-00yy
**Application :** Appliquée · Partielle · À faire
**Date :** AAAA-MM-JJ

## Contexte
Le problème, les contraintes, ce qui a été essayé.

## Décision
Ce qui est décidé, à l'affirmatif.

## Conséquences
Ce que ça coûte, ce que ça interdit, ce qu'il faudra surveiller.

## État dans le code
Où la décision est appliquée, ou ce qui manque, avec des liens vers les fichiers.
```

## Index

| # | Décision | Statut | Application |
|---|---|---|---|
| [0001](ADR-0001-archi-backend.md) | Contrôleurs + services + EF Core dans un seul projet | Accepté | Partielle |
| [0002](ADR-0002-auth-cookies-httponly.md) | Session par cookie `httpOnly` plutôt que jeton en en-tête | Accepté | À faire |
| [0003](ADR-0003-commande-mono-prof.md) | Une commande porte sur un seul professeur | Accepté | Appliquée |
| [0004](ADR-0004-idempotence-stripe.md) | Clés d'idempotence dérivées de données stables | Accepté | Appliquée |
| [0005](ADR-0005-versement-par-reservation.md) | Un versement par réservation, pas par lot | Accepté | Appliquée |
| [0006](ADR-0006-dette-prof-compensation.md) | Dette du professeur recouvrée par compensation | Accepté | Appliquée |
| [0007](ADR-0007-i18n-referentiels.md) | Libellés de référentiel en colonne JSON sur la ligne | Accepté | Appliquée |
| [0008](ADR-0008-stockage-objet-unifie.md) | Un seul fournisseur de stockage objet | Accepté | Appliquée |
| [0009](ADR-0009-fenetre-de-litige.md) | Fenêtre de litige égale au délai de versement | Accepté | Appliquée |
| [0010](ADR-0010-flux-sans-paiement.md) | Flux sans paiement pour les professeurs non payables | Accepté | Appliquée |
| [0011](ADR-0011-resolution-auto-litige.md) | Résolution automatique d'un litige en faveur du professeur à J+7 | Accepté | À faire |
| [0012](ADR-0012-code-de-presence.md) | Code de présence détenu par l'élève, sans enjeu par construction | Accepté | Partielle |
| [0013](ADR-0013-litige-partiel-a-charge-du-prof.md) | Remboursement partiel d'un litige à la charge du professeur | Accepté | Appliquée |
| [0014](ADR-0014-tableaux-de-suivi.md) | Tableaux de suivi : rattachement par réservation partagée, positions contiguës, HTML nettoyé à l'écriture | Accepté | Appliquée |
| [0015](ADR-0015-pieces-jointes-cartes.md) | Pièces jointes des cartes de suivi : fichiers stockés par lien signé, pas d'image en ligne | Accepté | Appliquée |
| [0016](ADR-0016-pieces-jointes-chat.md) | Fichiers dans le chat de réservation : envoi HTTP, un fichier par message, vingt par réservation, pas de suppression | Accepté | Appliquée |
| [0017](ADR-0017-cache-memoire.md) | Cache en mémoire des référentiels via HybridCache, sans Redis tant qu'il n'y a qu'une instance | Accepté | Appliquée |
| [0018](ADR-0018-articles-scientifiques.md) | Articles scientifiques : corps en jsonb, images publiques ré-encodées, slug figé à la première publication | Accepté | Appliquée |
| [0019](ADR-0019-centre-aide.md) | Centre d'aide : fiches éditées par l'admin, recherche sur une colonne normalisée, vidéo YouTube par identifiant | Accepté | Appliquée |
| [0020](ADR-0020-serveur-mcp.md) | Serveur MCP public : un outil de recherche anonyme, stateless, sans recherche par nom | Accepté | Appliquée |
| [0021](ADR-0021-notifications-natives-web-push.md) | Notifications natives par Web Push (VAPID), sans application store | Accepté | Appliquée |
| [0022](ADR-0022-chiffrement-adresses.md) | Chiffrement au repos des adresses postales (rue et complément) | Accepté | Appliquée |
| [0023](ADR-0023-envois-mail-groupes.md) | Envois groupés par e-mail : un mail par destinataire, Reply-To de l'expéditeur | Accepté | Appliquée |
| [0024](ADR-0024-mcp-contact-et-cours.md) | Serveur MCP : cours d'un prof et contact anonyme encadré | Accepté | Appliquée |
| [0025](ADR-0025-role-editeur.md) | Rôle Éditeur : des profs rédigent leurs propres articles | Accepté | Appliquée |

## Décisions de NewBonProf non reprises

Les specs de NewBonProf, dont ce dossier est issu, comptent deux décisions de plus. Elles ne
concernent pas ce dépôt :

- **Dépôt unique API + front + specs** : BonProfAPI ne contient que l'API ; le front vit
  dans son propre dépôt.
- **Front Angular avec rendu serveur** : décision du front, hors de ce dépôt. Ce que l'API
  lui expose est décrit dans [clients de l'API](../30-specs-techniques/07-clients.md).

Les numéros ont été réattribués à la suite : un numéro de ce dossier ne correspond donc pas
au même numéro dans NewBonProf.
