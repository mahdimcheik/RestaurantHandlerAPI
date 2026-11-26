# E01 — Comptes & authentification

Le socle hérité couvre l'essentiel (inscription, activation, connexion mot de passe et Google,
profil, avatar). Les stories ci-dessous fixent ce qui est attendu pour Greenadine.

### US-E01-1 — Créer un compte client

En tant que visiteur, je veux créer un compte afin de pouvoir commander en ligne.

**Priorité :** V1
**État :** Implémentée

**Critères d'acceptation**

- L'inscription demande nom, prénom, e-mail, mot de passe et les consentements (CGU,
  politique de confidentialité) ; le compte créé a le rôle `Client`. Pas de date de naissance
  (supprimée en J0 : aucun usage métier).
- Un e-mail d'activation est envoyé ; tant que le compte n'est pas activé, la connexion est
  refusée avec un message explicite.
- Un e-mail déjà utilisé (insensible à la casse) est refusé.

### US-E01-2 — Se connecter avec Google

En tant que visiteur, je veux me connecter avec mon compte Google afin de commander sans créer
de mot de passe.

**Priorité :** V1
**État :** Implémentée

**Critères d'acceptation**

- Un premier login Google crée un compte `Client` déjà activé.
- Sur l'inscription, « Continuer avec Google » est proposé en tête ; la mention affichée sous le
  bouton (CGU, politique de confidentialité, traitement des données) vaut acceptation au clic.
- Depuis la connexion, un compte Google inconnu ouvre « Finaliser votre compte » (même mention,
  bouton « Créer mon compte ») au lieu d'une erreur ; l'API signale ce cas par le statut 428.
- Un login Google sur un e-mail déjà inscrit rattache la connexion au compte existant.
- Le rôle `Manager` n'est jamais attribuable par Google ni par l'inscription publique.

### US-E01-3 — Commander exige d'être connecté

En tant que restaurant, je veux que toute commande en ligne soit rattachée à un compte afin de
pouvoir joindre le client et limiter les commandes fantômes.

**Priorité :** V1
**État :** Partielle (la connexion renvoie `ProfileIncomplete` tant que le téléphone manque)

**Critères d'acceptation**

- La carte et le panier sont accessibles sans compte.
- La validation du panier redirige vers la connexion puis revient au panier, intact.
- Le numéro de téléphone est demandé à la première commande s'il manque au profil, et
  enregistré.

### US-E01-4 — Gérer son profil

En tant que client, je veux modifier mes informations (nom, prénom, téléphone, avatar) afin
d'être joignable.

**Priorité :** V1
**État :** Implémentée

**Critères d'acceptation**

- `PUT /Auth/me` met à jour nom, prénom et téléphone ; l'e-mail ne se change pas par ce biais.
- Le téléphone est facultatif, validé (format téléphone, 20 caractères max) ; vide = effacé.
- Un client peut aussi renseigner un **profil professionnel** (SIREN, raison sociale, adresse
  de facturation) pour demander des factures :
  [US-E05-6](E05-paiement.md#us-e05-6--demander-une-facture-professionnelle).

### US-E01-5 — Réinitialiser son mot de passe

En tant que client, je veux réinitialiser mon mot de passe oublié afin de récupérer mon compte.

**Priorité :** V1
**État :** Implémentée

**Critères d'acceptation**

- La demande renvoie toujours la même réponse, que l'e-mail existe ou non.
- Le lien est à usage unique et expire.
- Après réinitialisation, les sessions existantes sont invalidées.

### US-E01-6 — Supprimer son compte

En tant que client, je veux supprimer mon compte afin d'exercer mon droit à l'effacement (RGPD).

**Priorité :** V1
**État :** Absente

**Critères d'acceptation**

- Les données personnelles sont anonymisées ; les commandes passées sont conservées (obligation
  comptable) sans lien vers une personne identifiable.
- Une commande en cours (non retirée, non annulée) bloque la suppression.

### US-E01-7 — Connexion Manager sur appareil partagé

En tant que Manager, je veux qu'un appareil du restaurant (tablette de service, écran KDS) reste
connecté pendant le service afin de ne pas me reconnecter à chaque commande.

**Priorité :** V1
**État :** Partielle

**Critères d'acceptation**

- La session Manager se renouvelle sans action tant que l'appareil est utilisé.
- Un Manager peut se déconnecter de tous ses appareils.
- Le KDS se reconnecte seul au temps réel après une coupure réseau.
