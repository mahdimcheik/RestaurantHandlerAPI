# Stratégie de tests

Le dépôt n'a **aucune suite de tests** aujourd'hui (elle a été retirée avec le domaine
d'origine). Elle est recréée en J2, en commençant par ce qui coûte le plus en cas d'erreur :
l'argent et les états.

## Niveaux

| Niveau | Outil | Cible | Exigence |
|---|---|---|---|
| Unitaire | xUnit | `Domain/Policies/` : calcul des prix, ventilation TVA, validation des personnalisations et des menus, transitions d'état, calcul des créneaux, disponibilité | **chaque règle R1–R10 et chaque transition** a au moins un test ; exécution < 5 s |
| Intégration | xUnit + `WebApplicationFactory` + PostgreSQL (Testcontainers) | API de bout en bout : validation de commande, capacité de créneau en concurrence, idempotence, webhook SumUp simulé, permissions (anonyme / client / autre client / Manager) | tous les points d'accès de commande et de paiement |
| Contrat SumUp | sandbox SumUp | création de checkout, paiement test, notification, remboursement | avant la fin de J3 |
| Front | tests de composants sur le panier et la personnalisation ; un parcours e2e (Playwright) click & collect | parcours critiques | J4 / J5 |
| Charge | k6 ou équivalent | pic d'un service : ~ 60 commandes / 15 min, 30 clients en suivi temps réel, KDS connecté | J6 |
| Recette terrain | service simulé avec l'équipe | parcours 1 à 5 de la [vision](../00-vision/02-acteurs-parcours.md) sur les vrais appareils | J6, avant ouverture |

## Cas qui doivent absolument être testés

- Un prix envoyé par le client est ignoré.
- Une rupture survenue entre l'ajout au panier et la validation refuse la commande.
- Deux validations simultanées sur la dernière place d'un créneau : une seule passe.
- Une notification SumUp rejouée ne confirme pas deux fois ; un montant différent met en
  anomalie.
- Un client ne peut lire ni suivre la commande d'un autre.
- `Prête → Retirée` est refusé si la commande n'est pas payée.
- Les snapshots ne changent pas quand la carte est modifiée.
- Le changement d'heure (fin mars / fin octobre) ne décale ni horaires ni créneaux.

## CI

Unitaires + intégration exécutés à chaque push ; un échec bloque le tag de déploiement.
