# E16 — Facturation & comptabilité

> Statut : **Stable**
>
> Le **vendeur légal du cours est le professeur** ; la plateforme émet la facture en son nom
> (mandat de facturation) et facture séparément sa commission.

## US-E16-01 — Télécharger la facture d'une commande

**En tant qu'** élève
**je veux** obtenir la facture PDF d'une commande payée
**afin de** justifier ma dépense.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`FacturationService`](../../RestaurantHandler/Services/FacturationService.cs), [`InvoiceDocument`](../../RestaurantHandler/Utilities/InvoiceDocument.cs)
`GET /Order/student/orders/{orderId}/invoice` : commande payée exigée, facture unique par
commande (`Document.OrderId`), HT et TVA dérivés. Écarts : une commande **partiellement
remboursée** n'est plus « payée » et n'a donc plus de facture ; pas d'accès administrateur.
Fichier privé servi par lien signé depuis le lot 5.

**Critères d'acceptation**

- La facture n'existe que pour une commande **payée**.
- Elle est générée à la première demande puis **réutilisée** : une seule facture par
  commande, garantie côté stockage.
- Elle porte : numéro de commande, date, identité du professeur émetteur, identité et adresse
  de facturation de l'élève, une ligne par réservation, montant HT, TVA et TTC, et la mention
  du mandat de facturation.
- Les prix étant TTC, la base HT et la TVA en sont **dérivées**.
- Le fichier est privé et servi via un lien signé à durée limitée.

## US-E16-02 — Consulter la comptabilité d'une commande

**En tant qu'** administrateur
**je veux** voir la décomposition financière d'une commande
**afin de** vérifier où va chaque euro.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`OrderAccountingService`](../../RestaurantHandler/Services/Orders/OrderAccountingService.cs), [`OrderAccountingDTO`](../../RestaurantHandler/Contracts/Order/OrderAccountingDTO.cs)
`GET /Order/admin/orders/{orderId}/accounting` (`OrderAccounting`).

**Critères d'acceptation**

- L'écran affiche : brut encaissé, frais du prestataire, net encaissé, montant remboursé, dû
  au professeur, déjà versé au professeur, total professeur, et gain de la plateforme.
- Une ligne par réservation détaille montant, taux de commission, part professeur, remboursé
  et versé.
- Tant que les frais du prestataire ne sont pas connus, le gain est affiché comme
  **provisoire** et l'écran le signale explicitement.
- Aucune valeur n'est stockée en double : tout est dérivé des écritures.

## US-E16-03 — Consulter mon bilan mensuel

**En tant que** professeur
**je veux** un récapitulatif de mon activité sur un mois
**afin de** préparer ma comptabilité.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`MonthlyReportService`](../../RestaurantHandler/Services/Orders/MonthlyReportService.cs), [`MonthRange`](../../RestaurantHandler/Domain/Policies/MonthRange.cs)
`POST /Order/teacher/monthly-report`.

**Critères d'acceptation**

- Je choisis un mois ; la liste des commandes du mois s'affiche, paginée, triable et
  filtrable.
- Un bloc de totaux récapitule le mois.
- La vue professeur **masque** les frais du prestataire, le net encaissé et le gain de la
  plateforme : ce ne sont pas ses affaires.
- Les bornes du mois sont calculées en heure locale de la plateforme, pas en heure du
  navigateur, afin que deux utilisateurs voient le même mois.

## US-E16-04 — Consulter le bilan mensuel global

**En tant qu'** administrateur
**je veux** le bilan mensuel complet de la plateforme
**afin de** suivre l'activité et les revenus.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`MonthlyReportService`](../../RestaurantHandler/Services/Orders/MonthlyReportService.cs)
`POST /Order/admin/monthly-report`.

**Critères d'acceptation**

- Mêmes écrans que la vue professeur, enrichis des colonnes élève et professeur et des
  montants réservés à la plateforme.
- Les totaux couvrent brut, frais, net, remboursements, part professeur et gain plateforme.

## US-E16-05 — Exporter un bilan

**En tant que** professeur ou administrateur
**je veux** exporter mon bilan mensuel
**afin de** le transmettre à mon comptable.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`MonthlyReportService`](../../RestaurantHandler/Services/Orders/MonthlyReportService.cs), [`Csv`](../../RestaurantHandler/Utilities/Csv.cs)
`POST /Order/{admin,teacher}/monthly-report/export`, mêmes filtres que la liste.

**Critères d'acceptation**

- L'export produit un fichier CSV **lisible par un tableur français** : séparateur
  point-virgule, virgule décimale, encodage avec marque d'ordre des octets.
- L'export applique **les mêmes filtres** que la liste affichée.
- Le nom du fichier contient la période et la portée.
- L'export professeur expose exactement les mêmes colonnes que sa vue à l'écran, sans les
  montants réservés à la plateforme.

## US-E16-06 — Retrouver mes commandes

**En tant qu'** élève, professeur ou administrateur
**je veux** parcourir les commandes qui me concernent
**afin de** retrouver un paiement.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`OrderService`](../../RestaurantHandler/Services/Orders/OrderService.cs)
Listes par rôle ; `GET /Order/orders/{orderId}` vérifie que l'appelant est l'élève, le
professeur ou un administrateur.

**Critères d'acceptation**

- Liste paginée, triable et filtrable par numéro, statut, date et montant.
- L'élève voit ses achats, le professeur ses ventes, l'administrateur tout.
- Le détail affiche les réservations, l'encaissement, les remboursements et l'accès à la
  facture.
- L'accès à une commande dont je ne suis pas partie prenante est refusé.
