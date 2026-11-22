# E03 — Ouverture, horaires & ruptures

Modèle et API faits en J1 (écarts M6, M7 du
[modèle](../30-technique/02-modele-donnees.md#écarts-à-combler)) ; règles pures dans
`Domain/Policies/` (`AvailabilityPolicy`, `OpeningPolicy`, `SlotPolicy`, `OpeningHoursPolicy`).

### US-E03-1 — Définir les horaires d'ouverture

En tant que Manager, je veux définir les horaires hebdomadaires (plusieurs plages par jour,
ex. 11h30–14h30 et 18h30–22h) afin que les commandes ne soient possibles qu'aux heures
d'ouverture.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/restaurant/hours`, `GET /restaurant/status`) ; affichage en J4.

**Critères d'acceptation**
- Une plage a un jour, une heure de début et de fin ; les plages d'un même jour ne se
  chevauchent pas.
- Les heures sont exprimées dans le fuseau du restaurant (`Europe/Paris`), changements d'heure
  compris.
- La carte affiche « Ouvert », « Fermé — ouvre à 18h30 » ou « Commandes en pause ».

### US-E03-2 — Déclarer une fermeture exceptionnelle

En tant que Manager, je veux déclarer à l'avance une fermeture (jour férié, congés) ou des
horaires exceptionnels afin que les clients ne puissent pas commander pour ces dates.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (`/manage/restaurant/closures`, fermeture partielle possible) ; liste des commandes programmées touchées en J2.

**Critères d'acceptation**
- Une fermeture a une date de début, de fin et un motif facultatif affiché au client.
- Une fermeture créée alors que des commandes programmées existent sur la période liste ces
  commandes au Manager (pas d'annulation automatique).

### US-E03-3 — Mettre les commandes en pause

En tant que Manager, je veux suspendre les commandes en ligne d'un clic pendant un coup de feu
afin de ne pas être débordé, puis les reprendre.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (pause 15/30/60 min ou sans fin, reprise Hangfire, diffusion temps réel) ; refus du panier en J2.

**Critères d'acceptation**
- La pause bloque la validation de nouvelles commandes en ligne ; la saisie comptoir reste
  possible.
- Une durée facultative (15, 30, 60 min) reprend automatiquement les commandes à son terme.
- L'état est diffusé en temps réel aux clients qui ont la carte ouverte.
- Un panier en cours de validation au moment de la pause est refusé avec un message clair.

### US-E03-4 — Déclarer une rupture

En tant que Manager, je veux marquer un ingrédient ou un produit indisponible afin qu'il ne soit
plus commandé.

**Priorité :** V1
**État :** Partielle
**Avancement :** API faite en J1 (ruptures ingrédient et produit, règle R6, diffusion `AvailabilityChanged` aux clients anonymes) ; écrans en J4/J5.

**Critères d'acceptation**
- Un ingrédient indisponible rend indisponible tout produit dont il est un ingrédient **non
  retirable**.
- Un ingrédient indisponible disparaît des suppléments et des substitutions proposés.
- Un ingrédient indisponible **retirable** laisse le produit commandable : le retrait est
  imposé et affiché (« sans tomate — rupture ») — tranché le 2026-11-22 (question 10).
- Un produit peut être marqué indisponible directement (ex. plus de pain spécial).
- Un menu dont un emplacement obligatoire n'a plus aucune option disponible devient
  indisponible.
- Le changement est visible côté client en moins de 5 secondes, sans recharger la page.
- Une commande déjà validée n'est pas affectée.

### US-E03-5 — Afficher le délai de préparation estimé

En tant que client, je veux connaître le délai avant que ma commande soit prête afin de
m'organiser.

**Priorité :** V1
**État :** Partielle
**Avancement :** Paramètre `AsapDelayMinutes` exposé dans l'état d'ouverture (J1) ; figé dans la commande en J2.

**Critères d'acceptation**
- Le délai ASAP = délai de base configuré par le Manager (ex. 15 min), ajustable à la volée
  pendant le service.
- Le délai affiché à la validation est figé dans la commande comme heure de retrait estimée.
- Une règle automatique fondée sur la charge de la cuisine est une V2.

### US-E03-6 — Configurer les créneaux de retrait

En tant que Manager, je veux définir la durée d'un créneau et sa capacité maximale afin de
lisser la production.

**Priorité :** V1
**État :** Partielle
**Avancement :** Paramètres et calcul des créneaux faits en J1 (`GET /restaurant/slots`) ; occupation et vérification sous verrou en J2.

**Critères d'acceptation**
- Paramètres : durée de créneau (ex. 15 min), capacité par créneau **en nombre de commandes**,
  délai minimal avant le premier créneau ; réservation **le jour même uniquement** (question 5
  tranchée le 2026-11-22).
- Les créneaux proposés sont calculés à partir des horaires, fermetures et pause.
- Un créneau plein n'est plus proposé ; la capacité est vérifiée **à la validation**, de façon
  sûre en cas de commandes simultanées.
