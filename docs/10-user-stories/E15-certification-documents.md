# E15 — Certification & documents

> Statut : **Stable**
>
> C'est le **sas d'entrée de l'offre** : un professeur non vérifié ne déclenche aucun
> paiement. Voir
> [invariant I13](../20-specs-fonctionnelles/01-regles-metier.md#i13--le-paiement-nest-exigé-que-si-le-professeur-peut-être-payé).

## US-E15-01 — Savoir ce qu'on attend de moi

**En tant que** professeur
**je veux** voir la liste des pièces à fournir et l'état de chacune
**afin de** compléter mon dossier sans deviner.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`TeacherDocumentService`](../../RestaurantHandler/Services/Users/TeacherDocumentService.cs), [`TeacherVerificationHelper`](../../RestaurantHandler/Utilities/TeacherVerificationHelper.cs)
`GET /Teachers/get-documents`, `GET /Teachers/document-requests`. Les types requis par
défaut sont pris en compte par la règle de vérification sans créer de demande en base :
écart de forme avec le dernier critère, sans effet visible.

**Critères d'acceptation**

- Mon espace affiche mon état de vérification et, le cas échéant, sa date.
- Les demandes **ouvertes** sont listées : type de pièce, note éventuelle de l'administrateur,
  date de la demande.
- Les types requis par défaut non encore fournis apparaissent même sans demande explicite.
- Chaque pièce déposée affiche son état : en attente, validée, refusée — et **le motif** en
  cas de refus.
- À la création du compte, les demandes correspondant aux types requis par défaut sont
  générées automatiquement.

## US-E15-02 — Déposer une pièce

**En tant que** professeur
**je veux** téléverser un justificatif
**afin de** faire avancer ma vérification.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`TeacherDocumentService`](../../RestaurantHandler/Services/Users/TeacherDocumentService.cs), [`StorageService`](../../RestaurantHandler/Integrations/Storage/StorageService.cs)
Validation par signature et taille (`StorageService.Upload`), stockage privé,
recalcul de la vérification.

**Critères d'acceptation**

- Je choisis un type de pièce, un titre et un fichier ; une description est facultative.
- Le type réel du fichier est vérifié par sa **signature binaire**, pas par son extension ; la
  liste des types acceptés est restreinte.
- La taille est plafonnée, avec un message explicite au-delà.
- Le dépôt satisfait la demande ouverte correspondante, s'il y en a une.
- Le fichier est stocké en **accès privé** ; il n'est consultable que par son propriétaire et
  l'administration, via un lien signé à durée limitée.
- La pièce est créée en attente de revue.

## US-E15-03 — Supprimer une pièce

**En tant que** professeur
**je veux** retirer un document
**afin de** corriger une erreur de dépôt.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`TeacherDocumentService`](../../RestaurantHandler/Services/Users/TeacherDocumentService.cs), [`TeacherVerificationHelper`](../../RestaurantHandler/Utilities/TeacherVerificationHelper.cs)
Refus sur une pièce validée, recalcul après suppression.

**Critères d'acceptation**

- Possible sur une pièce en attente ou refusée.
- Une pièce **validée** n'est pas supprimable par le professeur : elle justifie son état
  vérifié.
- Le fichier est effectivement supprimé du stockage.
- La demande correspondante redevient ouverte et l'état de vérification est recalculé.

## US-E15-04 — Revoir un dossier

**En tant qu'** administrateur
**je veux** examiner les pièces d'un professeur et les valider ou les refuser
**afin de** décider s'il peut exercer.

**Priorité :** Doit

**État :** Implémentée · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs), [`TeacherVerificationHelper`](../../RestaurantHandler/Utilities/TeacherVerificationHelper.cs)
`GET /UserManagement/teachers/{userId}/documents`,
`PATCH /UserManagement/documents/{documentId}/review` : motif de refus exigé, relecteur et
date conservés, recalcul systématique.

**Critères d'acceptation**

- Un écran par professeur rassemble : état de vérification, pièces avec leur type et leur
  état, demandes ouvertes, et informations d'entreprise en cas de statut professionnel.
- Chaque pièce peut être prévisualisée ou téléchargée via un lien signé à durée limitée.
- La validation et le refus sont journalisés avec l'auteur et la date.
- **Le refus exige un motif**, transmis au professeur.
- La décision **recalcule** l'état de vérification ; celui-ci n'est jamais posé à la main.

## US-E15-05 — Demander une pièce complémentaire

**En tant qu'** administrateur
**je veux** demander un document précis à un professeur
**afin de** compléter un dossier insuffisant.

**Priorité :** Devrait

**État :** Implémentée · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs)
`POST /UserManagement/teachers/{userId}/document-requests` et annulation par
archivage.

**Critères d'acceptation**

- Je choisis un type de pièce et j'ajoute une note explicative.
- Le professeur est notifié et voit la demande dans son espace.
- Une demande peut être annulée ; elle est alors close sans être détruite.
- Une demande ouverte empêche le passage à l'état vérifié.

## US-E15-06 — Gérer le catalogue des types de pièces

**En tant qu'** administrateur
**je veux** administrer les types de justificatifs
**afin d'** adapter les exigences sans redéploiement.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs)
CRUD des types, indicateur « requis par défaut ». Écart : ouvert aux administrateurs
alors que la cible le réserve au super-administrateur.

**Critères d'acceptation**

- Création, modification et suppression d'un type, avec libellé, couleur, icône et indicateur
  « requis par défaut ».
- Le passage d'un type en « requis par défaut » ne réécrit **pas** rétroactivement les
  dossiers existants ; il s'applique aux comptes créés ensuite.
- Un type référencé par des pièces existantes n'est pas détruit mais archivé.
- Les libellés sont traduisibles. Voir [E18](E18-referentiels-i18n.md).

## US-E15-07 — Vérifier un SIRET signalé

**En tant qu'** administrateur
**je veux** consulter le résultat de la vérification SIRET d'un professeur
**afin de** traiter les cas signalés.

**Priorité :** Devrait · **Dépend de :** US-E01-02

**État :** Implémentée · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs)
`GET /UserManagement/teachers/{userId}/siret-verification` et confirmation manuelle.

**Critères d'acceptation**

- L'écran affiche le SIRET, le statut de vérification, la date, la raison sociale, l'adresse
  et le code d'activité enregistrés.
- Les comptes « à vérifier » — SIRET existant mais établissement fermé — sont listés
  séparément.
- Je peux confirmer manuellement ou bannir le compte.

## US-E15-08 — Activer ou bannir un compte

**En tant qu'** administrateur
**je veux** activer manuellement un compte ou le bannir
**afin de** débloquer un utilisateur légitime ou écarter un abus.

**Priorité :** Doit

**État :** Partielle · **Code :** [`UserManagementService`](../../RestaurantHandler/Services/UserManagementService.cs)
`PATCH /UserManagement/users/{userId}/status`. **Écarts :** le bannissement n'invalide
aucune session et n'empêche pas la connexion par mot de passe (seule la connexion Google le
vérifie) ; ni motif ni journal ; l'activation ne marque pas l'e-mail confirmé.

**Critères d'acceptation**

- L'activation manuelle dispense de la confirmation d'e-mail, pour les cas de support.
- Le bannissement empêche immédiatement la connexion, **y compris via un fournisseur
  d'identité externe**, et invalide les sessions ouvertes.
- L'action est journalisée avec son auteur et un motif.
- Bannir un professeur ne détruit ni ses réservations en cours ni ses écritures comptables :
  les obligations d'argent survivent au bannissement.
