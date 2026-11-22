# E06 — Commande au comptoir et gestion des commandes

### US-E06-1 — Saisir une commande au comptoir

En tant que Manager, je veux saisir rapidement la commande d'un client présent afin de
l'envoyer en cuisine.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`POST /manage/orders/counter`, non bloquée par la pause) ; écran tactile en J5.

**Critères d'acceptation**
- Interface tactile optimisée tablette : catégories en tuiles, personnalisation en deux
  touches maximum pour les cas courants (« sans X », supplément).
- Mêmes règles de calcul et de validation que la commande en ligne ; canal `Comptoir`.
- Client de passage par défaut (sans compte) ; un prénom facultatif peut être saisi pour
  l'appel.
- Les commandes comptoir ne sont **pas** bloquées par la pause des commandes en ligne ; elles
  le sont par la fermeture.
- La commande part en cuisine dès sa validation (statut `Confirmée`) ; le numéro est affiché en
  grand pour l'annoncer.
- L'encaissement se fait sur la caisse certifiée tierce, puis marquage [US-E05-3](E05-paiement.md#us-e05-3--marquer-une-commande--payée--sur-place).

### US-E06-2 — Voir les commandes du jour

En tant que Manager, je veux une liste des commandes du jour filtrable par statut, canal et
état de paiement afin de piloter le service.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`GET /manage/orders`, filtres statut / canal / payée, détail avec historique) ; écran en J5.

**Critères d'acceptation**
- Mise à jour en temps réel.
- Colonnes : numéro, heure de retrait, client, canal, statut, payée / à encaisser, total.
- Accès au détail complet (lignes, personnalisations, note, historique des statuts).

### US-E06-3 — Valider une commande à payer au retrait

En tant que Manager, je veux accepter ou refuser une commande en ligne à payer au retrait afin
de ne préparer que des commandes crédibles.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (accepter, refuser avec motif, refus automatique Hangfire après `ApprovalTimeoutMinutes`, temps réel) ; son et badge en J5.

**Critères d'acceptation**
- Les commandes `À valider` sont mises en évidence (son + badge) dans le back-office.
- Accepter → `Confirmée`, la commande apparaît au KDS.
- Refuser → `Refusée`, avec un motif obligatoire affiché au client.
- Sans réponse au bout de N minutes (paramètre, ex. 10), la commande est refusée
  automatiquement et le client prévenu.
- Le client voit le résultat en temps réel sur sa page de suivi.

### US-E06-4 — Remettre une commande

En tant que Manager, je veux marquer une commande comme retirée afin de clore son cycle.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`complete`, refusé si non payée) ; recherche par numéro en J5.

**Critères d'acceptation**
- Possible uniquement depuis `Prête` et si la commande est payée.
- Recherche rapide par numéro.

### US-E06-5 — Déclarer un client non venu

En tant que Manager, je veux marquer une commande prête non retirée comme « non venu » afin de
clore la commande et de suivre les abus.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`no-show` après retrait + délai de grâce, compteur client) ; écran en J5.

**Critères d'acceptation**
- Possible depuis `Prête` après l'heure de retrait + délai de grâce.
- Une commande payée en ligne non retirée **n'est pas** remboursée automatiquement (décision
  Manager).
- Incrémente le compteur de non-venues du client (cf. [US-E04-5](E04-commande-en-ligne.md#us-e04-5--limiter-les-abus-du-paiement-au-retrait)).

### US-E06-6 — Annuler une commande côté restaurant

En tant que Manager, je veux annuler une commande avec un motif afin de gérer un imprévu
(rupture découverte, erreur de saisie).

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (annulation Manager avec motif à tout statut non final) ; remboursement en ligne en J3.

**Critères d'acceptation**
- Possible à tout statut avant `Retirée` ; motif obligatoire ; historisé.
- Une commande payée en ligne est remboursée ([US-E05-4](E05-paiement.md#us-e05-4--rembourser-une-commande-payée-en-ligne)).
- Le client en ligne est notifié en temps réel sur sa page de suivi.
