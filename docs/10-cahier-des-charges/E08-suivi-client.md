# E08 — Suivi client

Seul canal de notification en V1 : **la PWA en temps réel** (SignalR). Pas de push, e-mail ni
SMS de suivi.

### US-E08-1 — Suivre sa commande en temps réel

En tant que client, je veux voir l'avancement de ma commande sans recharger la page afin de
savoir quand venir.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`GET /orders/{id}`, `OrderStatusChanged` envoyé au client, 404 pour un autre client) ; page de suivi faite (F8a : `/commandes/:id`, temps réel, relecture à la reconnexion).

**Critères d'acceptation**
- Page de suivi : numéro (en grand), heure de retrait, état (*En attente de paiement / À
  valider / Confirmée / En préparation / Prête / Retirée / Refusée / Annulée*), contenu, total,
  payée ou à payer au retrait.
- Mise à jour en moins de 2 secondes après chaque transition.
- À la reconnexion (téléphone verrouillé, changement de réseau), l'état est relu depuis l'API.
- Le statut `Prête` est visuellement fort (couleur, animation, vibration si disponible).
- Le client ne voit que ses propres commandes.

### US-E08-2 — Retrouver sa commande en cours

En tant que client, je veux retrouver ma commande en cours dès l'ouverture de l'application afin
de ne pas la chercher.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`GET /orders/mine/current`) ; bandeau en J4.

**Critères d'acceptation**
- Un bandeau « Commande n° 42 — En préparation » est affiché sur toutes les pages tant qu'une
  commande est en cours.

### US-E08-3 — Consulter son historique

En tant que client, je veux voir mes commandes passées et leur reçu afin de garder une trace.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`GET /orders/mine` paginé, détail depuis les snapshots) ; écran en J4.

**Critères d'acceptation**
- Liste paginée, plus récente en premier : date, numéro, total, statut.
- Le détail affiche les lignes telles que commandées (snapshots), même si la carte a changé.

### US-E08-4 — Être alerté quand c'est prêt, application fermée

En tant que client, je veux être prévenu que ma commande est prête même si la PWA est fermée
afin de ne pas surveiller l'écran.

**Priorité :** V2
**État :** Partielle (Web Push natif présent dans le socle, non branché)

**Critères d'acceptation**
- Notification push à `Prête` si le client y a consenti (sur iOS : PWA installée requise).
