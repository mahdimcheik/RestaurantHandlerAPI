# E19 — Transverse

> Statut : **Stable** — exigences qui ne sont la propriété d'aucune épique mais
> conditionnent l'acceptation de toutes.

## Données personnelles

### US-E19-01 — Recueillir un consentement explicite

**En tant que** plateforme
**je veux** recueillir et conserver le consentement à l'inscription
**afin de** respecter mes obligations légales.

**Priorité :** Doit

**État :** Partielle · **Code :** [`RegistrationService`](../../RestaurantHandler/Services/Auth/RegistrationService.cs), [`UserApp`](../../RestaurantHandler/Domain/Entities/User/UserApp.cs)
Deux consentements obligatoires, stockés sur le compte. La date est celle de création
du compte (`CreatedAt`). Bandeau et page de politique : front.

**Critères d'acceptation**

- Deux consentements distincts et obligatoires : politique de confidentialité et traitement
  des données. Aucune case pré-cochée.
- Le consentement est stocké sur le compte ; sa date est celle de la création.
- La politique de confidentialité est accessible sans être connecté.
- Un bandeau de consentement encadre les traceurs non essentiels.

### US-E19-02 — Exercer mes droits

**En tant qu'** utilisateur
**je veux** obtenir mes données et demander leur suppression
**afin d'** exercer mes droits.

**Priorité :** Devrait

**État :** Absente
Ni export des données ni anonymisation.

**Critères d'acceptation**

- Un export de mes données personnelles est fourni sur demande.
- La suppression **anonymise** le compte plutôt que de le détruire : les écritures comptables
  et les factures ont une durée de conservation légale et ne peuvent pas disparaître.
- Ce qui est anonymisé et ce qui est conservé est écrit noir sur blanc dans la politique de
  confidentialité.

### US-E19-03 — Minimiser l'exposition des données

**En tant que** plateforme
**je veux** que chaque réponse n'expose que le nécessaire
**afin de** limiter les fuites.

**Priorité :** Doit

**État :** Partielle · **Code :** [`UserDTOs`](../../RestaurantHandler/Contracts/User/UserDTOs.cs)
Représentations `*Details` distinctes. Écarts : détail de réservation, conversations et
portefeuille lisibles hors de leurs parties (voir E06-09, E13-01, E08-03) ; factures servies
par l'API plutôt que par lien signé.

**Critères d'acceptation**

- Les représentations publiques, privées et administratives d'un utilisateur sont
  **distinctes** ; la publique ne contient ni e-mail, ni adresse exacte, ni SIRET, ni
  information bancaire.
- Aucune entité de base n'est renvoyée telle quelle : tout passe par une représentation
  dédiée.
- Les fichiers privés ne sont accessibles que par lien signé à durée limitée.

### US-E19-03b — Régler mes préférences d'affichage

**En tant qu'** utilisateur connecté
**je veux** choisir mon thème et ma langue
**afin d'** adapter l'interface à mon confort.

**Priorité :** Pourrait

**État :** Hors API

**Critères d'acceptation**

- Deux réglages seulement : **thème clair ou sombre** et **langue**. Le choix est conservé
  d'une visite à l'autre et appliqué avant la première peinture, sans clignotement.
- Le thème est également basculable depuis l'en-tête, sans passer par cet écran.
- Le changement de langue **ne recharge pas la page** — limite connue du front actuel.
- **Le configurateur de palettes et de préréglages visuels du front actuel n'est pas repris** :
  c'est un outil de démonstration de bibliothèque, pas une fonctionnalité produit.

## Référencement

### US-E19-04 — Être trouvable par les moteurs de recherche

**En tant que** plateforme
**je veux** que les pages publiques soient rendues côté serveur
**afin d'** être indexée correctement.

**Priorité :** Doit

**État :** Hors API
Relève du front ; l'API fournit les données des fiches et des avis.

**Critères d'acceptation**

- Les pages publiques — accueil, recherche, fiche professeur, mentions légales — sont rendues
  côté serveur ; les espaces connectés sont rendus côté client.
