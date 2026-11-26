# E06 — Gestion des commandes par le restaurant

Toutes les commandes sont passées en ligne et payées par carte avant d'arriver en cuisine
([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) ; le restaurant les suit, les
remet, les déclare non venues ou les annule à la demande du client.

### US-E06-1 — ~~Saisir une commande au comptoir~~

En tant que Manager, je veux saisir rapidement la commande d'un client présent afin de
l'envoyer en cuisine.

**Priorité :** V1
**État :** Abandonnée
**Abandonnée :** plus de saisie au comptoir ; les ventes au comptoir passent par la caisse du restaurant, hors de l'application ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md), 2026-11-26).

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

En tant que Manager, je veux une liste des commandes du jour filtrable par statut et numéro
afin de piloter le service.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`GET /manage/orders`, filtres statut / payée / numéro, détail avec historique ; filtre canal retiré le 2026-11-26) ; écran fait (F5, `/service/commandes`).

**Critères d'acceptation**
- Mise à jour en temps réel.
- Colonnes : numéro, heure de retrait, client, statut, payée / paiement en cours, total.
- Accès au détail complet (lignes, personnalisations, note, historique des statuts).

### US-E06-3 — ~~Valider une commande à payer au retrait~~

En tant que Manager, je veux accepter ou refuser une commande en ligne à payer au retrait afin
de ne préparer que des commandes crédibles.

**Priorité :** V1
**État :** Abandonnée
**Abandonnée :** plus de paiement au retrait, donc plus de validation Manager ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md), 2026-11-26).

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
**Avancement :** API faite en J2 (`complete`, refusé si non payée) ; recherche par numéro faite en J5 (`GET /manage/orders?number=`) ; écran fait (F5, et depuis le détail de l'écran cuisine).

**Critères d'acceptation**
- Possible uniquement depuis `Prête` ; la commande est toujours déjà payée en ligne (le
  serveur le revérifie).
- Recherche rapide par numéro.

### US-E06-5 — Déclarer un client non venu

En tant que Manager, je veux marquer une commande prête non retirée comme « non venu » afin de
clore la commande.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (`no-show` après retrait + délai de grâce ; compteur client retiré le 2026-11-26) ; écran fait (F5).

**Critères d'acceptation**
- Possible depuis `Prête` après l'heure de retrait + délai de grâce.
- Une commande payée en ligne non retirée **n'est pas** remboursée automatiquement (décision
  Manager).
- Aucune sanction ni compteur : le paiement au retrait, qu'elle protégeait, n'existe plus
  ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)).

### US-E06-6 — Annuler une commande côté restaurant

En tant que Manager, je veux annuler une commande avec un motif afin de gérer un imprévu
(rupture découverte) ou la demande d'un client, qui ne peut pas annuler lui-même.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J2 (annulation Manager avec motif à tout statut non final) ; remboursement en ligne en J3 ; écran fait (F5).

**Critères d'acceptation**
- Possible à tout statut avant `Retirée` ; motif obligatoire ; historisé.
- C'est la seule annulation à la main : le client appelle le restaurant ou le demande sur
  place ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)) ; une commande non
  payée expire seule ([US-E05-2](E05-paiement.md)).
- Une commande payée en ligne est remboursée ([US-E05-4](E05-paiement.md#us-e05-4--rembourser-une-commande-payée-en-ligne)).
- Le client en ligne est notifié en temps réel sur sa page de suivi.
