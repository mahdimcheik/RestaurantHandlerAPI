# E07 — Cuisine (KDS)

Le KDS est une vue plein écran de la PWA, connectée avec un compte Manager, alimentée en
temps réel (SignalR). Pas d'impression en V1.

### US-E07-1 — Voir les commandes à préparer

En tant que cuisinier, je veux voir en temps réel les commandes à préparer afin de les traiter
dans le bon ordre.

**Priorité :** V1
**État :** Partielle
**Avancement :** File faite en J2 (`GET /manage/orders/kitchen`, tri et visibilité des commandes programmées) ; écran en J5.

**Critères d'acceptation**
- Affiche les commandes `Confirmée` et `En préparation`, en colonnes ou en cartes.
- Tri par **heure de retrait prévue** (ASAP = heure estimée), puis par heure de validation.
- Une commande programmée n'apparaît qu'à partir de *heure de retrait − délai de préparation*
  (paramètre) pour ne pas encombrer l'écran.
- Chaque carte montre : numéro, heure de retrait, canal, lignes avec quantités, composants de
  menus, personnalisations **mises en évidence** (« SANS oignons », « + bacon ×2 », « Pain : Pita »), note client.
- Une nouvelle commande déclenche un signal sonore.
- Lisible à 2 mètres (taille de police, contraste) ; utilisable au doigt.

### US-E07-2 — Faire avancer une commande

En tant que cuisinier, je veux passer une commande en préparation puis prête d'un geste afin de
tenir le client et le comptoir informés.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`start`, `ready`, `undo` sous 10 s, historique) ; écran en J5.

**Critères d'acceptation**
- `Confirmée → En préparation → Prête`, une touche par transition.
- Annulation de la dernière transition possible pendant 10 secondes (erreur de doigt).
- Une commande `Prête` quitte le KDS et apparaît dans « à remettre » du back-office.
- Chaque transition est historisée (qui, quand).

### US-E07-3 — Résister aux coupures

En tant que cuisinier, je veux que le KDS reste fiable malgré une coupure réseau afin de ne
perdre aucune commande.

**Priorité :** V1
**État :** Partielle
**Avancement :** côté serveur, la resynchronisation complète passe par `GET /manage/orders/kitchen` et une transition refusée renvoie une erreur explicite ; bandeau et refus hors ligne à faire dans l'écran.

**Critères d'acceptation**
- Un bandeau rouge signale la perte de connexion.
- À la reconnexion, l'écran se resynchronise entièrement depuis l'API (pas seulement par
  événements manqués).
- Une transition tentée hors ligne est refusée visiblement, jamais perdue en silence.

### US-E07-4 — Vue « à préparer » agrégée

En tant que cuisinier, je veux voir le total des produits à préparer (ex. 7 burgers, 4 frites)
afin d'anticiper.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**
- Agrégation par produit sur les commandes `Confirmée` et `En préparation`.
