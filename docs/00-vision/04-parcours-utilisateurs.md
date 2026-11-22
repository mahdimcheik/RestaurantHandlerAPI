# Parcours utilisateurs

> Statut : **Stable** — vue d'ensemble bout-en-bout. Le détail est dans les
> [user stories](../10-user-stories/).

## 1. Le parcours nominal, des deux côtés

```mermaid
sequenceDiagram
    actor E as Élève
    participant P as Plateforme
    participant S as Stripe
    actor T as Professeur

    T->>P: publie créneaux et produits
    E->>P: recherche géolocalisée
    E->>P: réserve un créneau
    P->>P: crée/alimente la commande du professeur
    Note over P: délai de paiement : 30 min
    E->>S: paie la commande
    S-->>P: webhook « paiement confirmé »
    P->>P: commande PAID, réservations ACCEPTED
    P->>P: crédite le portefeuille du professeur (95 %)
    Note over P,T: le crédit est acquis mais pas versé
    E-->>T: le cours a lieu
    Note over P: fin du créneau + fenêtre de litige (48 h)
    P->>S: transfert vers le compte du professeur
    P->>P: réservation DONE
    E->>P: laisse un avis
```

## 2. Élève — de la recherche à l'avis

```mermaid
flowchart TD
    A[Arrivée sur le site] --> B[Recherche : matière, ville, rayon, disponibilité]
    B --> C[Profil du professeur]
    C -->|hésite| D[Ajoute aux favoris]
    C -->|question| E[Formulaire de contact]
    C --> F[Choisit un créneau]
    F --> G{Professeur vérifié<br/>et onboardé Stripe ?}
    G -->|oui| H[Commande à payer sous 30 min]
    G -->|non| I[Demande envoyée<br/>réponse du professeur sous 48 h]
    H -->|paie| J[Réservation confirmée]
    H -->|délai dépassé| K[Commande annulée, créneau libéré]
    I -->|accepte| J
    I -->|refuse ou silence| K
    J --> L[Messagerie avec le professeur]
    J --> M{Le cours a-t-il eu lieu ?}
    M -->|oui| N[Avis et note]
    M -->|annulation avant| O[Remboursement selon la politique]
    M -->|professeur absent| P[Ouverture d'un litige]
```

Points de friction à surveiller : le **délai de 30 minutes** (tout abandon du tunnel libère
le créneau) et le **flux sans paiement**, qui allonge la boucle à 48 heures.

## 3. Professeur — de l'inscription au premier versement

```mermaid
flowchart TD
    A[Inscription en tant que professeur] --> B[Confirmation de l'e-mail]
    B --> C[Dépôt des pièces justificatives]
    C --> D{Revue par un administrateur}
    D -->|refus motivé| C
    D -->|toutes les pièces validées| E[Compte vérifié]
    A --> F[Optionnel : passage professionnel<br/>SIRET vérifié auprès de l'INSEE]
    E --> G[Onboarding Stripe Connect Express]
    G --> H{Dossier soumis<br/>et versements autorisés ?}
    H -->|non| G
    H -->|oui| I[Confirmé Stripe]
    I --> J[Les réservations déclenchent un paiement]
    E --> K[Publication des produits et des créneaux]
    J --> L[Crédit au portefeuille à chaque paiement]
    L --> M[Cours donné]
    M --> N[Fin du créneau + 48 h sans litige]
    N --> O[Versement automatique, dette déduite]
```

**Le sas d'entrée est double** et c'est le point le plus coûteux du parcours : un professeur
vérifié mais non onboardé accumule un solde qu'il ne touche pas ; un professeur onboardé
mais non vérifié reste en flux sans paiement.

## 4. Administrateur — la boucle d'exploitation

```mermaid
flowchart LR
    subgraph Quotidien
        A[File des documents à revoir] --> B[Valider ou refuser avec motif]
        B --> C[Demander une pièce manquante]
        D[File des litiges] --> E[Arbitrer]
        F[Demandes de contact] --> G[Répondre]
    end
    subgraph Périodique
        H[Bilan mensuel] --> I[Export CSV]
        J[Référentiels : catégories, niveaux, types de pièces]
    end
```

L'objectif de conception est que **99 % des cas ne remontent jamais** à cet écran : les
versements partent tout seuls, les litiges se règlent en majorité par l'acceptation du
professeur.

## 5. Les trois chemins de sortie de l'argent

C'est le schéma à garder en tête ; le détail est dans
[flux d'argent](../20-specs-fonctionnelles/04-flux-argent.md).

```mermaid
flowchart TD
    A[Élève paie la commande] --> B[Crédit au portefeuille du professeur<br/>montant × 95 %]
    B --> C{Que se passe-t-il ensuite ?}
    C -->|rien, 48 h passent| D[Versement au professeur]
    C -->|annulation avant le cours| E[Remboursement de l'élève<br/>crédit annulé, jamais versé]
    C -->|litige ouvert| F[Crédit gelé]
    F -->|professeur accepte| E
    F -->|arbitrage pro professeur| D
    F -->|arbitrage pro élève| E
    D -->|remboursement ultérieur| G[CLAWBACK : dette recouvrée<br/>sur les versements suivants]
```

**L'asymétrie est voulue :** l'inaction mène au versement, jamais au blocage.