- Chaque page publique porte un titre et une description **propres**, une URL canonique et
  les métadonnées de partage social.
- Les données structurées sont générées côté serveur : organisation sur l'accueil, personne et
  note agrégée sur une fiche professeur, fil d'Ariane sur la recherche.
- Le fichier d'exclusion des robots interdit les espaces connectés et les pages de paiement.

### US-E19-05 — Exposer un plan de site à jour

**En tant que** plateforme
**je veux** publier un plan de site incluant les fiches professeurs
**afin que** les nouvelles fiches soient découvertes.

**Priorité :** Devrait

**État :** Absente
Aucun plan de site servi par l'API.

**Critères d'acceptation**

- Le plan de site est servi par le serveur, pas construit dans le navigateur.
- Il couvre les pages statiques **et** les fiches des professeurs vérifiés et actifs.
- Il est régénéré périodiquement et déclaré dans le fichier d'exclusion des robots.
- **Un plan de site déclaré mais inexistant est un défaut** : c'est le cas aujourd'hui.

## Accessibilité

### US-E19-06 — Utiliser le site au clavier et au lecteur d'écran

**En tant qu'** utilisateur en situation de handicap
**je veux** naviguer sans souris
**afin de** pouvoir utiliser le service.

**Priorité :** Devrait

**État :** Hors API

**Critères d'acceptation**

- Tout parcours critique — recherche, réservation, paiement, connexion — est réalisable au
  clavier seul, avec un focus toujours visible.
- Les images porteuses de sens ont une alternative textuelle ; les images décoratives sont
  ignorées des technologies d'assistance.
- La hiérarchie des titres est cohérente et les repères de page sont présents.
- Les messages d'erreur sont associés à leur champ et annoncés.
- Les contrastes respectent le niveau AA, en thème clair **et** en thème sombre.
- Un lien d'évitement mène au contenu principal.

## Performance

### US-E19-07 — Rester rapide sur les parcours d'entrée

**En tant que** visiteur
**je veux** que les pages publiques s'affichent vite
**afin de** ne pas abandonner.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`TableStateUtilities`](../../RestaurantHandler/Utilities/TableStateUtilities.cs), [`HeoHelper`](../../RestaurantHandler/Utilities/HeoHelper.cs)
Pagination serveur et pré-filtrage par boîte englobante conformes. Écarts :
`GET /Product/all` et `GET /Formations/all` renvoient des tables entières ; avatars en WebP
côté API, le reste relève du front.

**Critères d'acceptation**

- Les pages publiques sont mesurées sur les indicateurs web essentiels et un budget de
  performance est défini.
- Les images sont servies dans un format moderne, dimensionnées et chargées paresseusement
  hors du premier écran.
- Les listes sont paginées **côté serveur** ; aucun écran ne charge une table entière.
- La recherche géographique s'appuie sur un pré-filtrage par boîte englobante avant le calcul
  de distance exact.

## Exploitabilité

### US-E19-08 — Diagnostiquer un incident

**En tant qu'** exploitant
**je veux** des journaux structurés et corrélés
**afin de** comprendre ce qui s'est passé.

**Priorité :** Doit

**État :** Absente · **Code :** [`Program`](../../RestaurantHandler/Program.cs)
Journalisation non structurée (`Console.WriteLine` mêlé à `ILogger`), aucune
corrélation, aucun point de santé, aucun format d'erreur normalisé ; des messages
d'exception sont renvoyés au client (`$"Erreur: {ex.Message}"`).

**Critères d'acceptation**

- Les journaux sont structurés et portent un identifiant de corrélation propagé de la requête
  jusqu'aux tâches de fond.
- Toute opération d'argent est journalisée avec son identifiant métier, sa clé d'idempotence
  et son résultat.
- **Aucun secret ni donnée personnelle** n'est journalisé.
- Un point de santé distingue « démarré » de « prêt à servir ».
- Les erreurs sont renvoyées dans un format normalisé, sans trace d'exécution en production.
