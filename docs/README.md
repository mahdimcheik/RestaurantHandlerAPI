# Spécifications BonProfAPI

Source de vérité fonctionnelle et technique de l'API BonProf. Tout désaccord entre le code
et ces documents est un bug — de l'un ou de l'autre — et doit être tranché explicitement.

Ces documents décrivent **à la fois la cible et l'existant**. Le produit visé est le même que
celui des specs de NewBonProf, dont ce dossier est issu, mais on l'atteint en **refactorant
BonProfAPI sur place**. Chaque user story et chaque décision indique donc où en est le code,
et [la feuille de route](50-backlog/01-roadmap-refactoring.md) dit dans quel ordre on comble
les écarts.

## Ordre de lecture

| # | Dossier | Contenu | Pour qui |
|---|---|---|---|
| 1 | [`00-vision/`](00-vision/) | Pourquoi le produit existe, pour qui, avec quel vocabulaire | tout le monde |
| 2 | [`10-user-stories/`](10-user-stories/) | Ce que chaque utilisateur peut faire, avec critères d'acceptation | produit, dev, QA |
| 3 | [`20-specs-fonctionnelles/`](20-specs-fonctionnelles/) | Les règles transverses : argent, états, permissions | produit, dev |
| 4 | [`30-specs-techniques/`](30-specs-techniques/) | Comment on le construit | dev, ops |
| 5 | [`40-decisions/`](40-decisions/) | Les arbitrages structurants et leurs conséquences (ADR) | dev |
| 6 | [`50-backlog/`](50-backlog/) | Ce qui reste à trancher, dans quel ordre on refactore, la dette à corriger | produit |

**Raccourci si vous ne lisez qu'une chose :** [le glossaire](00-vision/03-glossaire.md),
puis [les règles métier](20-specs-fonctionnelles/01-regles-metier.md).

## Index

### 00 — Vision
- [Vision produit](00-vision/01-vision-produit.md)
- [Personas](00-vision/02-personas.md)
- [Glossaire](00-vision/03-glossaire.md)
- [Parcours utilisateurs](00-vision/04-parcours-utilisateurs.md)

### 10 — User stories
- [Conventions d'écriture](10-user-stories/README.md)
- [E01 — Authentification & comptes](10-user-stories/E01-authentification-comptes.md)
- [E02 — Profils](10-user-stories/E02-profils.md)
- [E03 — Catalogue de produits](10-user-stories/E03-catalogue-produits.md)
- [E04 — Recherche & découverte](10-user-stories/E04-recherche-decouverte.md)
- [E05 — Créneaux & planning](10-user-stories/E05-creneaux-planning.md)
- [E06 — Réservation & commande](10-user-stories/E06-reservation-commande.md)
- [E07 — Paiement](10-user-stories/E07-paiement.md)
- [E08 — Portefeuille & versements](10-user-stories/E08-wallet-versements.md)
- [E09 — Annulation & remboursement](10-user-stories/E09-annulation-remboursement.md)
- [E10 — Preuve de réalisation](10-user-stories/E10-preuve-de-realisation.md)
- [E11 — Litiges](10-user-stories/E11-litiges.md)
- [E12 — Avis & réputation](10-user-stories/E12-avis-reputation.md)
- [E13 — Messagerie](10-user-stories/E13-messagerie.md)
- [E14 — Notifications](10-user-stories/E14-notifications.md)
- [E15 — Certification & documents](10-user-stories/E15-certification-documents.md)
- [E16 — Facturation & comptabilité](10-user-stories/E16-facturation-comptabilite.md)
- [E17 — Administration](10-user-stories/E17-administration.md)
- [E18 — Référentiels & i18n](10-user-stories/E18-referentiels-i18n.md)
- [E19 — Transverse](10-user-stories/E19-transverse.md)
- [E20 — Suivi de cours](10-user-stories/E20-suivi-de-cours.md)

### 20 — Spécifications fonctionnelles
- [Règles métier](20-specs-fonctionnelles/01-regles-metier.md)
- [Machines à états](20-specs-fonctionnelles/02-machines-etats.md)
- [Rôles & permissions](20-specs-fonctionnelles/03-roles-permissions.md)
- [Flux d'argent](20-specs-fonctionnelles/04-flux-argent.md)
- [Politique d'annulation](20-specs-fonctionnelles/05-politique-annulation.md)
- [Événements & notifications](20-specs-fonctionnelles/06-evenements-notifications.md)

### 30 — Spécifications techniques
- [Architecture](30-specs-techniques/01-architecture.md)
- [Modèle de données](30-specs-techniques/02-modele-donnees.md)
- [Contrat d'API](30-specs-techniques/03-contrat-api.md)
- [Sécurité](30-specs-techniques/04-securite.md)
- [Intégrations externes](30-specs-techniques/05-integrations.md)
- [Tâches planifiées](30-specs-techniques/06-jobs-planifies.md)
- [Clients de l'API](30-specs-techniques/07-clients.md)
- [Exploitation](30-specs-techniques/08-exploitation.md)
- [Stratégie de tests](30-specs-techniques/09-strategie-tests.md)

### 40 — Décisions d'architecture
- [Index des ADR](40-decisions/README.md)

### 50 — Backlog
- [Feuille de route du refactoring](50-backlog/01-roadmap-refactoring.md)
- [Questions ouvertes](50-backlog/02-questions-ouvertes.md)
- [Dette technique](50-backlog/03-dette-technique.md)

## Vérification

```bash
./docs/check-docs.sh
```

Contrôle les liens relatifs (y compris vers le code), les blocs de code, l'unicité des
identifiants de user story, la présence de leurs critères d'acceptation et de leur état,
le référencement des ADR, la validité des diagrammes mermaid, et que chaque contrôleur de
[`RestaurantHandler/Controllers`](../RestaurantHandler/Controllers/) est traité dans le
[contrat d'API](30-specs-techniques/03-contrat-api.md).

## Conventions

- **Langue** : français pour le fonctionnel, anglais pour les identifiants techniques
  (`Reservation`, `PAYABLE`, `CHECKOUT_WINDOW`) — ce sont des noms de code, on ne les
  traduit jamais dans le texte.
- **Montants** : en euros, TTC sauf mention contraire, arrondis au centime
  (`MidpointRounding.AwayFromZero`).
- **Dates & heures** : stockées et raisonnées en UTC ; l'affichage seul est localisé.
- **Statut d'un document** : indiqué en tête (`Stable` · `À arbitrer` · `Brouillon`).
- **Référence croisée** : toujours un lien relatif vers le fichier, jamais une
  reformulation — une règle n'est écrite qu'à un seul endroit.
- **État d'implémentation** : chaque user story porte une ligne `État` —
  `Implémentée` · `Partielle` · `Absente` · `Hors API` (entièrement du ressort du front) —
  et chaque ADR une ligne `Application` —
  `Appliquée` · `Partielle` · `À faire`. Un écart est toujours décrit avec un lien vers le
  code concerné (`../../RestaurantHandler/...`). Ces lignes sont mises à jour **dans la même
  contribution** que le code qui les change.
- **Liens vers le code** : vers le fichier, sans numéro de ligne — les numéros bougent à
  chaque modification, les noms de méthode restent.
