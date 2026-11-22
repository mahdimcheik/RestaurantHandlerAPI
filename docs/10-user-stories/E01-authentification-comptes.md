# E01 — Authentification & comptes

> Statut : **Stable** · Conventions : [README](README.md) · Sécurité :
> [30-specs-techniques/04-securite.md](../30-specs-techniques/04-securite.md)

## US-E01-01 — S'inscrire

**En tant que** visiteur
**je veux** créer un compte en choisissant d'être élève ou professeur
**afin d'** accéder aux fonctions réservées aux membres.

**Priorité :** Doit

**État :** Partielle · **Code :** [`RegistrationService`](../../RestaurantHandler/Services/Auth/RegistrationService.cs)
Écarts : un e-mail déjà utilisé renvoie « L'email est déjà utilisé », ce qui révèle
l'existence du compte. Consentements, transaction et longueur minimale sont conformes.

**Critères d'acceptation**

- Étant donné le formulaire d'inscription
  Quand je le soumets avec nom, prénom, e-mail, mot de passe, date de naissance, genre et
  les deux consentements cochés
  Alors mon compte est créé avec le statut « en attente »
  Et je reçois un e-mail de confirmation.

- Le rôle proposé par défaut est **élève** ; le choix « professeur » est explicite.

- Étant donné que je décline l'un des deux consentements (politique de confidentialité,
  traitement des données)
  Quand je soumets
  Alors l'inscription est refusée — ce sont des consentements obligatoires au sens du RGPD.

- Étant donné un e-mail déjà utilisé
  Quand je soumets
  Alors l'inscription est refusée sans révéler si le compte existe.

- Un mot de passe fait au moins 8 caractères.

- **Aucune donnée n'est écrite** si une étape de la création échoue : création du compte,
  affectation du rôle et création du profil sont atomiques.

## US-E01-02 — M'inscrire comme professeur avec un SIRET

**En tant que** professeur exerçant à titre professionnel
**je veux** déclarer mon SIRET à l'inscription
**afin d'** être identifié comme professionnel dès l'ouverture de mon compte.

**Priorité :** Devrait · **Dépend de :** US-E01-01

**État :** Implémentée · **Code :** [`RegistrationService`](../../RestaurantHandler/Services/Auth/RegistrationService.cs), [`InseeSireneService`](../../RestaurantHandler/Services/InseeSireneService.cs), [`SiretAttribute`](../../RestaurantHandler/Utilities/SiretAttribute.cs)

**Critères d'acceptation**

