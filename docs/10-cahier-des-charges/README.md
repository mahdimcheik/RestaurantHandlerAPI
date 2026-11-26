# Cahier des charges — conventions

Le cahier des charges est découpé en **épopées** (`E01` … `E10`), chacune en **user stories**.

## Format d'une story

```markdown
### US-E04-3 — Titre court

En tant que <acteur>, je veux <action> afin de <bénéfice>.

**Priorité :** V1 | V2
**État :** Implémentée | Partielle | Absente | Abandonnée

**Critères d'acceptation**
- Étant donné …, quand …, alors …
```

- **Identifiant** `US-Exx-n` : stable, jamais réutilisé. Une story abandonnée est barrée, pas
  supprimée : titre barré après l'identifiant (`### US-E04-5 — ~~Titre~~`), état `Abandonnée`
  et une ligne `**Abandonnée :**` qui renvoie à la décision ; ses critères restent pour mémoire.
- **Priorité** : `V1` = nécessaire à l'ouverture ; `V2` = après l'ouverture.
- **État** : où en est le code aujourd'hui (`Implémentée`, `Partielle`, `Absente`, ou
  `Abandonnée` si la story n'est plus voulue). Mis à jour à
  chaque jalon de la [feuille de route](../50-feuille-de-route/01-feuille-de-route.md).
- **Critères d'acceptation** : testables, chacun devient au moins un test (unitaire,
  intégration ou recette).

Les règles transverses (calcul du total, états de commande, permissions, légal) ne sont pas
répétées dans les stories : elles renvoient aux [règles métier](../20-regles-metier/).

`./docs/check-docs.sh` vérifie que chaque story porte une priorité, un état et des critères.

## Épopées

| Épopée | Sujet | Jalon |
|---|---|---|
| [E01](E01-comptes-authentification.md) | Comptes & authentification | socle (J0) |
| [E02](E02-carte.md) | Carte | J1 |
| [E03](E03-ouverture-ruptures.md) | Ouverture, horaires & ruptures | J1 |
| [E04](E04-commande-en-ligne.md) | Commande en ligne | J2 / J4 |
| [E05](E05-paiement.md) | Paiement | J3 |
| [E06](E06-comptoir.md) | Gestion des commandes par le restaurant | J5 |
| [E07](E07-cuisine-kds.md) | Cuisine (KDS) | J5 |
| [E08](E08-suivi-client.md) | Suivi client | J4 |
| [E09](E09-statistiques.md) | Statistiques | J5 |
| [E10](E10-administration.md) | Administration | J0 / J5 |
