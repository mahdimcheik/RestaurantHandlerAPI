# Risques et dette

## Risques

| # | Risque | Probabilité | Impact | Parade |
|---|---|---|---|---|
| R1 | **Délai** : 3 mois en solo pour back + front + mise en service | élevée | ouverture repoussée | jalons à critère de sortie ; V2 assumée ; ouverture progressive (comptoir + KDS avant click & collect) ; couper US-E09-3 et les créneaux programmés en dernier recours |
| R2 | **Légal caisse** : la caisse tierce + paiements en ligne hors caisse ne suffit pas | moyenne | amende, remise en cause du périmètre | jalon G0 avec l'expert-comptable avant la fin de J2 |
| R3 | **Allergènes en ligne** non intégrés | moyenne | non-conformité INCO | document lié depuis la carte dès J4 ; intégration V2 avancée si G0 l'exige |
| R4 | **Double saisie** comptoir (app + caisse tierce) ralentit le service | élevée | files, erreurs, abandon de l'app au comptoir | saisie comptoir ultra rapide ; recette terrain ; V2 caisse maison en priorité 1 |
| R5 | **SumUp** : API en ligne moins documentée / notifications différentes de l'attendu | moyenne | retard J3 | prototype sandbox dès J1 (½ journée) pour lever l'inconnue tôt |
| R6 | **PWA sur iOS** : limites (push seulement si installée, mise en veille des connexions temps réel) | élevée | suivi client figé | resynchronisation REST à chaque retour au premier plan ; push en V2 |
| R7 | **Temps réel** coupé sur le KDS | moyenne | commande oubliée | bandeau de déconnexion, resynchronisation complète, son à chaque nouvelle commande |
| R8 | **Appareils partagés** avec droits Manager complets | moyenne | modification accidentelle de la carte, accès aux chiffres | sessions révocables ; rôle équipier en V2 |
| R9 | **Paiement au retrait** non honoré | moyenne | perte de marchandise | validation Manager, une commande en cours, blocage après non-venues |
| R10 | **Pic de charge** à midi | faible | lenteurs | une instance suffit ; cache carte ; test de charge J6 |
| R11 | **Panne internet** au restaurant | moyenne | service interrompu | 4G de secours, procédure papier, caisse tierce |
| R12 | **Absence de tests** au départ | certaine | régressions sur l'argent | tests unitaires des règles avant les services (J2), intégration en CI |

## Dette héritée du socle

| # | Dette | Action | Jalon |
|---|---|---|---|
| D1 | Workflows CI/CD poussent l'image Docker `…/bonprof` | renommer l'image | J0 |
| D2 | `ADDRESS_ENCRYPTION_KEY` encore référencée dans le code | retirer | J0 |
| D3 | SixLabors.ImageSharp 3.1.x : vulnérabilités connues | mettre à jour | J0 |
| D4 | `bin/Debug/net8.0` résiduel | supprimer (IDE fermé) | J0 |
| D5 | Centre d'aide, envois groupés, maintenance : hors périmètre | retirer code, seed, configuration | J0 |
| D6 | Hub SignalR nommé `ChatHub` (`/chathub`) | renommer, définir les groupes | J0 |
| D7 | JWT d'accès lisible par le script (ADR-0002 non appliquée) | bascule cookie `httpOnly` | J4 (question 13) |
| D8 | Aucune suite de tests | recréer projet xUnit + CI | J0 |
| D9 | `BonProf.sln.DotSettings.user` à la racine | supprimer | J0 |
| D10 | Filtre d'archivage du catalogue retiré à la création du modèle | trancher (M9) | J1 |
