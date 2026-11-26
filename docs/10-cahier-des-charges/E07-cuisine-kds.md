# E07 — Cuisine (KDS)

Le KDS est une vue plein écran de la PWA, connectée avec un compte Manager, alimentée en
temps réel (SignalR). Pas d'impression en V1.

### US-E07-1 — Voir les commandes à préparer

En tant que cuisinier, je veux voir en temps réel les commandes à préparer afin de les traiter
dans le bon ordre.

**Priorité :** V1
**État :** Partielle
**Avancement :** File faite en J2 (`GET /manage/orders/kitchen`, tri et visibilité des commandes programmées) ; écran `/service/cuisine` fait (F6 : deux colonnes, personnalisations mises en évidence, carillon ; la bande « À valider » des commandes à payer au retrait a été retirée le 2026-11-26, ADR-0013), à éprouver en service simulé.

**Critères d'acceptation**
- Affiche les commandes `Confirmée` et `En préparation`, en colonnes ou en cartes.
- Tri par **heure de retrait prévue** (ASAP = heure estimée), puis par heure de validation.
- Une commande programmée n'apparaît qu'à partir de *heure de retrait − délai de préparation*
  (paramètre) pour ne pas encombrer l'écran.
- Chaque carte montre : numéro, heure de retrait, client, lignes avec quantités, composants de
  menus, personnalisations **mises en évidence** (« SANS oignons », « + bacon ×2 », « Pain : Pita »), note client.
- Une nouvelle commande déclenche un signal sonore.
- Lisible à 2 mètres (taille de police, contraste) ; utilisable au doigt.

### US-E07-2 — Faire avancer une commande

En tant que cuisinier, je veux passer une commande en préparation puis prête d'un geste afin de
tenir le client et le personnel qui remet les commandes informés.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`start`, `ready`, `undo` sous 10 s, historique) ; écran fait (F6 : une touche par étape, annulation proposée 9 s ; colonne « Prêtes » jusqu'à la remise, décision du 2026-11-26).

**Critères d'acceptation**
- `Confirmée → En préparation → Prête`, une touche par transition.
- Annulation de la dernière transition possible pendant 10 secondes (erreur de doigt).
- Une commande `Prête` quitte les colonnes de préparation et passe dans une colonne « Prêtes »
  du KDS jusqu'à sa remise (elle reste aussi dans « à remettre » du back-office) : le
  cuisinier / Manager n'a pas à quitter l'écran pendant le service.
- Chaque transition est historisée (qui, quand).

### US-E07-3 — Résister aux coupures

En tant que cuisinier, je veux que le KDS reste fiable malgré une coupure réseau afin de ne
perdre aucune commande.

**Priorité :** V1
**État :** Partielle
**Avancement :** côté serveur, la resynchronisation complète passe par `GET /manage/orders/kitchen` et une transition refusée renvoie une erreur explicite ; écran fait (F6 : bandeau rouge, relecture complète à chaque reconnexion, geste hors ligne refusé visiblement).

**Critères d'acceptation**
- Un bandeau rouge signale la perte de connexion.
- À la reconnexion, l'écran se resynchronise entièrement depuis l'API (pas seulement par
  événements manqués).
- Une transition tentée hors ligne est refusée visiblement, jamais perdue en silence.

### US-E07-5 — Consulter le détail d'une commande sans quitter le KDS

En tant que cuisinier / Manager, je veux voir le détail et les prix d'une commande en cours ou
prête depuis l'écran cuisine afin de renseigner le client et de remettre sa commande sans
quitter cet écran.

**Priorité :** V1
**État :** Partielle
**Avancement :** écran fait le 2026-11-26 (F6 : numéro du bon et cartes « Prêtes » ouvrent une
fenêtre par-dessus l'écran ; aucune API nouvelle, `GET /manage/orders/{id}` et
`GET /manage/orders?Status=Ready`), testé unitairement ; à vérifier en navigateur et sur la
tablette de la cuisine ; l'encaissement depuis la fenêtre a été retiré le même jour (ADR-0013 :
toute commande arrive payée en ligne).

**Critères d'acceptation**
- Toucher le numéro d'une commande (à préparer, en préparation, prête) ouvre son
  détail dans une fenêtre, sans changer de page ; Fermer ou Échap y ramène.
- Le détail montre : retrait, client, statut, lignes avec quantité et prix de la ligne
  (prix unitaire si quantité > 1), suppléments chiffrés, composants de menus avec leur surcoût,
  note, total, paiement en ligne reçu.
- Une commande `Prête` se remet au client depuis le détail (« Remis au client », deux
  touches : la remise ne s'annule pas) ; elle est toujours déjà payée en ligne
  ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)).
- Hors ligne, le détail d'une commande déjà à l'écran reste consultable.

### US-E07-4 — Vue « à préparer » agrégée

En tant que cuisinier, je veux voir le total des produits à préparer (ex. 7 burgers, 4 frites)
afin d'anticiper.

**Priorité :** V2
**État :** Absente

**Critères d'acceptation**
- Agrégation par produit sur les commandes `Confirmée` et `En préparation`.
