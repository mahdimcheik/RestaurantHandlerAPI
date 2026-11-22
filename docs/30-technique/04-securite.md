# Sécurité

## Authentification et session

- Mots de passe : ASP.NET Identity (hachage, verrouillage après échecs).
- Session : JWT d'accès court + jeton de renouvellement en cookie `httpOnly`, `Secure`.
  Cible : cookie de session uniquement ([ADR-0002](../40-decisions/ADR-0002-auth-cookies-httponly.md)).
- Bannissement, changement de mot de passe et déconnexion globale invalident les jetons de
  renouvellement.
- Rôle `Manager` jamais attribuable par une route publique.

## Autorisation

- Refus par défaut ; chaque opération déclare son rôle.
- Contrôle de propriété des commandes côté serveur (404 sinon).
- Groupes SignalR attribués **par le serveur** à partir de l'identité, jamais demandés
  librement par le client.

## Argent

- Total recalculé par le serveur ; prix envoyé par le client ignoré.
- Notification SumUp : jamais crue sur parole → relecture de l'état par l'API SumUp avec la clé
  secrète ; traitement idempotent ; montant et devise comparés au total.
- Secrets SumUp uniquement en variables d'environnement, jamais exposés au front.
- Retour navigateur après paiement : n'a **aucun effet** sur l'état de la commande.

## Abus

- Rate limiting existant (par IP, par utilisateur, auth) appliqué aussi à la validation de
  commande.
- Limite d'une commande « payer au retrait » en cours par client, blocage après non-venues.
- Taille de panier bornée (nombre de lignes, quantités) pour éviter les commandes absurdes.

## Données

- Téléphone et e-mail : visibles uniquement du client et des Managers.
- Journaux : pas de données de carte (jamais reçues par l'API), pas de mots de passe ni de
  jetons.
- Uploads (photos) : taille, extension et signature binaire validées (`FileSignatures`).
- Sauvegardes chiffrées au repos chez le fournisseur de stockage.

## Front

- CSP stricte sur la PWA, aucun script tiers hors SumUp.
- Contenu saisi (note client, motifs) affiché comme texte, jamais comme HTML.
