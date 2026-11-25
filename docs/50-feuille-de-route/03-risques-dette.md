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
| R12 | **Couverture de tests** encore minimale (projet créé en J0) | certaine | régressions sur l'argent | tests unitaires des règles avant les services (J2), intégration en CI |
| R13 | **Licence ImageSharp** : à partir de 3.2 / 4.x, le build Release échoue sans clé Six Labors (Split License, gratuite sous un seuil de chiffre d'affaires). Le projet reste donc en **3.1.12**, qui a des failles connues (décodeurs TIFF, égalisation d'histogramme) | moyenne | failles non corrigées en amont | décodage limité à JPEG/PNG/GIF/WebP (`Utilities/ImageDecoding.cs`, testé) : les décodeurs vulnérables sont inatteignables. Passer en 3.2+ / 4.x avec une clé (secret de build) si l'éligibilité est confirmée, sinon remplacer la bibliothèque |

## Dette héritée du socle

| # | Dette | Action | Jalon |
|---|---|---|---|
| D7 | JWT d'accès lisible par le script (ADR-0002 non appliquée) | bascule cookie `httpOnly` | J4 (question 13) |
| D10 | Filtre d'archivage du catalogue retiré à la création du modèle | trancher (M9) | J1 |
| D11 | Logo des e-mails (`Templates/Assets/logo.png`) et couleurs : charte d'origine ; nom affiché = `HardCode.APP_NAME` provisoire | remplacer par l'identité du restaurant | J4 (question 14) |
| D12 | Dossier Rider `.idea/.idea.BonProfAPI/` versionné sous l'ancien nom | supprimer ou renommer | à l'occasion |

**Soldées en J0 (2026-11-22)** : image Docker renommée (D1), clé de chiffrement des adresses
et utilitaires morts retirés (D2), ImageSharp déjà en 4.1.3 sans vulnérabilité connue (D3 ; ramené en 3.1.12 le 2026-11-25, cf. R13),
`bin/Debug/net8.0` absent (D4), centre d'aide / envois groupés / maintenance retirés (D5), hub
renommé `RealtimeHub` avec groupes (D6), projet de tests + CI (D8), fichier
`BonProf.sln.DotSettings.user` supprimé (D9) ; paquets inutilisés retirés (Bogus,
HtmlSanitizer, ModelContextProtocol).

**Soldée le 2026-11-25** : workflow et compose du serveur de test BonProf supprimés, dépôts sur
GitHub, déploiement sur Acer documenté dans l'[exploitation](../30-technique/03-exploitation.md) (D13).
