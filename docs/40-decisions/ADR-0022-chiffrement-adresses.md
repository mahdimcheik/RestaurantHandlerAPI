# ADR-0022 — Chiffrement au repos des adresses postales (rue et complément)

**Statut :** Accepté
**Application :** Appliquée (branche `feature/web-push`, 2026-10-23)
**Date :** 2026-10-23

## Contexte

Les adresses des utilisateurs étaient stockées en clair, et plusieurs endpoints lus par d'autres
que le propriétaire (liste admin des profs, fiche admin d'un utilisateur, changement de statut)
renvoyaient la rue complète. Une fuite de la base, d'une sauvegarde ou d'une réponse admin
exposait donc le domicile des élèves et des profs.

## Décision

- **Chiffrés :** `Address.Street` et `Address.AdditionalInfo` (digicode, étage…).
  **En clair :** `City`, `ZipCode`, `Country`, `Latitude`/`Longitude`. Les coordonnées sont
  approximatives par construction : elles sont géocodées depuis ville + code postal uniquement
  (`GeocodingService`), jamais depuis la rue. La recherche par rayon, ville ou code postal
  (`TeacherSearchService`) fonctionne donc sans changement.
- **AES-256-GCM**, clé dans `ADDRESS_ENCRYPTION_KEY` (32 octets en base64, obligatoire au
  démarrage, validée par `EnvironmentVariables.ValidateRequired`). La clé vit hors de la base,
  comme `JWT_KEY` ; pas d'ASP.NET Data Protection, dont le trousseau n'est persisté nulle part
  aujourd'hui (perdu à chaque redéploiement Docker).
- Format stocké : `enc:v1:` + base64(nonce ‖ chiffré ‖ tag), nonce aléatoire par écriture
  ([`FieldEncryption`](../../RestaurantHandler/Utilities/FieldEncryption.cs)). Le préfixe versionné laisse
  la place à une rotation de clé.
- **Transparent pour le code métier :** un `ValueConverter`
  ([`EncryptedStringConverter`](../../RestaurantHandler/Utilities/EncryptedStringConverter.cs)) branché dans
  `AddressConfiguration`. L'entité ne porte que du clair, la base que du chiffré ; la facture
  PDF (`InvoiceDocument`) lit l'entité et imprime donc l'adresse complète.
- **Lisible par le seul propriétaire :** `UserDetails` / `UserAdminDetails` renvoient l'adresse
  réduite (ville, CP, pays, coordonnées) **par défaut** ; seuls les chemins « moi-même »
  (connexion, refresh, mon profil, mise à jour de mon profil, `GET /addresses`) passent
  `includeFullAddress: true`. L'admin ne voit plus la rue.
- **Reprise des données :** `AddressEncryptionBackfill` chiffre au démarrage, en SQL brut, toute
  ligne encore en clair (archivées comprises). Rejouable sans effet. À la lecture, une valeur
  sans préfixe est rendue telle quelle, le temps de la reprise.

## Conséquences

- **Perdre la clé rend toutes les rues illisibles** (y compris pour les factures futures) : elle
  doit être sauvegardée hors du serveur. La changer sans rotation casse la lecture de l'existant.
- Plus de filtre, de tri ni d'index SQL possible sur la rue (aucun usage aujourd'hui).
- Le `Down` de la migration `EncryptAddresses` ne déchiffre rien : revenir en arrière demande de
  déchiffrer d'abord, sinon les valeurs chiffrées dépassent les anciennes longueurs.
- Hors périmètre : l'adresse d'entreprise SIRET du prof (`Teacher.SiretCompanyAddress`) est une
  donnée publique du registre INSEE et reste en clair.

## Alternatives écartées

- **Chiffrement côté PostgreSQL (`pgcrypto`)** : la clé transiterait dans les requêtes SQL et les
  journaux.
- **Chiffrer aussi ville et code postal** : casse la recherche géographique, pour un gain faible
  (une ville n'identifie pas une personne).
