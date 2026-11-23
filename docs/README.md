# Documentation RestaurantHandler

Source de vérité fonctionnelle et technique de **RestaurantHandler**, l'application de
commande d'un fast-food à emporter : click & collect, saisie au comptoir, écran cuisine,
statistiques. Tout désaccord entre le code et ces documents est un bug, de l'un ou de
l'autre, et doit être tranché explicitement.

Rédigée le 2026-11-22, après le retrait du domaine d'origine (BonProf) et la création du modèle
de données restaurant. Elle remplace intégralement l'ancienne documentation.

## Ordre de lecture

| # | Dossier | Contenu |
|---|---|---|
| 1 | [`00-vision/`](00-vision/) | Pourquoi, pour qui, périmètre V1, vocabulaire |
| 2 | [`10-cahier-des-charges/`](10-cahier-des-charges/) | User stories par épopée, avec critères d'acceptation |
| 3 | [`20-regles-metier/`](20-regles-metier/) | Calcul, états, permissions, légal |
| 4 | [`30-technique/`](30-technique/) | Architecture, modèle, exploitation, sécurité, tests |
| 5 | [`40-decisions/`](40-decisions/) | Arbitrages structurants (ADR) |
| 6 | [`50-feuille-de-route/`](50-feuille-de-route/) | Jalons, questions ouvertes, risques |

**Si vous ne lisez que trois choses :** la [vision](00-vision/01-vision-objectifs.md), la
[feuille de route](50-feuille-de-route/01-feuille-de-route.md) et les
[questions ouvertes](50-feuille-de-route/02-questions-ouvertes.md).

## Index

### 00 — Vision
- [Vision et objectifs](00-vision/01-vision-objectifs.md)
- [Acteurs et parcours](00-vision/02-acteurs-parcours.md)
- [Glossaire](00-vision/03-glossaire.md)

### 10 — Cahier des charges
- [Conventions](10-cahier-des-charges/README.md)
- [E01 — Comptes & authentification](10-cahier-des-charges/E01-comptes-authentification.md)
- [E02 — Carte](10-cahier-des-charges/E02-carte.md)
- [E03 — Ouverture, horaires & ruptures](10-cahier-des-charges/E03-ouverture-ruptures.md)
- [E04 — Commande en ligne](10-cahier-des-charges/E04-commande-en-ligne.md)
- [E05 — Paiement](10-cahier-des-charges/E05-paiement.md)
- [E06 — Comptoir et gestion des commandes](10-cahier-des-charges/E06-comptoir.md)
- [E07 — Cuisine (KDS)](10-cahier-des-charges/E07-cuisine-kds.md)
- [E08 — Suivi client](10-cahier-des-charges/E08-suivi-client.md)
- [E09 — Statistiques](10-cahier-des-charges/E09-statistiques.md)
- [E10 — Administration](10-cahier-des-charges/E10-administration.md)

### 20 — Règles métier
- [Règles de commande](20-regles-metier/01-regles-commande.md)
- [Machine à états de la commande](20-regles-metier/02-machine-etats-commande.md)
- [Rôles et permissions](20-regles-metier/03-roles-permissions.md)
- [Conformité légale](20-regles-metier/04-conformite-legale.md)

### 30 — Technique
- [Architecture](30-technique/01-architecture.md)
- [Modèle de données](30-technique/02-modele-donnees.md)
- [Exploitation](30-technique/03-exploitation.md)
- [Sécurité](30-technique/04-securite.md)
- [Stratégie de tests](30-technique/05-tests.md)

### 40 — Décisions
- [Index des ADR](40-decisions/README.md)

### 50 — Feuille de route
- [Feuille de route](50-feuille-de-route/01-feuille-de-route.md)
- [Feuille de route du front](50-feuille-de-route/04-feuille-de-route-front.md)
- [Questions ouvertes](50-feuille-de-route/02-questions-ouvertes.md)
- [Risques et dette](50-feuille-de-route/03-risques-dette.md)

## Maintenance

- `./docs/check-docs.sh` vérifie les liens, les blocs de code, le format des stories et les
  ADR référencés.
- À chaque jalon : mettre à jour l'**État** des stories concernées, cocher la feuille de route,
  déplacer les questions tranchées.
