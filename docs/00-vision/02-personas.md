# Personas

> Statut : **Stable**

Quatre rôles techniques (`Student`, `Teacher`, `Admin`, `SuperAdmin`) mais **six profils
d'usage** : le statut professionnel du professeur et son état de vérification changent
radicalement son parcours.

---

## 1. Léa — l'élève

**Rôle technique :** `Student` · **Volume attendu :** le plus gros de la base.

Étudiante ou parent d'élève, cherche un professeur pour une matière précise, près de chez
elle ou en visio. Elle compare, hésite, et veut pouvoir annuler sans se faire piéger.

| | |
|---|---|
| **Objectifs** | trouver vite un professeur crédible et disponible ; payer sans risque ; annuler si besoin |
| **Frustrations** | profils invérifiables, créneaux affichés mais indisponibles, paiement d'avance sans recours |
| **Ce qu'elle fait** | recherche géolocalisée, met en favori, contacte, réserve, paie, discute, annule, note, conteste |
| **Ce qui la fait partir** | un professeur qui ne se présente pas et un remboursement qu'elle doit mendier |

**Parcours critique :** recherche → profil → créneau → commande → paiement sous 30 min →
cours → avis.

---

## 2. Marc — le professeur particulier (non professionnel)

**Rôle technique :** `Teacher`, `IsProfessional = false`.

Donne des cours en complément d'activité. Il n'a pas de SIRET et ne veut pas en créer.

| | |
|---|---|
| **Objectifs** | remplir son planning, être payé sans courir après l'argent |
| **Frustrations** | la paperasse, les commissions opaques, les annulations de dernière minute |
| **Ce qu'il fait** | crée ses créneaux et son catalogue, accepte les réservations, donne le cours, consulte son portefeuille |
| **Contrainte** | tant qu'il n'est ni vérifié ni onboardé sur Stripe, ses réservations passent par le **flux sans paiement** : il accepte ou refuse manuellement sous 48 h, et aucun argent ne circule |

---

## 3. Sonia — la professeure professionnelle (SIRET)

**Rôle technique :** `Teacher`, `IsProfessional = true`, SIRET vérifié auprès de l'INSEE.

Auto-entrepreneuse ou organisme, l'enseignement est son métier. Elle a besoin de
justificatifs comptables propres.

| | |
|---|---|
| **Objectifs** | volume de réservations, versements fiables, factures exploitables |
| **Frustrations** | délais de versement flous, comptabilité à reconstituer à la main |
| **Ce qu'elle fait** | tout ce que fait Marc, plus : onboarding Stripe Connect, consultation de sa comptabilité, bilan mensuel, export CSV |
| **Spécificité** | c'est **elle** le vendeur légal du cours ; la plateforme émet la facture en son nom (mandat de facturation) |

**Point de tension :** si elle annule un cours payé, elle porte une pénalité de 5 % qui
devient une **dette** recouvrée sur ses versements suivants.

---

## 4. Karim — le professeur en cours de vérification

**Rôle technique :** `Teacher`, `IsVerified = false`.

État transitoire mais structurant : c'est le sas d'entrée de toute l'offre.

| | |
|---|---|
| **Objectifs** | être validé vite pour commencer à travailler |
| **Frustrations** | ne pas savoir ce qu'on attend de lui, ni où en est son dossier |
| **Ce qu'il fait** | dépose ses documents (identité, diplôme, justificatif de domicile, K-bis), répond aux demandes de l'administrateur, suit l'état de chaque pièce |
| **Ce dont il a besoin** | une liste claire de ce qui manque et le motif précis d'un refus |

---

## 5. Nadia — l'administratrice

**Rôle technique :** `Admin` · **Volume :** quelques personnes.

Exploite la plateforme au quotidien. Son temps est la ressource rare : tout ce qu'elle
traite manuellement doit être l'exception.

| | |
|---|---|
| **Objectifs** | valider l'offre rapidement, trancher les litiges, garder la comptabilité juste |
| **Frustrations** | dossiers incomplets, litiges sans élément objectif, argent bloqué |
| **Ce qu'elle fait** | revue documentaire, certification, activation/bannissement de comptes, demandes de pièces, arbitrage des litiges, référentiels (catégories, niveaux, types de documents), bilans mensuels |
| **Principe** | elle **tranche au jugement** : un cours en présentiel ne se prouve pas, il n'y a pas de justificatif à exiger |

---

## 6. Le super-administrateur

**Rôle technique :** `SuperAdmin` · **Volume :** une ou deux personnes.

> **À arbitrer.** Aujourd'hui `Admin` et `SuperAdmin` ont exactement les mêmes droits :
> les deux arbres de routes du front sont identiques et aucune capacité ne les distingue.
> La proposition retenue est de **les différencier réellement** : le `SuperAdmin` gère les
> comptes administrateurs et les référentiels sensibles (types de documents, taux,
> paramètres de plateforme), l'`Admin` fait l'exploitation quotidienne. Voir
> [questions ouvertes](../50-backlog/02-questions-ouvertes.md).

---

## Cumul des rôles

Un compte **peut porter plusieurs rôles** — le modèle de données le permet déjà (relation
n-n entre utilisateur et rôle), mais l'interface actuelle suppose un rôle unique et
applique une précédence implicite.

**Décision proposée :** assumer le cumul `Student` + `Teacher` (un professeur peut vouloir
prendre des cours) avec un **sélecteur de rôle explicite** dans l'interface, et l'état
courant porté par la session. Le cumul avec `Admin` reste interdit.
