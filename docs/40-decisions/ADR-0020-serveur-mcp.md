# ADR-0020 — Serveur MCP public : un outil de recherche anonyme, stateless, sans recherche par nom

**Statut :** Accepté
**Application :** Appliquée (branche `feature/perfs`, 2026-10-19)
**Date :** 2026-10-19

## Contexte

On veut que des assistants IA (Claude, ChatGPT, Cursor…) puissent chercher des profs BonProf pour un
utilisateur, sans passer par le site. Le protocole standard pour exposer des outils à ces assistants
est MCP (Model Context Protocol). Trois questions :

1. **Quel transport et quel état** derrière le reverse proxy du VPS ?
2. **Quelle surface** exposer à un appelant anonyme ?
3. **Comment le serveur est-il découvert** ?

## Décision

**SDK officiel C# `ModelContextProtocol.AspNetCore`**, transport Streamable HTTP sur `/mcp`, en mode
**stateless** : chaque requête POST est autonome, aucune session n'est gardée en mémoire. Pas
d'affinité de session à configurer, un redéploiement ne coupe aucun client. Configuration et routes
dans [`McpServerSetup`](../../RestaurantHandler/Mcp/McpServerSetup.cs).

> **Complété par [ADR-0024](ADR-0024-mcp-contact-et-cours.md)** : deux outils de plus,
> `get_teacher_courses` et `contact_teacher`. La règle « pas de recherche par nom » ci-dessous vaut
> toujours pour `fast_search` ; le contact par nom ne compare que prénom et nom.

**Un seul outil, `fast_search`** ([`TeacherSearchTools`](../../RestaurantHandler/Mcp/TeacherSearchTools.cs)),
anonyme et en lecture seule, branché sur `TeacherSearchService.GetAllTeacherProfilesAsync` : mêmes
règles que `POST /teachers/all` (profs actifs, non archivés, tri par prochaine dispo). Le nom suit la
page front `/fast-search` ; il est en snake_case car certains clients refusent le tiret.

- Paramètres : matière, ville, code postal, prix min/max, période de disponibilité, nombre de
  résultats (20 au plus). **Pas de recherche par nom** : le filtre `FullName` compare aussi l'email,
  un appelant anonyme pourrait tester si une adresse appartient à un prof. Pas de catégories (un
  assistant ne connaît pas les identifiants) ni de favoris (il faut un compte).
- Sortie réduite aux champs publics du profil, avec un lien `/profile/{id}` vers le front. Jamais
  d'email ni de téléphone.
- **60 requêtes par minute et par IP** (limiteur intégré d'ASP.NET Core). L'IP est celle du client
  parce que `UseForwardedHeaders` croit les en-têtes `X-Forwarded-*` venant des réseaux privés (le
  reverse proxy joint l'API par un réseau Docker) ; par défaut ASP.NET Core ne croit que le
  loopback, et tous les clients partageaient alors l'IP du proxy, donc la même limite.

**Manifeste statique** sur `/.well-known/mcp-server` (et `/.well-known/mcp.json` en alias) : nom,
version, URL du transport tirée de `API_BACK_URL` (pas de la requête : derrière le proxy, l'API voit
le saut interne en `http`), absence d'authentification, liste des outils.
Aucun format n'est encore normalisé ; celui-ci suit la proposition « MCP Server Card ».

## Conséquences

- Ajouter un outil = une méthode `[McpServerTool]` dans une classe `[McpServerToolType]`, enregistrée
  dans `AddBonProfMcp`, et une ligne dans le manifeste.
- Un outil qui agit pour un utilisateur (favoris, réservation) demandera l'autorisation OAuth prévue
  par MCP ; le JWT actuel de l'API ne suffit pas, les assistants ne savent pas l'obtenir.
- Les routes `/mcp` et `/.well-known/*` sont déclarées dans le test `AnonymousRouteSweepTests`.
- En maintenance, `/mcp` répond 503 comme le reste de l'API.
- Les réponses SSE du SDK portent `X-Accel-Buffering: no` : nginx ne les met pas en tampon, aucune
  configuration du proxy n'est nécessaire.
- Faire confiance aux réseaux privés suppose que l'API ne publie aucun port : si elle devenait
  joignable directement, n'importe qui pourrait choisir son IP via `X-Forwarded-For`.
