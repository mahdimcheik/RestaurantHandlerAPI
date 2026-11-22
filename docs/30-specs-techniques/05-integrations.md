# Intégrations externes

> Statut : **Stable**

## Principes communs

| Règle | Énoncé |
|---|---|
| **Client typé, jamais instancié à la main** | un client HTTP par intégration, configuré une fois |
| **Délai d'attente explicite** | 5 secondes par défaut ; aucun appel sans limite |
| **Comportement en panne déclaré** | chaque intégration dit si elle échoue *ouvert* ou *fermé* |
| **Aucun secret en dur** | configuration par variables d'environnement, sans valeur de repli |
| **Journalisation sans donnée sensible** | identifiants de corrélation, jamais de contenu |

**Échouer ouvert ou fermé**, c'est la question la plus importante d'une intégration :

| Intégration | En panne | Pourquoi |
|---|---|---|
| Vérification d'entreprise | **fermé** — l'inscription est refusée | laisser passer un SIRET non vérifié casse la promesse de confiance |
| Géocodage | **ouvert** — l'adresse est enregistrée sans coordonnées | une adresse non géolocalisée reste une adresse valable |
| Envoi d'e-mail | **différé** — mis en file, réessayé | un prestataire indisponible ne doit pas faire échouer un paiement |
| Paiement | **fermé** — l'opération échoue proprement | on ne simule jamais un encaissement |
| Stockage de fichiers | **fermé** — le dépôt échoue | une pièce perdue est pire qu'un dépôt refusé |
| Identité externe | **fermé** — la connexion échoue | on n'authentifie pas sans vérification |

---

## Paiement

**Rôle :** encaissement auprès de l'élève, comptes connectés des professeurs, transferts,
remboursements.

| Élément | Choix |
|---|---|
| Encaissement | session de paiement hébergée, **une ligne par réservation** |
| Compte professeur | compte connecté de type simplifié, parcours d'inscription hébergé |
| Versement | **un transfert par réservation** |
| Remboursement | différé, exécuté par tâche récurrente |
| Idempotence | clé dérivée de données stables — commande et tentative, ou réservation |

**Deux points de réception de notifications, avec des secrets distincts :**

| Point | Événements |
|---|---|
| Plateforme | paiement réussi, session expirée, paiement échoué, remboursement émis |
| Comptes connectés | mise à jour d'un compte professeur |

Un compte professeur est considéré opérationnel lorsque le dossier est **soumis** *et* les
versements **autorisés** — les deux conditions.

**Contraintes du prestataire à connaître :**

- expiration d'une session : minimum 30 minutes, maximum 24 heures ;
- une clé d'idempotence n'est dédupliquée que **24 heures** et **refuse** la même clé avec
  des paramètres différents — d'où le figement en base avant l'appel ;
- un transfert exige des fonds **effectivement disponibles** : l'erreur de solde insuffisant
  est traitée comme transitoire et rejouée au cycle suivant, jamais comme un échec définitif ;
- les frais ne sont connus qu'après règlement de l'opération, d'où la réconciliation différée.

**En développement :** les notifications sont relayées vers l'API locale par l'outil en ligne
de commande du prestataire, et les clés de test sont distinctes des clés réelles.

## Vérification d'entreprise

**Rôle :** valider un SIRET à l'inscription et au passage en statut professionnel.

- Contrôle de **format d'abord** — clé de Luhn, avec l'exception des établissements de La
  Poste — puis appel au référentiel officiel. Un SIRET malformé ne consomme pas d'appel.
- Trois issues : établissement et unité légale actifs → vérifié ; existant mais fermé →
  signalé pour revue administrative ; introuvable → refus.
- **Échec fermé** : service indisponible signifie inscription refusée.
- Données conservées : raison sociale (repli sur le nom de la personne pour un entrepreneur
  individuel), adresse, code d'activité et sa nomenclature.
- Appel effectué **avant toute écriture en base**.

## Géocodage

**Rôle :** obtenir les coordonnées d'une adresse pour la recherche par rayon.

- Service public français, à partir de la ville et du code postal.
- **Échec ouvert** : l'enregistrement de l'adresse réussit toujours ; l'absence de
  coordonnées est signalée à l'utilisateur, car elle le rend invisible en recherche par
  rayon.
- Le calcul de distance combine un **pré-filtrage par boîte englobante** — utilisable par un
  index — et une distance exacte ensuite.

## Identité externe

**Rôle :** connexion et inscription par fournisseur tiers.

- Le jeton d'identité est validé **côté serveur** : signature, émetteur, audience,
  expiration. Le client n'est jamais cru sur parole.
- Récupération de l'avatar au **mieux** : un échec n'interrompt jamais la connexion.
- Le contrôle de bannissement précède toute écriture.

## Envoi d'e-mails

**Rôle :** messages transactionnels.

- Protocole standard, chiffrement **strict** : connexion sécurisée sur le port dédié, sinon
  élévation de sécurité obligatoire. **Jamais** de dégradation silencieuse en clair.
