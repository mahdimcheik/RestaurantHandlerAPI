# Stratégie de tests

> Statut : **Stable**
>
> BonProfAPI n'avait **aucun projet de test** avant le lot 0, qui a posé le socle d'intégration
> avec les tests de sécurité. Le filet reste le principal écart de qualité à combler, et
> le **préalable** au refactoring — pas par dogme, mais parce qu'un domaine où l'argent dépend
> de l'état ne se vérifie pas à la main, et qu'on ne restructure pas sans filet.

## Priorités

L'effort de test n'est pas réparti uniformément. Par ordre décroissant :

1. **Les règles d'argent** — annulation, commission, dette, compensation, solde.
2. **Les invariants d'idempotence** — double paiement, double versement, rejeu de
   notification.
3. **Les machines à états** — transitions autorisées et surtout **interdites**.
4. **Les contrôles d'accès** — chaque opération, pour chaque rôle, y compris le refus.
5. **Les parcours critiques de bout en bout** — inscription, réservation, paiement.
6. Le reste.

## Les quatre niveaux

### Unitaires — les règles pures

**Cible :** le dossier des règles métier, sans base ni web.

| Ce qui est testé | Exemples de cas |
|---|---|
| Politique d'annulation | les quatre lignes du barème, exactement à la limite des 48 h, arrondis au centime, taux de commission différent de la valeur par défaut |
| Calcul de solde | lignes archivées exclues, dette distinguée par **type** et non par signe, solde négatif |
| Compensation | dette inférieure, égale, supérieure au versement ; **scission** d'une ligne et somme inchangée |
| Transitions de statut | chaque transition autorisée ; **chaque transition interdite refusée** |
| Disponibilité | créneau passé, archivé, occupé, bloqué par une commande en attente |
| Validation SIRET | clé de Luhn valide, invalide, exception La Poste |
| Bornes de mois | changement d'heure, fin d'année |
| Export tabulaire | séparateur, virgule décimale, échappement, encodage |

Ces tests sont **rapides et nombreux** : ils forment le filet principal.

> **Le cas à ne jamais casser :** une ligne de remboursement est négative mais n'est **pas**
> une dette. Un test dédié doit échouer si quelqu'un remplace le contrôle de type par un
> contrôle de signe.

### Intégration — l'API avec une vraie base

**Cible :** l'API complète, sur une base PostgreSQL **éphémère** démarrée par le test. Pas
de base en mémoire : les filtres globaux, les colonnes JSON, les contraintes d'unicité et les
comportements de suppression ne se comportent pas pareil.

| Ce qui est testé | Exemples de cas |
|---|---|
| Contrôles d'accès | pour chaque opération : anonyme, mauvais rôle, bon rôle mais **pas propriétaire**, propriétaire |
| Contraintes | un seul avis par réservation, une adresse par type, un favori unique, un numéro de commande unique |
| Suppressions | une valeur de référentiel référencée n'est pas détruite ; les traces d'audit survivent à la suppression de leur auteur |
| Filtres globaux | une entité archivée n'apparaît pas ; **une somme de portefeuille exclut bien les lignes archivées** |
| Contrat de liste | pagination, tri, filtres, total avant pagination, champ non déclaré refusé |
| Migrations | une base vierge migrée produit le même schéma qu'une base existante migrée |

### Contrat — le prestataire de paiement

**Cible :** tout ce qui touche l'argent, avec un prestataire **simulé**.

| Ce qui est testé | Le comportement attendu |
|---|---|
| Double ouverture de paiement | la seconde échoue, aucune session parallèle |
| Session déjà réglée | l'ouverture est refusée |
| Session ouverte existante | elle est expirée avant l'émission d'une nouvelle |
| Rejeu d'une notification de paiement | aucun second crédit |
| Signature invalide | rejet |
| Notification concernant une session remplacée | ignorée |
| Appel de transfert réussi, enregistrement local échoué | le rejeu envoie **la même clé** et le prestataire ne verse pas deux fois |
| Solde insuffisant | traité comme transitoire, repris au cycle suivant, les autres réservations passent |
| Versement en attente rejoué | **mêmes paramètres**, jamais recalculé |
| Versement entièrement absorbé par la dette | aucun appel externe |

**Ces tests sont les plus importants du projet.** Chacun correspond à un incident qui coûte
de l'argent réel.

### Bout en bout — les parcours

**Cible :** le navigateur, sur un environnement complet avec prestataire simulé.

| Parcours | Étapes |
|---|---|
| Inscription et activation | inscription, confirmation, première connexion |
| Réservation payée | recherche, fiche, créneau, commande, paiement, réservation confirmée |
| Expiration | réservation, attente de l'échéance, créneau libéré |
| Flux sans paiement | réservation chez un professeur non vérifié, acceptation, confirmation |
| Annulation | annulation tardive et anticipée, montant remboursé **conforme à la prévisualisation** |
| Litige | ouverture, acceptation par le professeur, remboursement |
| Certification | dépôt de pièce, refus motivé, nouveau dépôt, validation, compte vérifié |
| Accessibilité | les parcours ci-dessus réalisables **au clavier seul** |

Volontairement peu nombreux : lents et fragiles, ils ne couvrent que ce qu'aucun autre niveau
ne peut couvrir.

## Données de test

