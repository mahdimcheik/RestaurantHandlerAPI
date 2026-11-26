# E09 — Statistiques

Les statistiques de l'application sont des **indicateurs de pilotage**, pas des données
comptables. Toutes les ventes de l'application sont des ventes en ligne payées par carte
([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) ; les éventuelles ventes au
comptoir du restaurant sont hors application.

## Définitions communes

- **Commande comptée** : commande au statut `Retirée`, ou `Prête` payée (une commande annulée
  ou non venue n'est jamais comptée dans le CA).
- **CA TTC** : somme des `TotalCents` des commandes comptées, à la date de **retrait**.
- **CA HT / TVA** : ventilés par taux à partir des snapshots de lignes.
- **Journée** : de 00:00 à 23:59 en heure de Paris.

### US-E09-1 — Tableau de bord du jour

En tant que gérant, je veux voir l'activité du jour en direct afin de piloter le service.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J5 (`GET /manage/stats/dashboard`) ; écran fait (F7, tableau de bord du back-office, relu en temps réel).

**Critères d'acceptation**
- Indicateurs : CA TTC, nombre de commandes, panier moyen (CA / commandes), montant payé en
  ligne (la répartition par canal et « payé sur place » a été retirée, ADR-0013).
- Compteurs opérationnels : commandes en cours par statut, non venues, annulées.
- Rafraîchi en temps réel ou à intervalle court (≤ 1 min).

### US-E09-2 — Ventes par produit

En tant que gérant, je veux le classement des produits et menus vendus sur une période afin
d'ajuster la carte.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J5 (`GET /manage/stats/products` (jour, semaine, mois, plage libre ≤ 366 jours)) ; écran fait (F7, `/gestion/statistiques`).

**Critères d'acceptation**
- Période : jour, semaine, mois, ou dates libres.
- Par produit : quantité vendue (seul et dans les menus, distingués), CA généré.
- Suppléments les plus vendus.

### US-E09-3 — Heures de pointe

En tant que gérant, je veux voir le nombre de commandes et le CA par tranche horaire et par jour
de semaine afin d'organiser les équipes.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J5 (`GET /manage/stats/peak-hours` (tranches de 15 ou 30 min, temps moyen de préparation)) ; écran fait (F7, `/gestion/statistiques`, filtre par jour de semaine).

**Critères d'acceptation**
- Histogramme par tranche de 15 ou 30 min sur une période choisie.
- Temps moyen entre `Confirmée` et `Prête` par tranche horaire.

### US-E09-4 — Comparer des périodes

En tant que gérant, je veux comparer une période à la précédente afin de mesurer l'évolution.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**
- Évolution en % de CA, commandes, panier moyen.
