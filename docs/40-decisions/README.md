# Décisions (ADR)

Une ADR consigne un arbitrage structurant : contexte, décision, conséquences. Elle n'est jamais
réécrite : une décision remplacée passe au statut *Remplacée par ADR-xxxx*.

Numérotation repartie de 0001 le 2026-11-22 (les ADR du projet d'origine ont été retirées ;
celles encore valides pour le socle ont été reprises et adaptées : 0001 à 0004).

| ADR | Décision | Statut |
|---|---|---|
| [0001](ADR-0001-archi-backend.md) | Contrôleurs + services + EF Core dans un seul projet | Accepté |
| [0002](ADR-0002-auth-cookies-httponly.md) | Session par cookie `httpOnly` | Accepté, à appliquer |
| [0003](ADR-0003-stockage-objet.md) | Un seul stockage objet S3 | Accepté |
| [0004](ADR-0004-cache-memoire.md) | Cache en mémoire, jamais pour l'argent ni les commandes | Accepté |
| [0005](ADR-0005-restaurant-unique.md) | Un seul restaurant, pas de multi-tenant | Accepté |
| [0006](ADR-0006-caisse-certifiee-tierce-v1.md) | Caisse certifiée tierce en V1, caisse maison NF525 en V2 | Accepté |
| [0007](ADR-0007-sumup-psp.md) | SumUp comme prestataire de paiement | Accepté |
| [0008](ADR-0008-centimes-snapshots.md) | Montants en centimes et snapshots dans les commandes | Accepté |
| [0009](ADR-0009-compte-client-obligatoire.md) | Compte client obligatoire pour commander en ligne | Accepté |
| [0010](ADR-0010-paiement-retrait-validation-manager.md) | Paiement au retrait soumis à validation Manager | Accepté |
| [0011](ADR-0011-francais-uniquement.md) | Français uniquement, carte sans traductions | Accepté |
| [0012](ADR-0012-remplacements-prix-ingredient.md) | Remplacements au prix de référence de l'ingrédient, suppléments en groupe d'options | Accepté |

Format : `**Statut**`, `**Date**`, puis *Contexte*, *Décision*, *Conséquences*.