- **Constructeurs explicites** pour les entités du domaine, pas d'alimentation globale
  partagée : un test qui dépend d'un jeu de données commun casse quand un autre test le
  modifie.
- Un **générateur de données factices déterministe** pour l'environnement de démonstration,
  pas pour les tests automatisés.
- Chaque test d'intégration s'exécute dans sa **propre transaction annulée**, ou sur une base
  jetable.

## Mise en place dans ce dépôt

État : `tests/BonProf.IntegrationTests` existe depuis le lot 0. Il lance l'API complète
(`WebApplicationFactory<Program>`) sur un PostgreSQL éphémère (Testcontainers, Docker requis) et
porte un test par faille corrigée. Stripe y est simulé au niveau HTTP
([`FakeStripe`](../../tests/BonProf.IntegrationTests/Infrastructure/FakeStripe.cs)) : le SDK est
réellement exercé et la règle d'idempotence de Stripe (même clé, mêmes paramètres) reproduite.
Le stockage objet l'est de la même façon depuis le lot 5
([`FakeS3`](../../tests/BonProf.IntegrationTests/Infrastructure/FakeS3.cs)) : le SDK AWS signe
réellement, et chaque lien renvoyé est vérifié signé.
Les contrats d'argent sont dans `tests/BonProf.IntegrationTests/Money`, écrits avant le lot 3.
`tests/BonProf.UnitTests` (lot 4) couvre les règles de `Domain/Policies/`.
`docker-compose.test.yml` décrit un *environnement* de recette, pas une suite de tests.

```bash
dotnet test tests/BonProf.IntegrationTests
```

```
BonProfAPI/
├── RestaurantHandler/
├── tests/
│   ├── BonProf.UnitTests/          règles pures, sans base ni web
│   └── BonProf.IntegrationTests/   API + PostgreSQL éphémère en conteneur, Stripe et S3 simulés
└── BonProfAPI.sln                  référence les deux projets
```

**Ordre de mise en place** — c'est le lot 1 de la
[feuille de route](../50-backlog/01-roadmap-refactoring.md) :

1. **Tests de caractérisation** des briques à préserver
   ([architecture](01-architecture.md#briques-à-préserver)) : ils figent le comportement
   actuel **avant** tout déplacement de code, y compris les écarts connus — un test qui
   documente un écart est marqué comme tel et inversé quand l'écart est corrigé.
2. **Contrats d'argent** : idempotence du checkout, du versement et du remboursement ;
   compensation de la dette.
3. **Contrôles d'accès** : un test par ligne du tableau des
   [écarts de permissions](../20-specs-fonctionnelles/03-roles-permissions.md#écarts-dans-le-code-actuel),
   écrit rouge, passé au vert par la correction.
4. Le reste, au fil du découpage des services.

Les **services** ne sont testables en unitaire qu'une fois libérés de `ClaimsPrincipal` et de
`HttpContext` ([ADR-0001](../40-decisions/ADR-0001-archi-backend.md)) ; d'ici là, ils se
testent en intégration.

Côté front, les tests relèvent de son propre dépôt ; le parcours de bout en bout ci-dessus
s'exécute contre une API déployée en recette.

## Tests de charge (k6)

`tests/BonProf.LoadTests` charge la recherche de profs, la création de créneaux et la
réservation (flux sans paiement) avec [k6](https://grafana.com/docs/k6/), lancé via Docker
(`./run.sh <search|slots|booking|mixed>`). Les données viennent du seed de démo
(`POST /Dev/seed-all`, Admin, Development ou `ALLOW_DEMO_SEED`) : comptes
`teacher{n}` / `student{n}@bonprof.test` au mot de passe commun, adresses géolocalisées sur
15 villes, formations, expériences, produits, créneaux et réservations ; un rappel avec des
compteurs plus grands n'ajoute que le delta. Le seed ne touche que ces comptes — pas les
fixtures de test (`teacher.{guid}@bonprof.test`) — et il est couvert par
`tests/BonProf.IntegrationTests/Seeding/DemoSeedTests.cs`. Mode d'emploi :
[README du projet](../../tests/BonProf.LoadTests/README.md).

Ils ne tournent **pas** en intégration continue : ils se lancent à la main contre une API locale
ou de recette, avant une mise en production ou après une optimisation de requête.

## Intégration continue

| Étape | Bloquant |
|---|---|
| Compilation, analyse statique, format | oui |
| Tests unitaires | oui |
| Tests d'intégration et de contrat | oui |
| Tests de bout en bout | oui sur la branche principale, informatif sur une branche de travail |

**Pas de seuil de couverture global.** Une couverture forte sur du code sans risque ne prouve
rien. La règle est qualitative : **toute règle métier listée dans
[01-regles-metier.md](../20-specs-fonctionnelles/01-regles-metier.md) a au moins un test
nommé qui la vérifie**, et un invariant non testé est un défaut au même titre qu'un bug.

## Vérification manuelle

Certaines choses ne s'automatisent pas utilement et font l'objet d'une liste de contrôle
avant mise en production :

- une facture réelle est lisible et ses montants sont justes ;
- un e-mail transactionnel s'affiche correctement dans les principaux clients de messagerie ;
- le parcours de paiement réel fonctionne avec une carte de test du prestataire ;
- les retours de paiement et d'onboarding ramènent sur les bonnes pages du front.
