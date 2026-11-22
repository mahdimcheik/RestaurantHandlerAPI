# Glossaire

> Statut : **Stable** — vocabulaire de référence. Les noms techniques (en `code`) sont les
> noms réels des entités et ne sont jamais traduits.

## Acteurs

| Terme | `Code` | Définition |
|---|---|---|
| Élève | `Student` | Acheteur. Réserve et paie des cours. Profil 1:1 avec le compte utilisateur. |
| Professeur | `Teacher` | Vendeur. Publie des créneaux et des produits, reçoit les versements. Profil 1:1 avec le compte utilisateur. |
| Administrateur | `Admin` | Exploite la plateforme : vérification, arbitrage, référentiels. |
| Super-administrateur | `SuperAdmin` | Administrateur des administrateurs et des paramètres sensibles. |
| Éditeur | `Editor` | Professeur autorisé par l'administration à rédiger et publier ses propres articles. Rôle cumulé avec `Teacher`. |
| Plateforme | — | L'intermédiaire. Encaisse, garde la commission, reverse. |
| Compte utilisateur | `UserApp` | Identité unique. Porte les rôles, les consentements, l'avatar, les adresses. |

## Offre

| Terme | `Code` | Définition |
|---|---|---|
| Produit | `Product` | Un cours vendable : intitulé, description, prix horaire, niveau, catégories. Appartient à un professeur. Sert aussi de vitrine sur le profil. |
| Niveau | `LevelCursus` | Débutant · Intermédiaire · Avancé · Tous. Référentiel administrable. |
| Catégorie | `CategoryCursus` | Matière/domaine du produit. Référentiel administrable, ouvert à l'exécution. |
| Créneau | `Slot` | Une plage horaire proposée par un professeur (`DateFrom` → `DateTo`). Porte l'identité du professeur : c'est de là que la réservation tire son vendeur. |
| Type de créneau | `TypeSlot` | Présentiel · Visio · Indifférent. |
| Formation | `Formation` | Diplôme ou cursus suivi, affiché sur le profil. |
| Expérience | `Experience` | Expérience professionnelle, affichée sur le profil. |

## Transaction

| Terme | `Code` | Définition |
|---|---|---|
| Réservation | `Reservation` | Le cœur du domaine : un élève réserve un créneau pour un produit. Fige le prix unitaire, le montant et le taux de commission au moment de la réservation — ces valeurs ne sont **jamais** recalculées. |
| Commande | `Order` | Le panier. Regroupe une ou plusieurs réservations **d'un même professeur**. Une commande = un professeur = un paiement. |
| Encaissement | `Payment` | L'argent reçu de l'élève. Relation 1:1 avec la commande. |
| Remboursement | `Refund` | Une ligne par réservation remboursée. Ajouté par-dessus la commande, qui reste immuable une fois payée. |
| Facture | `Document` (lié à `OrderId`) | PDF émis au nom du professeur (mandat de facturation). Une seule par commande. |
| Comptabilité de commande | `OrderAccounting` | Vue dérivée : brut encaissé, frais Stripe, net, remboursé, dû au professeur, versé, gain plateforme. |

## Argent côté professeur

| Terme | `Code` | Définition |
|---|---|---|
| Portefeuille | — | Vue agrégée des lignes de transaction d'un professeur. N'est pas une entité : c'est une somme. |
| Ligne de portefeuille | `TeacherWalletTransaction` | Écriture signée au grand livre. Type `PAYMENT`, `REFUND`, `PAYOUT`, `PENALTY` ou `CLAWBACK`. |
| Solde disponible | — | `SUM(Amount)` des lignes non versées (`PayoutId IS NULL`) et non archivées (`ArchivedAt IS NULL`). Peut être **négatif**. |
| Versement | `TeacherPayout` | Un virement sortant vers le compte Stripe du professeur. **Un versement = une réservation.** |
| Dette | — | Somme des lignes ouvertes de type `PENALTY` ou `CLAWBACK`. C'est le **type**, pas le signe, qui fait la dette. |
| Pénalité | `PENALTY` | Dette née d'une annulation par le professeur d'un cours payé (5 % du montant). |
| Récupération | `CLAWBACK` | Dette née d'un remboursement émis **après** que le professeur a été versé. |
| Compensation | — | Mécanisme de recouvrement : la dette ouverte est déduite des versements suivants, du plus ancien au plus récent. |

