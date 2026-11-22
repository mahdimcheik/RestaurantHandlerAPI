# E13 — Messagerie

> Statut : **Stable**
>
> La messagerie est **adossée à une réservation** : elle n'existe pas hors de ce contexte.
> Cela borne la surface d'abus et donne un fil de discussion utile en cas de litige.

## US-E13-01 — Échanger autour d'une réservation

**En tant qu'** élève ou professeur
**je veux** discuter avec l'autre partie depuis la réservation
**afin d'** organiser le cours.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ConversationService`](../../RestaurantHandler/Services/ConversationService.cs), [`ChatHub`](../../RestaurantHandler/Controllers/ChatHub.cs)
Lecture par `GET /Slots/conversation/{reservationId}`, envoi par la méthode du hub
`SendChatByUserEmail`, poussée temps réel. **Écarts de sécurité :** la lecture ne reçoit pas
l'identité de l'appelant — toute conversation est lisible par tout utilisateur connecté ;
l'envoi prend l'expéditeur (`SenderId`) **dans la requête** et ne vérifie pas la
participation. Autres écarts : plafond à 512 caractères, aucun nettoyage.

**Critères d'acceptation**

- La conversation est accessible depuis le détail de la réservation, aux **deux parties
  uniquement** — plus l'administrateur en consultation.
- Les messages sont plafonnés en longueur et nettoyés avant affichage.
- Ils arrivent **en temps réel** chez le destinataire connecté, sans rechargement.
- L'ordre chronologique est garanti par l'horodatage serveur, jamais par celui du client.
- Le fil reste consultable après la fin du cours : c'est une trace.

## US-E13-02 — Voir que l'autre écrit

**En tant que** participant à une conversation
**je veux** savoir que mon interlocuteur est en train de répondre
**afin de** ne pas écrire en même temps.

**Priorité :** Pourrait

**État :** Partielle · **Code :** [`ChatHub`](../../RestaurantHandler/Controllers/ChatHub.cs), [`HardCode`](../../RestaurantHandler/Utilities/HardCode.cs)
Type d'événement `Writing` relayé par `SendMessageByUserEmail`, sans contrôle de
destinataire.

**Critères d'acceptation**

- L'indicateur apparaît pendant la frappe et disparaît après une courte inactivité.
- Il n'est **pas** persisté : c'est un signal éphémère.

## US-E13-03 — Contacter un professeur hors réservation

**En tant qu'** élève
**je veux** poser une question à un professeur avant de réserver
**afin de** vérifier qu'il correspond à mon besoin.

**Priorité :** Devrait · **Voir aussi :** US-E02-02

**État :** Partielle · **Code :** [`TeacherContactService`](../../RestaurantHandler/Services/Users/TeacherContactService.cs)
E-mail et notification interne, sans conversation. Écarts : aucune limite de
fréquence ; le HTML saisi par l'élève est inséré **tel quel** dans un e-mail construit par
concaténation, et envoyé depuis l'adresse de la plateforme.

**Critères d'acceptation**

- Le message part depuis la fiche publique et arrive par e-mail **et** en notification
  interne.
- Il n'ouvre **pas** de conversation temps réel : ce canal reste réservé aux réservations.
- La fréquence d'envoi est limitée par élève et par professeur.

## US-E13-04 — Rester connecté au temps réel

**En tant qu'** utilisateur connecté
**je veux** que la connexion temps réel se rétablisse toute seule
**afin de** ne pas rater de message.

**Priorité :** Devrait

**État :** Partielle · **Code :** [`ChatHub`](../../RestaurantHandler/Controllers/ChatHub.cs), [`Program`](../../RestaurantHandler/Program.cs)
Écarts : le JWT n'est pas lu depuis la chaîne de requête (transport WebSocket depuis
un navigateur à vérifier) ; l'adhésion aux groupes est **déclarée par le client**
(`AddToGroup`). La base reste la source de vérité pour les notifications et messages.

**Critères d'acceptation**

- La connexion s'établit après la restauration de session, jamais pendant un contrôle
  d'accès.
- Elle est authentifiée par la **même session** que les appels classiques.
- Une coupure réseau entraîne une reconnexion automatique avec temporisation croissante.
- Les messages reçus hors connexion sont retrouvés à la reconnexion : le temps réel est un
  confort, **la base reste la source de vérité**.
- L'appartenance aux groupes temps réel est recalculée côté serveur à partir des rôles, jamais
  déclarée par le client.

## US-E13-05 — Fonctionner sur plusieurs instances

**En tant que** plateforme
**je veux** que le temps réel fonctionne quand l'API tourne sur plusieurs instances
**afin de** pouvoir monter en charge.

**Priorité :** Devrait

