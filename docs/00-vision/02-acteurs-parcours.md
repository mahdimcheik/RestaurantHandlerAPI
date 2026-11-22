# Acteurs et parcours

## Acteurs

| Acteur | Rôle applicatif | Ce qu'il fait |
|---|---|---|
| **Visiteur** | aucun (anonyme) | Consulte la carte, les horaires et l'état d'ouverture. Ne peut pas commander. |
| **Client** | `Client` | Crée son compte (e-mail ou Google), commande, paie en ligne ou choisit de payer au retrait, suit sa commande, consulte son historique. |
| **Personnel** | `Manager` | Tout le personnel (comptoir, cuisine, gérant) utilise un compte Manager : saisie comptoir, validation des commandes à payer au retrait, KDS, marquage « payée », ruptures, pause des commandes. |
| **Gérant** | `Manager` | Mêmes droits ; gère en plus la carte, les horaires, les comptes et consulte les statistiques. Le compte Manager initial vient de `ADMIN_EMAIL` / `ADMIN_PASSWORD`. |
| **SumUp** | système externe | Encaisse les paiements en ligne et notifie l'API (webhook). |
| **Caisse certifiée tierce** | système externe, hors app | Enregistre légalement toute vente encaissée sur place. |

Il n'y a **pas de distinction de droits** entre équipier et gérant en V1 (décision de
cadrage) : chaque appareil du restaurant (tablette comptoir, écran cuisine) est connecté
avec un compte Manager. Conséquence assumée : un équipier peut techniquement modifier la carte
ou consulter les chiffres. Voir [risques](../50-feuille-de-route/03-risques-dette.md).

## Appareils du restaurant

- **Tablette ou PC comptoir** : back-office Manager (saisie comptoir, validation, remise).
- **Écran cuisine (KDS)** : tablette ou écran fixe, vue KDS de la PWA, en plein écran.
- **Terminal / caisse certifiée tierce** : encaissement légal, indépendant de l'app.
- Pas d'imprimante pilotée par l'app en V1.

## Parcours 1 — Click & collect payé en ligne

1. Le client ouvre la PWA, voit la carte et l'état d'ouverture (« Ouvert — prêt en ~15 min »).
2. Il compose son panier : produits personnalisés (« sans oignons », « cheddar → chèvre »,
   « + bacon ») et menus (choix par emplacement).
3. Il se connecte (ou crée son compte) au moment de valider.
4. Il choisit **dès que possible** ou un **créneau** disponible.
5. Il choisit **payer en ligne** → redirection vers le paiement SumUp.
6. Paiement réussi → la commande passe `Payée` et apparaît **au KDS**.
7. Il suit sa commande en temps réel : *Payée → En préparation → Prête*.
8. Au comptoir, il donne son numéro ; le Manager remet la commande et la passe `Retirée`.

## Parcours 2 — Click & collect payé au retrait

1–4. Comme le parcours 1.
5. Il choisit **payer au retrait**. La commande attend la **validation d'un Manager**.
6. Le Manager accepte (→ cuisine) ou refuse (motif affiché au client).
7. Au retrait, le client paie **sur la caisse certifiée tierce** (CB, espèces, mixte) ; le
   Manager marque la commande « payée » dans l'app puis `Retirée`.

## Parcours 3 — Commande au comptoir

1. Le client commande oralement au comptoir.
2. Le Manager saisit la commande dans le back-office (client de passage, sans compte).
3. L'encaissement se fait sur la caisse certifiée tierce, avant ou après préparation.
4. La commande part en cuisine dès sa validation ; son numéro est annoncé au client.

## Parcours 4 — Cuisine

1. Le KDS affiche les commandes à préparer, triées par heure de retrait prévue.
2. Le cuisinier passe une commande *En préparation* puis *Prête* d'un geste.
3. Le client (en ligne) voit le changement en temps réel ; au comptoir, le numéro est appelé.

## Parcours 5 — Gestion courante (Manager)

- Rupture : « plus de bacon » → l'ingrédient devient indisponible ; les produits et
  suppléments concernés disparaissent ou sont grisés immédiatement côté client.
- Coup de feu : **pause des commandes en ligne** en un clic, reprise en un clic.
- Fermeture exceptionnelle : saisie à l'avance (congés, jour férié).
- Fin de journée : consultation des statistiques du jour.
