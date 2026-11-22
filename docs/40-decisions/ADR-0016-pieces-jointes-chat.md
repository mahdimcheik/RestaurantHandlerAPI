# ADR-0016 — Fichiers dans le chat de réservation : la chaîne des cartes, ouverte aux élèves

**Statut :** Accepté
**Application :** Appliquée (lot 11, 2026-10-03)
**Date :** 2026-10-03

## Contexte

Élèves et professeurs veulent s'envoyer, depuis la conversation d'une réservation, une photo de
copie, un énoncé ou un PDF ([US-E13-07](../10-user-stories/E13-messagerie.md#us-e13-07--envoyer-un-fichier-dans-la-conversation)).
La conversation ne transporte que du texte, par le hub SignalR (`SendChatByUserEmail`).
L'[ADR-0015](ADR-0015-pieces-jointes-cartes.md) vient d'établir, pour les cartes de suivi, une
chaîne de dépôt complète — liste blanche, signature, lien signé — réservée au professeur, en
notant qu'**ouvrir le dépôt aux élèves appellerait un plafond**.

Quatre questions :

1. **Par où passe le fichier** : par le hub SignalR, ou par une requête HTTP ?
2. **Quel modèle** : une pièce jointe rattachée au message, ou au fil ?
3. **Qui dépose, et combien** ?
4. **Peut-on retirer un fichier envoyé** ?

## Décision

**Requête HTTP `multipart/form-data`, pas le hub.** SignalR n'est pas fait pour transporter
10 Mo, et la validation de modèle, la limite de taille de requête et les réponses d'erreur
d'ASP.NET n'y jouent pas. `POST /Slots/conversation/{reservationId}/attachments` enregistre le
message et son fichier, puis pousse l'événement `Chat` existant aux deux parties ; le client du
destinataire relit déjà le fil sur cet événement, il n'y a pas de nouveau type d'événement.

**Un fichier par message, avec une légende facultative.** `ConversationAttachment : Document`,
dans sa propre table, avec une clé étrangère **unique** vers `Conversation`. Le contenu du
message sert de légende et peut être vide. Un message texte reste une ligne `Conversation`
sans pièce jointe : rien ne change pour lui.

**Même liste blanche et même taille que les cartes**, désormais partagées dans `HardCode`
(`ATTACHMENT_EXTENSIONS`, `ATTACHMENT_MAX_SIZE_BYTES`) et contrôlées par `AttachmentRules`
avant tout envoi au stockage. Dépôt sous `reservations/{reservationId}/chat/…`, lien signé à
chaque lecture.

**Les deux parties déposent, vingt fichiers au plus par réservation**
(`CHAT_ATTACHMENT_MAX_PER_RESERVATION`), toutes parties confondues. Le plafond est compté sous
un verrou de la ligne de réservation (`FOR UPDATE`) : deux envois simultanés ne le dépassent
pas. Une réservation bornant le volume à 200 Mo, c'est le plafond que l'ADR-0015 demandait
avant d'ouvrir le dépôt aux élèves.

**Pas de suppression.** Comme les messages texte, un fichier envoyé reste : le fil est une
trace, consultée en cas de litige. L'administrateur lit les fichiers comme il lit le fil.

## Conséquences

**Ce qu'on gagne**

- Aucune nouvelle surface de validation : la chaîne éprouvée des cartes sert telle quelle.
- Le hub ne grossit pas ; le temps réel reste un simple signal de relecture.
- La trace d'un litige comprend les fichiers échangés.

**Ce que ça coûte**

- Un message avec fichier ne passe pas par le même chemin qu'un message texte : deux voies
  d'envoi côté client.
- Un fichier envoyé par erreur ne peut pas être retiré par son auteur.
- Le volume stocké croît avec le nombre de réservations, sans quota par compte.

**Ce qu'on surveille**

- Les demandes de retrait d'un fichier envoyé par erreur : elles appelleraient un retrait
  logique (« fichier retiré ») plutôt qu'une suppression physique, pour garder la trace.
- Le volume stocké sous `reservations/` : une rétention bornée après la fin du cours deviendrait
  nécessaire s'il pesait.

## État dans le code

Appliqué au lot 11 (branche `feat/lot-11-pieces-jointes-chat`) :

- entité `ConversationAttachment : Document` (table `ConversationAttachments`, clé étrangère
  unique vers `Conversation`, en cascade), migration `Lot11PiecesJointesChat` ;
- [`AttachmentRules`](../../RestaurantHandler/Utilities/AttachmentRules.cs) : contrôle de type et de
  taille extrait de `BoardService`, partagé par les deux services ;
- [`ConversationService`](../../RestaurantHandler/Services/ConversationService.cs) :
  `SendAttachmentAsync` vérifie la participation, contrôle le fichier, compte sous le verrou de
  la réservation, dépose puis supprime l'objet si l'enregistrement échoue, et notifie les deux
  parties ; la lecture du fil signe un lien neuf par fichier ;
- `SlotsController` : `[RequestSizeLimit]` à 11 Mo, comme pour les cartes.
