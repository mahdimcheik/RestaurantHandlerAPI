# E02 — Profils

> Statut : **Stable** · Conventions : [README](README.md)

## US-E02-01 — Consulter la fiche publique d'un professeur

**En tant que** visiteur, même non connecté
**je veux** consulter la fiche d'un professeur
**afin de** décider si je veux réserver chez lui.

**Priorité :** Doit

**État :** Partielle · **Code :** [`ProfileService`](../../RestaurantHandler/Services/Users/ProfileService.cs), [`TeacherProfileService`](../../RestaurantHandler/Services/Users/TeacherProfileService.cs), [`TeachersController`](../../RestaurantHandler/Controllers/TeachersController.cs)
`GET /Teachers/user/{userId}`. Le rendu serveur relève du front. Les données exposées
par la représentation publique restent à auditer champ par champ (aucune entité ne doit
sortir telle quelle).

**Critères d'acceptation**

- La fiche affiche : avatar, prénom et nom, titre, âge, description, langues parlées,
  réseaux sociaux, badges « vérifié » et « professionnel », note moyenne et nombre d'avis.
- Elle affiche le catalogue de produits en lecture seule, les formations, la ville de
  l'adresse principale sur une carte, et les avis reçus.
- Un panneau latéral montre les **prochains créneaux disponibles** et une invitation à
  réserver.
- **Aucune donnée personnelle sensible** n'est exposée : ni adresse exacte, ni e-mail, ni
  téléphone, ni SIRET, ni information bancaire.
- La page est rendue côté serveur avec ses métadonnées et ses données structurées. Voir
  [E19](E19-transverse.md).

## US-E02-02 — Contacter un professeur depuis sa fiche

**En tant qu'** élève
**je veux** envoyer un message depuis la fiche d'un professeur
**afin de** poser une question avant de réserver.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`TeacherContactService`](../../RestaurantHandler/Services/Users/TeacherContactService.cs), [`StudentsController`](../../RestaurantHandler/Controllers/StudentsController.cs)
`POST /Students/contact-teacher` : e-mail et notification interne. Écarts : aucun
nettoyage du contenu, aucune limite de fréquence, pas de trace persistée
(`ContactRequest` absent).

**Critères d'acceptation**

- Le formulaire demande prénom, nom et message.
- Le message est transmis au professeur par e-mail **et** en notification interne.
- Le contenu est nettoyé avant tout affichage.
- Le formulaire n'est pas proposé sur sa propre fiche.
- Un envoi abusif répété est limité en fréquence.

## US-E02-03 — Modifier mes informations personnelles

**En tant que** membre
**je veux** modifier mon profil
**afin de** le garder à jour.

**Priorité :** Doit

**État :** Partielle · **Code :** [`ProfileService`](../../RestaurantHandler/Services/Users/ProfileService.cs), [`TeacherProfileService`](../../RestaurantHandler/Services/Users/TeacherProfileService.cs)
Écart : la description enrichie n'est **pas** nettoyée (aucune bibliothèque de
nettoyage HTML dans le projet).

**Critères d'acceptation**

- Champs modifiables : prénom, nom, date de naissance, genre, titre, description en texte
  enrichi, langues parlées, réseaux sociaux.
- L'adresse e-mail n'est **pas** modifiable directement : elle exige une revalidation.
- La description enrichie est nettoyée à l'enregistrement **et** à l'affichage.
- Un professeur dispose en plus du champ **tarif indicatif horaire**.

## US-E02-04 — Changer mon avatar

**En tant que** membre
**je veux** téléverser une photo de profil
**afin d'** être reconnaissable.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`AvatarService`](../../RestaurantHandler/Services/Auth/AvatarService.cs)
Écarts : point d'accès marqué `[AllowAnonymous]` (le service refuse toutefois un
appel sans utilisateur, avec un statut `40` erroné) ; type vérifié par extension et type
déclaré, pas par signature ; aucun plafond de taille propre à l'avatar. Recadrage, WebP et
suppression de l'ancien fichier conformes.

**Critères d'acceptation**

- Le point d'accès exige d'être **authentifié** ; l'image remplace celle du compte connecté
  et d'aucun autre.
