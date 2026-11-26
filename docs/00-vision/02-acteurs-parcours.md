# Acteurs et parcours

## Acteurs

| Acteur | Rôle applicatif | Ce qu'il fait |
|---|---|---|
| **Visiteur** | aucun (anonyme) | Consulte la carte, les horaires et l'état d'ouverture. Ne peut pas commander. |
| **Client** | `Client` | Crée son compte (e-mail ou Google), commande, paie par carte en ligne, suit sa commande, consulte son historique. Pour annuler, il appelle le restaurant ou le demande sur place. |
| **Personnel** | `Manager` | Tout le personnel (remise, cuisine, gérant) utilise un compte Manager : KDS, remise des commandes, annulation à la demande d'un client, ruptures, pause des commandes. |
| **Gérant** | `Manager` | Mêmes droits ; gère en plus la carte, les horaires, les comptes et consulte les statistiques. Le compte Manager initial vient de `ADMIN_EMAIL` / `ADMIN_PASSWORD`. |
| **SumUp** | système externe | Encaisse les paiements en ligne et notifie l'API (webhook). |
| **Caisse du restaurant** | système externe, hors app | Enregistre les éventuelles ventes faites au comptoir, sans lien avec l'app. |

Il n'y a **pas de distinction de droits** entre équipier et gérant en V1 (décision de
cadrage) : chaque appareil du restaurant (tablette de service, écran cuisine) est connecté
avec un compte Manager. Conséquence assumée : un équipier peut techniquement modifier la carte
ou consulter les chiffres. Voir [risques](../50-feuille-de-route/03-risques-dette.md).

## Appareils du restaurant

- **Tablette ou PC de service** : back-office Manager (commandes du jour, remise, annulation).
- **Écran cuisine (KDS)** : tablette ou écran fixe, vue KDS de la PWA, en plein écran.
- **Caisse du restaurant** : ventes au comptoir éventuelles, indépendante de l'app.
- Pas d'imprimante pilotée par l'app en V1.

## Parcours 1 — Click & collect payé en ligne

1. Le client ouvre la PWA, voit la carte et l'état d'ouverture (« Ouvert — prêt en ~15 min »).
2. Il compose son panier : produits personnalisés (« sans oignons », « cheddar → chèvre »,
   « + bacon ») et menus (choix par emplacement).
3. Il se connecte (ou crée son compte) au moment de valider.
4. Il choisit **dès que possible** ou un **créneau** disponible.
5. Il paie **par carte en ligne** (seul moyen) → redirection vers le paiement SumUp.
6. Paiement réussi → la commande passe `Confirmée` et apparaît **au KDS**.
7. Il suit sa commande en temps réel : *Confirmée → En préparation → Prête*. Pour annuler, la
   page l'invite à appeler le restaurant ou à le demander sur place ; un Manager annule et la
   carte est remboursée.
8. Au comptoir, il donne son numéro ; le Manager remet la commande et la passe `Retirée`.

## Parcours 2 et 3 — supprimés

Le paiement au retrait (parcours 2) et la commande au comptoir (parcours 3) ont été retirés le
2026-11-26 ([ADR-0013](../40-decisions/ADR-0013-paiement-en-ligne-seul.md)).

## Parcours 4 — Cuisine

1. Le KDS affiche les commandes à préparer, triées par heure de retrait prévue.
2. Le cuisinier passe une commande *En préparation* puis *Prête* d'un geste.
3. Le client voit le changement en temps réel ; au comptoir, le numéro est appelé.

## Parcours 5 — Gestion courante (Manager)

- Rupture : « plus de bacon » → l'ingrédient devient indisponible ; les produits et
  suppléments concernés disparaissent ou sont grisés immédiatement côté client.
- Coup de feu : **pause des commandes en ligne** en un clic, reprise en un clic.
- Fermeture exceptionnelle : saisie à l'avance (congés, jour férié).
- Fin de journée : consultation des statistiques du jour.
