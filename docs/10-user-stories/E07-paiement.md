# E07 — Paiement

> Statut : **Stable** · Flux détaillé : [flux d'argent](../20-specs-fonctionnelles/04-flux-argent.md)
>
> La plateforme encaisse pour son propre compte puis reverse le professeur : c'est un modèle
> d'encaissement séparé, pas un paiement direct de l'élève au professeur.

## US-E07-01 — Payer une commande par carte

**En tant qu'** élève
**je veux** payer une commande en attente
**afin de** confirmer mes réservations.

**Priorité :** Doit · **Dépend de :** US-E06-02

**État :** Partielle · **Code :** [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs), [`PaymentController`](../../RestaurantHandler/Controllers/PaymentController.cs)
`POST /Payment/checkout` : une ligne par réservation, échéance commune, refus si la
commande n'est pas en attente. **Écart :** point d'accès anonyme, la commande est désignée
par son seul identifiant, sans contrôle de propriété.

**Critères d'acceptation**

- Le paiement porte sur **une commande**, donc un seul professeur, avec **une ligne par
  réservation** afin que le détail soit lisible sur le relevé et le reçu.
- Je suis redirigé vers une page de paiement hébergée ; la plateforme ne manipule aucune
  donnée de carte.
- L'échéance de la commande et celle de la session de paiement sont **la même valeur**.
- L'appel est refusé si la commande est déjà payée, ou si elle relève du flux sans paiement.
- L'appel exige d'être **authentifié** et de posséder la commande.

## US-E07-02 — Ne jamais être débité deux fois

**En tant qu'** élève
**je veux** qu'un double clic ou un rechargement ne crée pas deux paiements
**afin de** ne pas devoir demander un remboursement.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs)
`Order.CheckoutAttempt` (`[ConcurrencyCheck]`), relecture et expiration de la session
existante, clé `checkout_{orderId}_{attempt}`.

**Critères d'acceptation**

- Une commande ne peut avoir qu'**un seul lien de paiement vivant** à la fois. Voir
  [invariant I3](../20-specs-fonctionnelles/01-regles-metier.md#i3--jamais-deux-liens-de-paiement-vivants-sur-une-même-commande).
- Une session déjà réglée fait **refuser** une nouvelle demande, même si la commande n'a pas
  encore basculé côté plateforme.
- Une session encore ouverte est **expirée** avant l'émission d'une nouvelle, car la nouvelle
  reflète le panier courant.
- Deux demandes simultanées : la seconde échoue au lieu d'ouvrir un lien parallèle.
- La clé d'idempotence envoyée au prestataire est dérivée de la commande et du numéro de
  tentative, et la requête est **reproductible à l'identique** lors d'un rejeu.

## US-E07-03 — Confirmer le paiement de façon fiable

**En tant que** plateforme
**je veux** traiter la confirmation de paiement du prestataire
**afin de** confirmer les réservations et créditer le professeur.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`CheckoutWebhookService`](../../RestaurantHandler/Services/Payments/CheckoutWebhookService.cs)
Webhook `checkout.session.completed`, signature vérifiée, garde par transition
`PENDING → PAID`, crédit calculé sur les valeurs figées.

**Critères d'acceptation**

- La notification est authentifiée par signature ; une signature invalide est rejetée.
- Au traitement : l'encaissement est enregistré, la commande passe payée, ses réservations
  passent confirmées, et le portefeuille du professeur est crédité du montant net de
  commission.
- La garde contre le double traitement est **la transition d'état** : rejouer la notification
  ne recrédite pas.
- Le crédit est calculé à partir des valeurs **figées sur la réservation**, jamais des prix
  courants.
- Un échec de traitement est réessayé et reste visible en supervision.

## US-E07-04 — Libérer un créneau quand le paiement expire

**En tant que** plateforme
**je veux** réagir à l'expiration d'une session de paiement
**afin de** libérer le créneau sans attendre le balayage périodique.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`CheckoutWebhookService`](../../RestaurantHandler/Services/Payments/CheckoutWebhookService.cs)
Webhook `checkout.session.expired` ; `clean-stale-orders` reste le filet de
sécurité.

**Critères d'acceptation**

- La commande n'est libérée que si la session expirée est **bien la session courante** : une
  notification tardive concernant une session remplacée est ignorée.
- Le résultat est identique à celui du balayage périodique, qui reste le filet de sécurité.

## US-E07-05 — Réconcilier les frais du prestataire

**En tant que** plateforme
**je veux** renseigner les frais réels et le net encaissé après coup
**afin que** la comptabilité soit exacte.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`PaymentService`](../../RestaurantHandler/Services/PaymentService.cs)
`BackfillStripeFeesAsync`, planifiée avec 10 réessais.

**Critères d'acceptation**

- Les frais ne sont **pas** connus à l'encaissement : ils sont récupérés une fois l'opération
  réglée chez le prestataire, par une tâche différée avec réessais.
- **Aucune règle métier ne dépend de ces frais** au moment d'une action utilisateur. Voir
  [politique d'annulation](../20-specs-fonctionnelles/05-politique-annulation.md).
- Tant que les frais sont inconnus, la comptabilité affiche un gain **provisoire**, clairement
  identifié.

## US-E07-06 — Revenir sur la plateforme après le paiement

**En tant qu'** élève
**je veux** être ramené sur une page claire après le paiement
**afin de** savoir si mes réservations sont confirmées.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs)
Adresses de retour `/payment/success`, `/payment/cancel` et retours d'onboarding
construites par l'API ; les pages relèvent du front.

**Critères d'acceptation**

- Trois retours : succès, annulation, retour d'onboarding professeur.
- La page de succès n'affirme rien tant que la confirmation n'est pas traitée : elle indique
  un état en cours et s'actualise.
- **La page de retour ne vaut jamais confirmation de paiement** : seule la notification
  signée du prestataire fait foi.
- La page d'annulation propose de reprendre le paiement tant que l'échéance n'est pas passée.

## US-E07-07 — Protéger les points d'accès de paiement

**En tant que** plateforme
**je veux** qu'aucun point d'accès de paiement ne soit ouvert anonymement
**afin d'** empêcher la manipulation des commandes et des comptes bancaires d'autrui.

**Priorité :** Doit

**État :** Partielle · **Code :** [`PaymentController`](../../RestaurantHandler/Controllers/PaymentController.cs)
Seule la signature des webhooks est conforme. **Écarts critiques :** checkout,
onboarding et transfert manuel sont `[AllowAnonymous]`. Voir la
[dette technique](../50-backlog/03-dette-technique.md).

**Critères d'acceptation**

- La création d'un paiement, l'onboarding et le transfert exigent une session authentifiée et
  vérifient la **propriété** de la ressource.
- Seules les réceptions de notification du prestataire sont anonymes, et elles sont
  authentifiées par signature.
- Voir [dette technique](../50-backlog/03-dette-technique.md) : ces points d'accès sont
  actuellement ouverts en production.
