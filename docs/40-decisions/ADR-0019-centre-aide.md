# ADR-0019 — Centre d'aide : fiches éditées par l'admin, recherche sur une colonne normalisée, vidéo par identifiant

**Statut :** Accepté
**Application :** Appliquée (branche `feature/perfs`, 2026-10-19)
**Date :** 2026-10-19

## Contexte

Profs et élèves doivent trouver seuls la réponse à leurs questions (réserver, payer, ouvrir des
créneaux…). Le bandeau d'aide par page du front est figé dans les fichiers de traduction. On veut
un centre d'aide public, indexable, que les admins alimentent sans redéploiement, avec une barre de
recherche et, par fiche, une vidéo YouTube facultative. Trois questions :

1. **Comment chercher** sans moteur dédié, en ignorant accents et casse ?
2. **Comment stocker la vidéo** sans ouvrir le texte enrichi aux iframes ?
3. **Comment classer** les fiches ?

## Décision

**Une fiche par ligne** (`HelpEntries`, [`HelpEntry`](../../RestaurantHandler/Domain/Entities/Help/HelpEntry.cs)) :
question, résumé en texte brut, réponse en HTML nettoyé par `RichTextSanitizer` (ADR-0014), langue,
slug figé à la première publication comme pour les articles (ADR-0018). Une fiche n'existe que dans
une langue.

**La recherche lit une colonne `SearchText`** calculée à l'écriture : question, résumé, mots-clés et
texte de la réponse, en minuscules, sans accents ni ponctuation (`HelpService.Normalize`). La requête
est normalisée de la même façon, découpée en termes, et chaque terme doit apparaître (`LIKE`). Les
fiches dont la question contient le plus de termes passent devant. Pas de plein texte Postgres :
l'extension `unaccent` n'est pas installée, les racinisations `french` et `english` imposent une
langue par requête, et le volume (quelques dizaines de fiches) ne justifie pas d'index. Les
recherches ne sont pas mises en cache ; liste, détail et plan du site le sont sous
`CacheKeys.HELP_TAG` (ADR-0017).

**La vidéo est un identifiant YouTube** (11 caractères), extrait par l'API du lien saisi par l'admin
(youtu.be, watch?v=, /embed/, /shorts/, /live/). Tout autre lien est refusé. Le front construit
lui-même l'adresse `youtube-nocookie.com` et ne charge l'iframe qu'au clic : aucune iframe ni adresse
arbitraire n'entre dans le contenu, et aucun script tiers ne s'exécute avant que le visiteur le demande.

**Classement par deux énumérations fermées**, traduites côté Angular : la rubrique (`HelpCategory`)
et le public (`HelpAudience` : tous, prof, élève). Une fiche « tous » apparaît pour les deux publics.

**Fiches de départ** : `Persistence/Seed/help-entries.json` (français et anglais, rédigées à partir
du bandeau d'aide du front), embarqué dans l'assembly. `HelpEntriesSeeder` les crée et les publie au
démarrage via `HelpService`, seulement si la table est vide, archivées comprises : une fiche
modifiée ou archivée par un admin n'est jamais réécrite.

## Conséquences

- Ajouter une rubrique demande une modification de code (énumération, libellés, icône).
- La recherche ne tolère ni faute de frappe ni variante grammaticale : les mots-clés de la fiche
  servent de synonymes.
- Corriger une fiche de départ déjà en base se fait dans l'admin : modifier le JSON n'a d'effet que
  sur une base neuve.
- Au-delà de quelques milliers de fiches, il faudrait un index trigramme (`pg_trgm`) sur `SearchText`.
