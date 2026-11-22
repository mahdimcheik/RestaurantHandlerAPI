# ADR-0023 — Envois groupés par e-mail : un mail par destinataire, Reply-To de l'expéditeur

**Statut :** Accepté
**Application :** Appliquée (branche `feature/mail-broadcast`, 2026-10-25), non vérifiée sur le serveur SMTP de production
**Date :** 2026-10-25

## Contexte

L'administration n'avait aucun moyen d'écrire à un ensemble d'utilisateurs (annonce, maintenance,
rappel), et un professeur ne pouvait pas écrire d'un coup à ses élèves en dehors du chat d'une
réservation. Les e-mails partent déjà par le serveur SMTP de la plateforme (Stalwart), en file
Hangfire ([`MailService`](../../RestaurantHandler/Services/MailService.cs)).

## Décision

- **Qui écrit à qui** :
  - un administrateur ou super-administrateur choisit un ou plusieurs publics parmi professeurs,
    élèves et administrateurs (super-administrateurs compris), ou les trois ;
  - un professeur écrit à ses élèves, c'est-à-dire ceux qui ont **au moins une réservation qui
    n'est ni rejetée ni annulée** sur l'un de ses créneaux (en attente comprise). Il peut
    restreindre l'envoi à une sélection ; un élève hors de cette liste fait refuser l'envoi (403) ;
  - un élève n'envoie rien.
- **Destinataires joignables** : compte actif, non archivé, avec une adresse ; l'expéditeur est
  exclu ; un compte à plusieurs rôles ne reçoit qu'un exemplaire.
- **Un mail par destinataire**, jamais de copie groupée : aucune adresse n'est exposée aux autres
  destinataires, et chaque envoi a ses propres rejeux.
- **Expéditeur technique, réponse à l'auteur** : le mail part de `DO_NO_REPLY_MAIL` (alignement
  SPF/DKIM du domaine) avec l'adresse de l'auteur en **Reply-To**. Répondre écrit donc
  directement au professeur ou à l'administrateur, dont l'adresse est révélée aux destinataires.
- **Historisé** : `MailBroadcasts` (auteur, public, objet, corps nettoyé, nombre de
  destinataires) et `MailBroadcastRecipients`. L'administration voit tous les envois, un
  professeur les siens.
- **Éclatement en deux temps** : la requête enregistre l'envoi puis met en file un seul job
  (`FanOutAsync`), qui met en file un `DeliverAsync` par destinataire. La requête répond aussitôt,
  même pour des milliers de destinataires.
- **Contenu** : HTML de l'éditeur riche nettoyé par `RichTextSanitizer` ; objet de 200 caractères
  et texte de 20 000 caractères au plus.
- **Quota** : `MAIL_BROADCAST_MAX_PER_HOUR` (10) envois par expéditeur sur une heure glissante,
  compté en base (429 au-delà). Il tient sur plusieurs instances et après un redémarrage,
  contrairement au rate limiter en mémoire.

## Conséquences

- Pas de désinscription : les messages sont liés au service (cours réservés, vie de la
  plateforme) et non commerciaux. Un usage promotionnel imposerait un opt-out et un lien
  `List-Unsubscribe`.
- Un gros envoi « tout le monde » sollicite le serveur SMTP d'un coup ; la file sortante de
  Stalwart l'absorbe, mais les limites des fournisseurs destinataires (Gmail, Outlook) restent à
  surveiller.
- Les textes du mail restent en français côté serveur, comme les autres e-mails.

## Alternatives écartées

- **Un mail avec tous les destinataires en Cci** : un seul échec SMTP fait tout échouer, et
  certains filtres anti-spam pénalisent ces envois.
- **Envoi depuis l'adresse de l'auteur** : casse l'alignement SPF/DKIM du domaine BonProf et
  ferait passer les mails en spam.
