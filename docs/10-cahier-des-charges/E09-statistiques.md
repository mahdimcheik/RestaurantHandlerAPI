# E09 — Statistiques

Les statistiques de l'application sont des **indicateurs de pilotage**, pas des données
comptables : la référence fiscale des ventes sur place est la caisse certifiée tierce.

## Définitions communes

- **Commande comptée** : commande au statut `Retirée`, ou `Prête` payée (une commande annulée,
  refusée ou non venue n'est jamais comptée dans le CA).
- **CA TTC** : somme des `TotalCents` des commandes comptées, à la date de **retrait**.
- **CA HT / TVA** : ventilés par taux à partir des snapshots de lignes.
- **Journée** : de 00:00 à 23:59 en heure de Paris.

### US-E09-1 — Tableau de bord du jour

En tant que gérant, je veux voir l'activité du jour en direct afin de piloter le service.

**Priorité :** V1
**État :** Absente

**Critères d'acceptation**
- Indicateurs : CA TTC, nombre de commandes, panier moyen (CA / commandes), répartition
  en ligne / comptoir, répartition payé en ligne / payé sur place.
- Compteurs opérationnels : commandes en cours par statut, refusées, non venues, annulées.
- Rafraîchi en temps réel ou à intervalle court (≤ 1 min).

### US-E09-2 — Ventes par produit

En tant que gérant, je veux le classement des produits et menus vendus sur une période afin
d'ajuster la carte.

**Priorité :** V1
**État :** Absente

**Critères d'acceptation**
- Période : jour, semaine, mois, ou dates libres.
- Par produit : quantité vendue (seul et dans les menus, distingués), CA généré.
- Suppléments les plus vendus.

### US-E09-3 — Heures de pointe

En tant que gérant, je veux voir le nombre de commandes et le CA par tranche horaire et par jour
de semaine afin d'organiser les équipes.

**Priorité :** V1
**État :** Absente

**Critères d'acceptation**
- Histogramme par tranche de 15 ou 30 min sur une période choisie.
- Temps moyen entre `Confirmée` et `Prête` par tranche horaire.

### US-E09-4 — Comparer des périodes

En tant que gérant, je veux comparer une période à la précédente afin de mesurer l'évolution.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**
- Évolution en % de CA, commandes, panier moyen.
