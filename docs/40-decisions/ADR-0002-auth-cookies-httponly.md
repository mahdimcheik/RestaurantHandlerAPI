# ADR-0002 — Session par cookie `httpOnly`

**Statut :** Accepté
**Application :** À faire
**Date :** 2026-09-12

## Contexte

Le code actuel émet un jeton d'accès conservé dans le navigateur et renvoyé par un intercepteur,
avec un jeton de renouvellement en cookie. L'intercepteur concerné porte d'ailleurs un
commentaire indiquant qu'il est réservé au développement.

Trois problèmes :

1. **Exposition aux injections de script.** Un jeton lisible par le JavaScript de la page est
   exfiltrable. Le produit affiche du texte enrichi — descriptions, messages, avis — ce qui
   élargit la surface.
2. **Révocation impossible.** Un jeton reste valable jusqu'à son expiration, même après
   déconnexion ou changement de mot de passe.
3. **Incompatibilité avec le rendu serveur.** Le serveur de rendu n'a pas accès au stockage
   du navigateur ; il ne peut donc pas appeler l'API au nom de l'utilisateur.

Le troisième point est rédhibitoire : le rendu serveur est une exigence de référencement.

## Décision

La session est portée par un **cookie `httpOnly`, `Secure`, `SameSite=Lax`**, invalidable
côté serveur, avec rotation à chaque renouvellement.

## Conséquences

**Ce qu'on gagne**

- Le secret de session n'est plus accessible au script de la page.
- La déconnexion, le changement de mot de passe et le bannissement **invalident réellement**
  les sessions.
- Le rendu serveur peut relayer le cookie et appeler l'API authentifié.
- Le temps réel utilise la même authentification, sans second mécanisme.

**Ce que ça coûte**

- **Protection anti-falsification obligatoire** sur toute requête mutante : c'est le prix du
  cookie.
- La liste des origines autorisées devient stricte et explicite ; aucun joker.
- Le mode `Lax` doit être vérifié sur les retours de redirection du prestataire de paiement —
  c'est précisément le cas d'usage qu'il laisse passer, mais cela se teste.
- La détection de réutilisation d'un identifiant après rotation demande de suivre les
  familles de sessions.

**Ce qu'on surveille**

Les retours de paiement et les retours d'onboarding : ce sont les seuls parcours où
l'utilisateur revient depuis un domaine tiers.

## État dans le code

Le jeton d'accès est un JWT porté par l'en-tête `Authorization`
([`Program.cs`](../../RestaurantHandler/Program.cs), `AddJwtBearer`). Seul le jeton de renouvellement est en cookie
`httpOnly`, `Secure`, `SameSite=Strict` ([`AuthController`](../../RestaurantHandler/Controllers/AuthController.cs)).
La décision est donc à appliquer pour le jeton d'accès. Le passage en `Lax` est un choix à
valider sur les retours Stripe.
