# Rôles et permissions

Deux rôles : `Client` (défaut, seul rôle attribuable publiquement) et `Manager` (tout le
personnel). L'autorisation est déclarée **sur l'opération**, avec **refus par défaut**
(politique de repli exigeant une session ; cf. [ADR-0001](../40-decisions/ADR-0001-archi-backend.md)).

| Fonction                                                      | Anonyme |      Client      |   Manager   |
| ------------------------------------------------------------- | :-----: | :--------------: | :---------: |
| Consulter carte, horaires, état d'ouverture, créneaux         |   ✅    |        ✅        |     ✅      |
| S'inscrire, se connecter, réinitialiser son mot de passe      |   ✅    |        —         |      —      |
| Gérer son profil, supprimer son compte                        |    —    |        ✅        |     ✅      |
| Valider une commande en ligne                                 |    —    |        ✅        |      —      |
| Payer en ligne, annuler sa commande (avant préparation)       |    —    | ✅ (les siennes) |      —      |
| Suivre une commande, voir son historique et ses reçus         |    —    | ✅ (les siennes) | ✅ (toutes) |
| Saisir une commande comptoir                                  |    —    |        —         |     ✅      |
| Accepter / refuser une commande à payer au retrait            |    —    |        —         |     ✅      |
| Faire avancer une commande (KDS), remettre, déclarer non venu |    —    |        —         |     ✅      |
| Marquer payée sur place, rembourser, annuler côté restaurant  |    —    |        —         |     ✅      |
| Gérer la carte, les ruptures, les horaires, la pause          |    —    |        —         |     ✅      |
| Paramétrer Greenadine                                         |    —    |        —         |     ✅      |
| Consulter les statistiques                                    |    —    |        —         |     ✅      |
| Gérer les comptes, créer un Manager, bannir                   |    —    |        —         |     ✅      |
| Tableau de bord Hangfire                                      |    —    |        —         |     ✅      |

**Propriété** : une commande n'est lisible par un Client que s'il en est le `CustomerId` ;
toute autre tentative renvoie **404** (pas 403, pour ne pas révéler l'existence).

**Temps réel** : un Client ne reçoit que les événements qui lui sont adressés ; le groupe
`restaurant` (KDS, back-office) est attribué à la connexion aux seuls Managers.

**Limite assumée en V1** : pas de rôle équipier — un appareil de cuisine a les mêmes droits que
le gérant. Voir [risques](../50-feuille-de-route/03-risques-dette.md).