## Confiance

| Terme | `Code` | Définition |
|---|---|---|
| Code de présence | `Reservation.ValidationCode` | Code à usage unique permettant de marquer le cours comme réalisé **plus tôt**. Jamais une condition de paiement. |
| Fenêtre de litige | `DISPUTE_WINDOW` | Délai après la fin du créneau pendant lequel l'élève peut contester — et avant lequel le professeur n'est pas versé. Une seule constante pour les deux, de sorte qu'un litige ne puisse jamais survenir après un versement. |
| Litige | `Dispute` | Contestation ouverte par l'élève. Gèle le crédit du professeur jusqu'à résolution. |
| Issue de litige | `DisputeOutcome` | Professeur · Élève · Partielle. |
| Vérification | `Teacher.IsVerified` | État recalculé : toutes les pièces requises ont été validées par un administrateur. |
| Certification | — | Le processus complet de vérification documentaire d'un professeur. |
| Confirmation Stripe | `Teacher.IsStripeConfirmed` | Le compte Stripe Connect du professeur est opérationnel (dossier soumis **et** versements autorisés). |
| Document de confidentialité | `PrivacyDocument` | Pièce justificative déposée par un professeur, soumise à revue. |
| Demande de document | `TeacherDocumentRequest` | Pièce attendue d'un professeur donné, générée automatiquement ou demandée par un administrateur. |

## Suivi de cours

| Terme | `Code` | Définition |
|---|---|---|
| Tableau de suivi | `Board` | Espace de suivi qu'un professeur ouvre pour **un** élève avec qui il partage une réservation. Un professeur peut en ouvrir plusieurs par élève ; l'élève le consulte en lecture seule. |
| Liste | `BoardList` | Colonne ordonnée d'un tableau. Trois sont créées avec lui — *À faire*, *En cours*, *Fait* — sans statut particulier ensuite. |
| Carte | `BoardCard` | Élément de travail posé dans une liste : titre, importance, description enrichie, et en option un produit et une réservation du couple professeur-élève. |
| Importance | `CardImportance` | Basse · Normale · Haute · Urgente. Énumération à ajout seul. |

## Technique transverse

| Terme | `Code` | Définition |
|---|---|---|
| Archivage | `ArchivedAt` | Suppression logique. Toute somme et toute requête doit exclure les lignes archivées. |
| Référentiel traduisible | `TranslatableModelOption` | Liste administrable dont les libellés sont stockés en JSON multilingue, avec le français comme repli canonique. |
| État de tableau | `CustomTableState` | Contrat unique de pagination / tri / filtre partagé par tous les écrans de liste. |
| Clé d'idempotence | — | Identifiant stable dérivé de données métier (`checkout_{orderId}_{attempt}`, `payout_{reservationId}`) qui empêche Stripe d'exécuter deux fois la même opération. |
| Flux sans paiement | `Order.PaymentRequired = false` | Parcours de réservation d'un professeur non encore vérifié/onboardé : aucun argent ne circule, le professeur accepte ou refuse manuellement sous 48 h. |

## Faux amis

- **« Panier » ≠ panier classique.** Il y en a autant que de professeurs chez qui l'élève
  réserve, tous actifs en même temps.
- **« Solde » ≠ ce que le professeur va recevoir.** La dette et les crédits gelés par un
  litige s'en déduisent.
- **« Remboursement » ≠ dette du professeur.** Une ligne `REFUND` annule une ligne
  `PAYMENT` de la même réservation : les deux se neutralisent, rien n'est dû.
- **« Vérifié » ≠ « confirmé Stripe ».** Le premier est documentaire, le second est
  bancaire. Il faut **les deux** pour qu'une réservation déclenche un paiement.