- Le SIRET est validé **avant toute écriture en base** : format (clé de Luhn, avec
  l'exception La Poste) puis existence auprès du référentiel officiel des entreprises.

- Étant donné un établissement et une unité légale actifs
  Alors le compte est marqué vérifié
  Et la raison sociale, l'adresse et le code d'activité sont enregistrés.

- Étant donné un SIRET existant mais fermé
  Alors le compte est marqué « à vérifier » et signalé à l'administration.

- Étant donné un SIRET introuvable
  Alors l'inscription est refusée.

- Étant donné que le service de vérification est indisponible
  Alors l'inscription est **refusée** — on ne laisse pas passer un SIRET non vérifié.

## US-E01-03 — Confirmer mon adresse e-mail

**En tant que** nouvel inscrit
**je veux** activer mon compte depuis le lien reçu par e-mail
**afin de** pouvoir me connecter.

**Priorité :** Doit · **Dépend de :** US-E01-01

**État :** Partielle · **Code :** [`RegistrationService`](../../RestaurantHandler/Services/Auth/RegistrationService.cs)
Écarts : la connexion n'est **pas** refusée tant que l'e-mail n'est pas confirmé ;
aucun renvoi de lien. Durée du jeton (1 h) conforme.

**Critères d'acceptation**

- Le lien contient un jeton à usage unique valable **1 heure**.
- À l'activation, le compte passe « actif » et je suis redirigé vers une page de succès.
- Un jeton expiré ou déjà consommé affiche une erreur explicite et propose un renvoi.
- Tant que l'e-mail n'est pas confirmé, la connexion est refusée.

## US-E01-04 — Me connecter

**En tant que** membre
**je veux** me connecter avec mon e-mail et mon mot de passe
**afin d'** accéder à mon espace.

**Priorité :** Doit

**État :** Partielle · **Code :** [`SignInService`](../../RestaurantHandler/Services/Auth/SignInService.cs)
Écart : session par JWT en en-tête, pas par cookie. Verrouillage, refus des comptes bannis ou
non activés, message unique et adresse normalisée : corrigés au lot 0 (D-06, D-09).

**Critères d'acceptation**

- La session est matérialisée par un **cookie `httpOnly`, `Secure`, `SameSite`** ; aucun
  jeton n'est accessible au JavaScript de la page.
- Je suis redirigé vers l'espace correspondant à mon rôle, ou vers la page que je tentais
  d'atteindre avant d'être renvoyé vers la connexion.
- Après **5 tentatives** infructueuses, le compte est verrouillé **2 minutes**.
- Un compte banni ne peut pas se connecter, avec un message distinct de « identifiants
  incorrects ».
- Un identifiant inexistant et un mot de passe faux donnent le **même** message.

## US-E01-05 — Me connecter avec Google

**En tant que** visiteur
**je veux** utiliser mon compte Google
**afin d'** éviter un mot de passe de plus.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`SignInService`](../../RestaurantHandler/Services/Auth/SignInService.cs), [`GoogleAuthService`](../../RestaurantHandler/Services/GoogleAuthService.cs)

**Critères d'acceptation**

- Le jeton d'identité Google est validé côté serveur, audience vérifiée.
- Le rattachement se fait d'abord par identifiant Google, puis par adresse e-mail.
- Si aucun compte ne correspond, un compte **élève** est créé, avec l'avatar Google si
  disponible (meilleur effort, jamais bloquant).
- Un compte en attente devient actif : Google atteste l'adresse.
- Un compte banni est refusé **avant** toute écriture.

## US-E01-06 — Réinitialiser mon mot de passe

**En tant que** membre ayant oublié son mot de passe
**je veux** en définir un nouveau depuis un lien reçu par e-mail
**afin de** retrouver l'accès à mon compte.

**Priorité :** Doit

**État :** Partielle · **Code :** [`PasswordService`](../../RestaurantHandler/Services/Auth/PasswordService.cs)
Écarts : réponse différente si l'adresse est inconnue ; `reset-password` répond
404 « L'utilisateur n'existe pas » ; les sessions ne sont pas invalidées ; le lien transite en
clair dans les arguments Hangfire.

**Critères d'acceptation**

- La demande répond **toujours** de la même façon, que l'adresse existe ou non.
- Le lien est à usage unique et valable 1 heure.
- Après réinitialisation, **toutes les sessions ouvertes sont invalidées**.
- Le lien de réinitialisation ne doit apparaître dans **aucun** journal, tableau de bord
  technique ou fichier de trace. Voir
  [dette technique](../50-backlog/03-dette-technique.md).

## US-E01-07 — Changer mon mot de passe

**En tant que** membre connecté
**je veux** changer mon mot de passe en fournissant l'ancien
**afin de** sécuriser mon compte.

**Priorité :** Devrait

**État :** Absente
`POST /Auth/reset-password` (action `ChangePassword`) exige le jeton de
réinitialisation : aucun changement avec l'ancien mot de passe.

**Critères d'acceptation**

- L'ancien mot de passe est exigé.
- Les autres sessions sont invalidées ; la session courante est conservée.

## US-E01-08 — Rester connecté et me déconnecter

**En tant que** membre
**je veux** que ma session se prolonge d'elle-même et pouvoir m'en défaire
**afin de** ne pas ressaisir mes identifiants en permanence, sans laisser de session ouverte.

**Priorité :** Doit

**État :** Partielle · **Code :** [`SignInService`](../../RestaurantHandler/Services/Auth/SignInService.cs), [`TokenService`](../../RestaurantHandler/Services/Auth/TokenService.cs), [`AuthController`](../../RestaurantHandler/Controllers/AuthController.cs)
Écarts : la déconnexion efface le cookie sans révoquer le jeton de renouvellement
en base ; renouvellement en `GET`, sans jeton anti-falsification (le cookie est toutefois
`SameSite=Strict`).

**Critères d'acceptation**

- La session est renouvelée de façon transparente tant qu'elle est active.
- La déconnexion invalide la session **côté serveur**, pas seulement dans le navigateur.
- Le renouvellement est protégé contre la falsification de requête inter-site.
- Deux renouvellements concurrents ne produisent qu'un seul appel.

## US-E01-09 — Consulter mes informations de session

**En tant que** application cliente
**je veux** récupérer l'utilisateur courant et ses rôles
**afin de** restaurer la session au chargement avant toute navigation.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`AuthController`](../../RestaurantHandler/Controllers/AuthController.cs)
`GET /Auth/my-informations`. La restauration avant navigation relève du front.

**Critères d'acceptation**

- Un point d'accès renvoie l'identité, les rôles, le statut du compte et, pour un
  professeur, ses états de vérification et de confirmation bancaire.
- La navigation initiale du client est déclenchée **après** cette restauration, afin
  qu'aucun contrôle d'accès ne s'exécute sur un état vide.

## US-E01-10 — Choisir mon rôle actif

**En tant qu'** utilisateur disposant à la fois du rôle élève et du rôle professeur
**je veux** basculer explicitement d'un rôle à l'autre
**afin de** savoir dans quel espace j'agis.

**Priorité :** Pourrait · **À arbitrer** — voir
[questions ouvertes](../50-backlog/02-questions-ouvertes.md)

**État :** Absente

**Critères d'acceptation**

- Le rôle actif est visible en permanence et modifiable en un geste.
- Il détermine la navigation et les actions proposées, jamais les droits réels — ceux-ci
  restent vérifiés côté serveur.
- Le cumul avec un rôle d'administration est interdit.
