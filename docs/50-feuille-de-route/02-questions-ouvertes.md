# Questions ouvertes

Chaque question a un **interlocuteur** et une **échéance** (le jalon qu'elle bloque). Une
question tranchée est déplacée en bas avec sa réponse et, si structurante, une ADR.

| # | Question | Interlocuteur | Bloque |
|---|---|---|---|
| 1 | **Caisse certifiée tierce** : SumUp POS est-il retenu ? Le restaurant a-t-il déjà une caisse ? Faut-il rapprocher chaque jour ses ventes avec les commandes de l'application, et comment ? | gérant | G0 (fin J2) |
| 2 | **Ventes en ligne SumUp** : doivent-elles aussi être enregistrées dans la caisse certifiée ? Quel justificatif délivrer (reçu, facture) et avec quelles mentions ? | expert-comptable | G0 |
| 3 | **TVA** : taux de chaque produit à emporter (5,5 % / 10 % / 20 %) ; ventilation d'un menu qui mêle des taux différents (taux unique ou ventilation par composant) ? | expert-comptable | G0 |
| 4 | **Machine à états** : la proposition (`Confirmée` remplace `Payée`, ajout de `AValider`, `Refusée`, `NonVenu`) convient-elle ? Délai de refus automatique d'une commande `AValider` ? | gérant + dev | début J2 |
| 6 | **Annulation / remboursement** : jusqu'à quand le client annule-t-il seul ? Remboursement en cas de non-venue d'une commande payée en ligne ? À inscrire dans les CGV. | gérant | J3 |
| 8 | **Allergènes en ligne** : un document lié depuis la carte suffit-il pour l'ouverture, ou faut-il les intégrer dès la V1 ? | expert-comptable / DGCCRF | G0 |
| 9 | **Mode dégradé** : contenu exact de la procédure papier, matériel de secours 4G, la caisse tierce fonctionne-t-elle hors ligne ? | gérant | J6 |
| 11 | **Numéro affiché** : séquence globale complète ou numéro court remis à zéro chaque jour (plus lisible pour l'appel) ? | gérant | J2 |
| 13 | **Session** : bascule complète en cookie `httpOnly` (ADR-0002) avant l'ouverture, ou après ? | dev | J4 |
| 14 | **Nom de domaine** et identité visuelle (logo, couleurs) de la PWA. | gérant | J4 |
| 15 | **Seuils** : nombre de non-venues avant blocage, délai de grâce au retrait, durée d'expiration d'un paiement en ligne. | gérant | J2 |

## Tranchées

| Question | Réponse | Date | Trace |
|---|---|---|---|
| Un ou plusieurs restaurants ? | Un seul | 2026-11-22 | [ADR-0005](../40-decisions/ADR-0005-restaurant-unique.md) |
| Type de restauration | Fast-food / snack à emporter | 2026-11-22 | [vision](../00-vision/01-vision-objectifs.md) |
| Canaux V1 | Click & collect + comptoir | 2026-11-22 | vision |
| Rôles | Client + Manager, pas de rôle équipier en V1 | 2026-11-22 | [permissions](../20-regles-metier/03-roles-permissions.md) |
| Compte pour commander | Obligatoire, Google conservé | 2026-11-22 | [ADR-0009](../40-decisions/ADR-0009-compte-client-obligatoire.md) |
| Paiement | En ligne SumUp ou au retrait après validation Manager | 2026-11-22 | [ADR-0007](../40-decisions/ADR-0007-sumup-psp.md), [ADR-0010](../40-decisions/ADR-0010-paiement-retrait-validation-manager.md) |
| Caisse légale V1 | Caisse certifiée tierce ; caisse maison en V2 | 2026-11-22 | [ADR-0006](../40-decisions/ADR-0006-caisse-certifiee-tierce-v1.md) |
| Moyens sur place | CB terminal, espèces, mixte (via la caisse tierce) | 2026-11-22 | E05 |
| Retrait | ASAP et créneau | 2026-11-22 | E03, E04 |
| Matériel | KDS ; pas d'imprimante en V1 | 2026-11-22 | E07 |
| Notification client | Suivi temps réel dans la PWA uniquement | 2026-11-22 | E08 |
| Front | PWA Angular mobile-first | 2026-11-22 | [architecture](../30-technique/01-architecture.md) |
| Langue | Français uniquement | 2026-11-22 | [ADR-0011](../40-decisions/ADR-0011-francais-uniquement.md) |
| Hébergement | VPS + Docker comme le projet d'origine | 2026-11-22 | [exploitation](../30-technique/03-exploitation.md) |
| Panne internet | Mode dégradé documenté, pas de hors-ligne | 2026-11-22 | exploitation |
| Socle conservé | Auth (mot de passe + Google), profil ; retrait du centre d'aide, des envois groupés, de la maintenance | 2026-11-22 | feuille de route J0 |
| Web Push (ex-question 7) | Gardé dormant, branché en V2 | 2026-11-22 | [architecture](../30-technique/01-architecture.md) |
| Date de naissance (ex-question 12) | Supprimée ; le profil incomplet = téléphone manquant | 2026-11-22 | [E01](../10-cahier-des-charges/E01-comptes-authentification.md) |
| Créneaux (ex-question 5) | Capacité en nombre de commandes, réservation le jour même uniquement ; durée, capacité et délai minimal paramétrables | 2026-11-22 | [E03](../10-cahier-des-charges/E03-ouverture-ruptures.md) |
| Ingrédient retirable en rupture (ex-question 10) | Produit commandable, retrait imposé et affiché | 2026-11-22 | [E03](../10-cahier-des-charges/E03-ouverture-ruptures.md) |
| Nom de l'image Docker | `mahdimcheik/restauranthandler` | 2026-11-22 | [exploitation](../30-technique/03-exploitation.md) |
