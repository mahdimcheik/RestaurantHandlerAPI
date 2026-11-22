# ADR-0024 — Serveur MCP : cours d'un prof et contact anonyme encadré

**Statut :** Accepté
**Application :** Appliquée (branche `feature/mail-broadcast`, 2026-11-19)
**Date :** 2026-11-19

## Contexte

Le serveur MCP public (ADR-0020) ne permettait que de chercher des profs (`fast_search`). Un
assistant IA qui a trouvé un prof doit pouvoir lire ses cours et lui écrire au nom de son
utilisateur. Le serveur reste anonyme : les assistants ne savent pas obtenir le JWT de l'API, et
l'OAuth MCP demanderait un serveur d'autorisation. Trois questions :

1. **Qui écrit au prof**, puisque l'appelant n'a pas de compte ?
2. **Comment désigner le prof** par son nom ou son email sans en faire un oracle d'adresses ?
3. **Comment éviter le spam**, l'outil étant anonyme ?

## Décision

**`get_teacher_courses`** ([`TeacherCourseTools`](../../RestaurantHandler/Mcp/TeacherCourseTools.cs)), en
lecture seule : à partir de l'id renvoyé par `fast_search`, les cours du prof via
`CursusService.GetCursusesByTeacherIdAsync`, description réduite en texte brut. Seuls les profs que
la recherche montre (actifs, non archivés) sont visibles : `CursusService` ne filtre pas sur le statut
du prof, l'outil le fait.

**`contact_teacher`** ([`TeacherContactTools`](../../RestaurantHandler/Mcp/TeacherContactTools.cs),
`TeacherContactService.SendExternalMessageAsync`) envoie un mail au prof **au nom d'un expéditeur
déclaratif** : nom et adresse fournis par l'assistant, jamais vérifiés. Le mail
(`ExternalTeacherContactTemplate`) le dit au prof, porte un `Reply-To` vers l'expéditeur, un objet
préfixé `[BonProf]`, et le message est du **texte brut encodé par Razor** (le contact élève, lui,
accepte du HTML de l'éditeur riche).

Le prof est désigné par **exactement un** de :

- **id** : direct, une erreur s'il est inconnu (l'id est déjà public via `fast_search`) ;
- **nom** : « Prénom Nom », « Nom Prénom » ou un seul mot égal au prénom ou au nom, sans la casse.
  **Jamais l'email** (c'est ce qui exclut le filtre `FullName`, cf. ADR-0020). Plusieurs profs → rien
  n'est envoyé, les candidats (id, nom, cours, lien profil) sont renvoyés pour que l'assistant
  redemande avec l'id ;
- **email** : correspondance exacte sur `NormalizedEmail`. La réponse est **identique** que l'adresse
  appartienne ou non à un prof, sans id : l'outil ne sert pas à tester une adresse.

**Quota dédié : 5 envois par heure et par IP** (`McpContactLimiter`), en plus des 60 requêtes/minute
de `/mcp`. Il est consommé dans l'outil, pas par une policy de route (toutes les requêtes MCP
partagent `/mcp`, la recherche ne doit pas être bloquée par des envois), et **avant** la résolution
du prof, pour que le quota ne dise rien de l'existence d'une adresse. Dépassement = erreur d'outil.

**Documentation** : rubrique `HelpCategory.Assistants` dans le centre d'aide (ADR-0019), fiches FR/EN
dans `Persistence/Seed/help-entries-mcp.json`. Comme la table est déjà remplie en production, ces
fiches portent un **slug fixe** et sont créées au démarrage dès que ce slug manque, archivées
comprises : une fiche supprimée par un admin ne revient pas. Le manifeste pointe vers la fiche
principale (`documentationUrl`, `/aide/serveur-mcp-bonprof`).

## Conséquences

- Le prof reçoit des messages d'inconnus. La mention « expéditeur non vérifié » et la fiche d'aide
  dédiée l'invitent à faire réserver sur la plateforme.
- Le quota est en mémoire, par instance : un redémarrage le remet à zéro, et plusieurs instances
  multiplieraient la limite. Acceptable avec une seule instance de l'API.
- Le temps de réponse par email diffère légèrement selon qu'un mail est mis en file ou non.
- Ajouter une fiche d'aide « après coup » = une entrée avec slug dans `help-entries-mcp.json` (ou un
  nouveau fichier lu par `SeedMissingSlugsAsync`).
- La rubrique `Assistants` demande son libellé et son icône côté Angular.
- Un outil qui agirait avec le compte de l'élève (réserver, contact authentifié) demande toujours
  l'OAuth MCP.
