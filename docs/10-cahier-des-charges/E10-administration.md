# E10 — Administration

### US-E10-1 — Gérer les utilisateurs

En tant que Manager, je veux lister, rechercher et consulter les comptes afin d'assister un
client.

**Priorité :** V1
**État :** Implémentée
**Avancement :** recherche sur le téléphone, filtre par statut, compteur de non-venues sur la
fiche et `GET /manage/customers/{userId}/orders` ajoutés en F7 ; écran `/gestion/utilisateurs`.

**Critères d'acceptation**
- Recherche par nom, e-mail, téléphone ; filtre par rôle et statut.
- La fiche client montre ses commandes et son compteur de non-venues.

### US-E10-2 — Créer un compte Manager

En tant que Manager, je veux créer un autre compte Manager afin d'équiper un nouvel appareil ou
un nouvel équipier.

**Priorité :** V1
**État :** Implémentée

**Critères d'acceptation**
- Seul un Manager crée un Manager (`POST /Users`) ; le compte créé est « en attente » jusqu'à
  son activation depuis sa fiche.
- Le compte Manager initial est créé au démarrage depuis `ADMIN_EMAIL` / `ADMIN_PASSWORD`.

### US-E10-3 — Changer le statut d'un compte

En tant que Manager, je veux bannir ou réactiver un compte afin d'écarter un client abusif.

**Priorité :** V1
**État :** Implémentée

**Critères d'acceptation**
- Statuts : actif, en attente, banni.
- Un compte banni ne peut plus se connecter et ses sessions sont invalidées.
- Un Manager ne peut pas se bannir lui-même ni bannir le dernier Manager actif (contrôle
  serveur ajouté en F7, `UsersApiTests`).

### US-E10-4 — Lever un blocage « paiement au retrait »

En tant que Manager, je veux réautoriser le paiement au retrait pour un client bloqué afin de
gérer une exception.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J5 (`POST /manage/customers/{userId}/no-shows/reset`, historisé dans `NoShowResets` ; compteur visible du Manager dans le détail de commande et sur la fiche utilisateur) ; écran fait (F7, fiche utilisateur).

**Critères d'acceptation**
- Remet à zéro le compteur de non-venues ; action historisée.

### US-E10-5 — Paramétrer le restaurant

En tant que Manager, je veux renseigner les informations du restaurant afin qu'elles
apparaissent sur la PWA et les reçus.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/restaurant/settings`, logo, historique) ; écran en J5.

**Critères d'acceptation**
- Nom commercial, raison sociale, adresse, téléphone, SIRET, n° de TVA intracommunautaire,
  logo.
- Paramètres de service : délai ASAP, durée et capacité des créneaux, délai de validation
  Manager, délai de grâce de retrait, seuil de non-venues, délai d'expiration des paiements en
  ligne.
- Chaque modification est historisée.