- Rendu depuis des gabarits versionnés.
- **Mis en file, jamais envoyé en ligne.** Réessais avec délais croissants, puis signalement.
- **Seuls les éléments nécessaires au rendu transitent par la file** : destinataire, type,
  identifiants métier. Jamais le corps rendu. Les arguments d'une tâche sont persistés en
  clair et lisibles dans le tableau de bord technique.

> **Développement local.** Le chiffrement strict casse les serveurs de test sans certificat,
> et l'échec est indirect — plusieurs réessais puis un abandon. La position retenue : fournir
> un serveur de test **avec certificat auto-signé** documenté, plutôt qu'un indicateur de
> tolérance. Si un tel indicateur devait exister, il serait faux par défaut et
> accompagné d'un avertissement bruyant au démarrage.

## Stockage de fichiers

**Rôle :** avatars, pièces justificatives, factures.

- **Un seul fournisseur compatible S3, derrière une seule abstraction**
  ([ADR-0008](../40-decisions/ADR-0008-stockage-objet-unifie.md)) : MinIO auto-hébergé.
- Un seul compartiment **privé** ; tout fichier, avatars compris, est servi par **lien signé
  à durée limitée**.
- Clés construites de façon déterministe et assainie ; le nom fourni par l'utilisateur n'est
  jamais un chemin.
- Le type est vérifié par signature binaire avant écriture.

## Génération de documents

**Rôle :** factures PDF.

- Génération à la demande, **mise en cache par commande** : une facture et une seule.
- Le contenu est reconstruit à partir des données figées sur la commande, jamais des prix
  courants.
- La bibliothèque est exécutée hors du fil de requête si la génération dépasse un seuil.

## Traitement d'images

**Rôle :** avatars.

- Recadrage au carré, redimensionnement, conversion en format web compact.
- Limites de dimensions **en entrée** : une image décompressée trop grande est un vecteur de
  déni de service.
- Le fichier d'origine n'est pas conservé.

## Variables de configuration

Par famille, sans valeur de repli. L'absence d'une variable requise **fait échouer le
démarrage**.

| Famille | Contenu |
|---|---|
| Base de données | hôte, port, nom, utilisateur, mot de passe |
| Session | clé de signature, durées de validité |
| URL | adresse publique de l'API, adresse publique du site |
| Paiement | clés publique et secrète, secrets des deux points de réception |
| Identité externe | identifiant client |
| E-mail | hôte, port, identifiants, adresse d'expédition |
| Stockage | point d'accès, clés, compartiment, région, durée des liens, limites |
| Vérification d'entreprise | clé d'API, adresse de base |
| Géocodage | adresse de base |
| Administration | adresses et mots de passe initiaux, **distincts** |
| Divers | version applicative, autorisation de l'alimentation de démonstration |

## État dans le code

| Intégration | Code | État |
|---|---|---|
| Paiement | [`CheckoutService`](../../RestaurantHandler/Services/Payments/CheckoutService.cs), [`StripeConnectService`](../../RestaurantHandler/Services/Payments/StripeConnectService.cs), [`StripeRefundService`](../../RestaurantHandler/Services/Payments/StripeRefundService.cs), [`StripeWebhookService`](../../RestaurantHandler/Services/Payments/StripeWebhookService.cs) (SDK Stripe.net) | Conforme sur le fond ; points d'accès anonymes (voir [sécurité](04-securite.md#failles-et-écarts-actuels)). Le SDK est configuré globalement, pas par client typé. |
| Vérification d'entreprise | [`InseeSireneService`](../../RestaurantHandler/Services/InseeSireneService.cs), client typé, 5 s | Conforme : indisponibilité → refus à l'inscription (`AuthService`) et au passage professionnel (`UserService`). |
| Géocodage | [`GeocodingService`](../../RestaurantHandler/Services/GeocodingService.cs), client typé, 5 s | Conforme, échec ouvert. |
| Identité externe | [`GoogleAuthService`](../../RestaurantHandler/Services/GoogleAuthService.cs), client typé, 5 s | Conforme. |
| E-mails | [`MailService`](../../RestaurantHandler/Services/MailService.cs), MailKit + RazorLight via Hangfire | Chiffrement strict conforme. **Le corps rendu transite par la file.** Pas de serveur de test avec certificat documenté (voir [exploitation](08-exploitation.md)). |
| Stockage | [`StorageService`](../../RestaurantHandler/Integrations/Storage/StorageService.cs) (avatars, pièces justificatives, factures : clés, validation, liens signés) | Conforme : MinIO, un compartiment privé, tout par lien signé. |
| Documents | [`FacturationService`](../../RestaurantHandler/Services/FacturationService.cs), [`InvoiceDocument`](../../RestaurantHandler/Utilities/InvoiceDocument.cs) (QuestPDF) | Facture nommée d'après le numéro de commande ; mise en cache à vérifier. |
| Images | `AuthService.UploadAvatar` (ImageSharp) | Recadrage carré et redimensionnement ; limites de dimensions en entrée à vérifier. |
| Configuration | [`EnvironmentVariables`](../../RestaurantHandler/Utilities/EnvironmentVariables.cs) | **Valeurs de repli** sur la plupart des variables, y compris les secrets ; aucun échec au démarrage. |