**État :** Absente · **Code :** [`ConnectionManager`](../../RestaurantHandler/Services/ConnectionManager.cs)
Registre des connexions en mémoire du processus ; aucun fond de panier (backplane).

**Critères d'acceptation**

- La correspondance entre connexion et utilisateur n'est **pas** conservée en mémoire d'un
  seul processus.
- Un message émis depuis une instance atteint un destinataire connecté à une autre.
- Voir [dette technique](../50-backlog/03-dette-technique.md) : l'implémentation actuelle ne
  dépasse pas une instance.

## US-E13-06 — Contacter l'administration

**En tant qu'** élève ou professeur
**je veux** adresser une demande à l'équipe de la plateforme
**afin d'** obtenir de l'aide sur un cas que l'application ne couvre pas — demande
particulière, problème de compte, question sur un versement.

**Priorité :** Pourrait · **Voir aussi :** US-E17-05 · *Story propre à BonProfAPI, issue de
l'ancien `userStory.md` ; absente des specs de NewBonProf.*

**État :** Absente

**Critères d'acceptation**

- Le formulaire est accessible depuis l'espace connecté, avec un objet et un message.
- La demande est **persistée** avec son auteur et sa date, et apparaît dans la liste des
  demandes de l'administration.
- L'auteur est notifié quand sa demande est traitée.
- Le contenu est nettoyé avant affichage et la fréquence d'envoi est limitée.
- Une demande anonyme est refusée : ce canal est réservé aux membres.


## US-E13-07 — Envoyer un fichier dans la conversation

**En tant qu'** élève ou professeur
**je veux** envoyer une image ou un PDF dans la conversation d'une réservation
**afin de** partager un énoncé, une photo de copie ou un document utile au cours.

**Priorité :** Devrait · **Dépend de :** US-E13-01 · **Décision :**
[ADR-0016](../40-decisions/ADR-0016-pieces-jointes-chat.md)

**État :** Implémentée · **Code :** [`ConversationService`](../../RestaurantHandler/Services/ConversationService.cs), [`SlotsController`](../../RestaurantHandler/Controllers/SlotsController.cs)
Envoi par `POST /Slots/conversation/{reservationId}/attachments` en `multipart/form-data`.

**Critères d'acceptation**

- Chacune des deux parties envoie **un fichier par message**, accompagné d'une légende
  facultative plafonnée comme un message. Le fichier est accepté s'il est un **JPEG, PNG,
  WebP ou PDF**, reconnu par sa signature et non par son seul nom ; sinon l'envoi est refusé.
- Un fichier vide est refusé ; un fichier de plus de `ATTACHMENT_MAX_SIZE_BYTES` (10 Mo) est
  refusé.
- Une réservation porte au plus `CHAT_ATTACHMENT_MAX_PER_RESERVATION` (20) fichiers, toutes
  parties confondues ; au-delà, l'envoi est refusé et rien n'est stocké.
- Le message arrive **en temps réel** chez l'autre partie connectée, comme un message texte.
- La conversation liste chaque fichier avec son nom, son type, sa taille et un **lien de
  téléchargement temporaire**, renouvelé à chaque lecture.
- Un fichier envoyé ne se supprime pas : le fil reste une trace, utile en cas de litige.
- Étant donné une réservation à laquelle l'appelant ne participe pas, alors l'envoi répond
  « introuvable ».

## US-E13-08 — Écrire par e-mail à mes élèves

**En tant que** professeur
**je veux** envoyer un e-mail à mes élèves, à tous ou à une sélection
**afin de** les prévenir d'un changement, partager une consigne ou garder le contact.

**Priorité :** Devrait · **Décision :** [ADR-0023](../40-decisions/ADR-0023-envois-mail-groupes.md)

**État :** Implémentée · **Code :** [`MailBroadcastService`](../../RestaurantHandler/Services/Mailing/MailBroadcastService.cs), [`MailBroadcastsController`](../../RestaurantHandler/Controllers/MailBroadcastsController.cs)
`GET /mail-broadcasts/teacher/students`, `POST /mail-broadcasts/teacher`, historique par `POST /mail-broadcasts/search`.

**Critères d'acceptation**

- Sont proposés les élèves ayant au moins une réservation avec moi qui n'est ni rejetée ni
  annulée, et dont le compte est actif.
- Sans sélection, le message part à tous ces élèves ; un élève hors de cette liste fait refuser
  l'envoi.
- Chaque élève reçoit son propre e-mail ; « Répondre » m'écrit directement.
- Le message est nettoyé ; objet de 200 caractères et texte de 20 000 caractères au plus.
- Au plus 10 envois par heure ; au-delà, l'envoi est refusé.
- Je retrouve l'historique de mes envois.
- Un élève ne peut pas utiliser cette fonction.