- Le type réel du fichier est vérifié par sa signature binaire, pas par son extension.
- L'image est recadrée au carré, redimensionnée et convertie dans un format web compact.
- La taille est plafonnée ; au-delà, un message clair est renvoyé.
- L'ancien fichier est supprimé du stockage.

## US-E02-05 — Gérer mes adresses

**En tant que** membre
**je veux** enregistrer une adresse principale et une adresse de facturation
**afin d'** être localisé dans les recherches et facturé correctement.

**Priorité :** Doit

**État :** Partielle · **Code :** [`AddressesService`](../../RestaurantHandler/Services/AddressesService.cs), [`GeocodingService`](../../RestaurantHandler/Services/GeocodingService.cs)
Unicité par type et géocodage non bloquant conformes. Écart : aucun attribut
d'autorisation sur le contrôleur (le service lit l'utilisateur depuis la session).

**Critères d'acceptation**

- Champs : rue, ville, pays, code postal, complément facultatif, type.
- **Une seule adresse par type et par utilisateur.**
- À l'enregistrement, les coordonnées géographiques sont calculées automatiquement à partir
  de la ville et du code postal.
- Si le service de géolocalisation est indisponible, **l'enregistrement réussit quand même**
  sans coordonnées : il n'est jamais bloquant.
- Sans coordonnées, le professeur n'apparaît pas dans une recherche par rayon — c'est une
  conséquence assumée, signalée dans son espace.
- La suppression est logique.

## US-E02-06 — Gérer mes formations

**En tant que** professeur
**je veux** lister mes diplômes et cursus
**afin de** justifier ma compétence.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`FormationsService`](../../RestaurantHandler/Services/FormationsService.cs)
Écart : modification et suppression sans attribut d'autorisation. Contrôle des dates
et de propriété présents dans le service.

**Critères d'acceptation**

- Champs : intitulé, établissement, description, date de début, date de fin facultative.
- Création, modification et suppression logique depuis mon espace.
- Les formations sont visibles sur la fiche publique.
- Une date de fin antérieure à la date de début est refusée.

## US-E02-07 — Gérer mes expériences professionnelles

**En tant que** professeur
**je veux** lister mes expériences
**afin de** compléter mon profil.

**Priorité :** Pourrait

**État :** Absente · **Code :** [`Experience`](../../RestaurantHandler/Domain/Entities/Experience/Experience.cs)
L'entité existe, mais aucun contrôleur ni service ne l'expose.

**Critères d'acceptation**

- Champs : intitulé, entreprise, description, date de début, date de fin facultative.
- Mêmes règles de validation que les formations.

## US-E02-08 — Passer en professionnel

**En tant que** professeur particulier
**je veux** déclarer mon SIRET après coup
**afin de** basculer en statut professionnel.

**Priorité :** Devrait · **Dépend de :** US-E01-02

**État :** Partielle · **Code :** [`TeacherProfileService`](../../RestaurantHandler/Services/Users/TeacherProfileService.cs)
`PUT /Teachers/switch-to-professional`. Écart : le changement n'est pas journalisé
pour l'administration.

**Critères d'acceptation**

- Mêmes contrôles qu'à l'inscription : format, puis existence au référentiel officiel.
- En cas de succès, le statut professionnel est activé et les informations d'entreprise sont
  enregistrées.
- Un établissement fermé passe le compte « à vérifier » sans basculer le statut.
- Le changement est journalisé et visible côté administration.
- L'action n'est proposée qu'aux professeurs non encore professionnels.

## US-E02-09 — Consulter le profil d'un élève

**En tant que** professeur ayant une réservation avec cet élève, ou administrateur
**je veux** consulter le profil d'un élève
**afin de** savoir à qui j'ai affaire.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ProfileService`](../../RestaurantHandler/Services/Users/ProfileService.cs), [`StudentsController`](../../RestaurantHandler/Controllers/StudentsController.cs)
Écarts : `GET /Students/user/{userId}` est ouvert sans attribut et
`GET /Students/profile/{userId}` à tout utilisateur connecté, sans contrôle de réservation
partagée.

**Critères d'acceptation**

- Le profil affiche prénom, nom, avatar et description.
- Il n'est accessible **ni publiquement ni à un professeur sans réservation partagée**.
- Aucune donnée de contact ou de paiement n'y figure.
