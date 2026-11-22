# Questions ouvertes

> Statut : **À arbitrer** — chaque question porte une **proposition par défaut**. Tant qu'elle
> n'est pas contestée, c'est elle qui s'applique et qui est écrite dans les specs.

## Q1 — Durée de la fenêtre de litige

**Contexte.** Une seule constante sert de fenêtre de contestation *et* de délai avant
versement. Voir [ADR-0009](../40-decisions/ADR-0009-fenetre-de-litige.md).

| Option | Conséquence |
|---|---|
| 24 h | professeur payé vite, mais l'élève qui a cours le vendredi soir a jusqu'au samedi soir |
| **48 h + 2 h** *(proposé)* | couvre un week-end partiel, absorbe le délai de règlement du prestataire |
| 72 h | confortable pour l'élève, immobilise l'argent du professeur trois jours |

> **Proposition :** conserver 48 h + 2 h — la valeur actuelle. Les 2 heures ne sont pas
> cosmétiques : sans elles, un transfert peut échouer faute de fonds réglés.
>
> **Indicateur de révision :** si des contestations arrivent régulièrement *après* l'échéance,
> allonger — en acceptant alors de concevoir la récupération comme un cas nominal.

## Q2 — Litige que personne ne traite — **tranchée**

> **Tranchée le 2026-09-14**, sur la proposition par défaut. Voir
> [ADR-0011](../40-decisions/ADR-0011-resolution-auto-litige.md).


**Contexte.** Aujourd'hui, si le professeur n'accepte pas et qu'aucun administrateur ne
tranche, le crédit reste gelé **indéfiniment**.

| Option | Conséquence |
|---|---|
| **Pro professeur à J+7** *(proposé)* | cohérent avec le principe « par défaut, le professeur est payé » |
| Pro élève | inverse le principe directeur : un élève obtiendrait un cours gratuit en se taisant |
| Gel indéfini | l'argent d'un professeur immobilisé à vie par un litige oublié |

> **Proposition :** résolution automatique en faveur du professeur à J+7, avec relance de
> l'administration à J+5 et mention explicite « résolu automatiquement » dans l'historique.
> Sept jours laissent largement le temps d'arbitrer tout en bornant l'immobilisation.

## Q3 — Nature du code de présence — **tranchée**

> **Tranchée le 2026-09-14**, sur la proposition par défaut. Voir
> [ADR-0012](../40-decisions/ADR-0012-code-de-presence.md).


**Contexte.** Le code est aujourd'hui détenu par l'élève et saisi par le professeur.

| Option | Conséquence |
|---|---|
| **Élève → professeur** *(proposé)* | l'élève tient le code, le professeur le saisit — mais ne rien saisir ne bloque rien |
| Échange mutuel | plus symbolique, deux fois plus de friction pour zéro gain |
| Supprimer le code | on perd un horodatage utile à l'arbitrage |

> **Proposition :** garder le mécanisme actuel. Il est **sans enjeu par construction** — il
> n'accélère que la trésorerie — donc sa direction importe peu. Ce qu'il apporte vraiment,
> c'est un horodatage exploitable en cas de litige.
>
> **Cas distanciel :** en visio, un événement « a rejoint l'appel » remplacerait avantageusement
> le code. Sans fournisseur intégré en v1, ce n'est pas disponible.

## Q4 — Professeur endetté qui cesse d'enseigner

**Contexte.** Une dette ne se recouvre que par compensation sur des versements futurs. Un
professeur qui arrête ne rembourse jamais. Voir
[ADR-0006](../40-decisions/ADR-0006-dette-prof-compensation.md).

| Option | Conséquence |
|---|---|
| **Plafond de dette** *(proposé)* | au-delà, ses créneaux ne sont plus réservables — borne la perte |
| Prélèvement bancaire | impose un mandat et un parcours contractuel, disproportionné |
| Renvoi de transfert | ne s'applique qu'aux sommes déjà versées, ne couvre pas la pénalité sur solde vide |
| Ne rien faire | perte non bornée |

> **Proposition :** un plafond paramétrable. Au-delà, les créneaux du professeur cessent
> d'être réservables et il est informé du montant à régulariser ; l'administration dispose
> d'une liste des professeurs endettés.
>
> **À décider :** la valeur du plafond. Un ordre de grandeur raisonnable est le montant d'une
> à deux réservations moyennes.

## Q5 — Administrateur et super-administrateur

**Contexte.** Les deux rôles ont aujourd'hui **exactement** les mêmes droits : les arbres de
routes du front sont dupliqués à l'identique.

| Option | Conséquence |
|---|---|
| **Les différencier** *(proposé)* | le super-administrateur gère les administrateurs et les paramètres sensibles |
| Fusionner en un seul rôle | plus simple, mais aucun garde-fou sur les paramètres critiques |
| Laisser en l'état | deux rôles qui ne servent à rien : dette conceptuelle |

> **Proposition :** différencier.
>
> **Super-administrateur seul :** création et retrait de comptes administrateurs, catalogue
> des types de pièces justificatives, paramètres de plateforme (taux, fenêtres, plafonds).
> **Administrateur :** exploitation quotidienne — revue documentaire, arbitrage, gestion des
> utilisateurs, référentiels de cursus, bilans.

## Q6 — Cumul élève et professeur

**Contexte.** Le modèle autorise plusieurs rôles par compte, mais l'interface suppose un rôle
unique avec une précédence implicite.

| Option | Conséquence |
|---|---|
| **Cumul avec sélecteur explicite** *(proposé)* | un professeur peut prendre des cours ; l'espace actif est choisi |
| Interdire le cumul | il faut deux comptes et deux adresses e-mail |
| Cumul avec précédence implicite | comportement actuel : imprévisible pour l'utilisateur |

> **Proposition :** autoriser le cumul élève + professeur avec un sélecteur de rôle visible en
> permanence. Le cumul avec un rôle d'administration reste interdit.
>
> **Attention :** un professeur ne peut pas réserver ses propres créneaux, et la messagerie
> doit rester lisible quand les deux parties portent les deux rôles.

## Q7 — Régime de TVA

**Contexte.** La facture applique un taux plat de 20 %, alors que beaucoup de professeurs
particuliers relèvent de la franchise en base de TVA.

| Option | Conséquence |
|---|---|
| Taux plat | simple, mais **faux** pour un professeur en franchise |
| **Régime par professeur** *(proposé)* | correct, au prix d'un champ et d'un choix à l'onboarding |
| Pas de TVA sur la facture | non conforme pour un professeur assujetti |

> **Proposition :** un champ de régime fiscal sur le profil professeur, avec trois valeurs —
> franchise en base, assujetti au taux normal, exonéré. La facture porte la mention légale
> correspondante. C'est le seul moyen d'émettre une facture juste.
>
> **À confirmer avec un comptable** : c'est le point de cette liste qui porte le plus de
> risque juridique.

## Q8 — Visioconférence

**Contexte.** Le type de créneau « visio » existe, mais aucun fournisseur n'est intégré.

| Option | Conséquence |
|---|---|
| **Lien fourni par le professeur** *(proposé)* | aucun coût, mais aucune preuve de présence |
| Fournisseur intégré | preuve de connexion exploitable en litige, au prix d'une intégration et d'un abonnement |
| Retirer le type « visio » | on perd un segment de marché entier |

> **Proposition :** conserver le type de créneau, champ « lien de connexion » renseigné par le
> professeur et visible de l'élève une fois la réservation confirmée. L'intégration d'un
> fournisseur est reportée après la v1 — c'est elle qui apporterait la preuve de présence qui
> manque aujourd'hui au distanciel.

## Q9 — Licence de la bibliothèque de composants *(front)*

*Question du dépôt front, conservée ici pour mémoire : elle ne bloque aucun lot de l'API.*

**Contexte.** L'agenda est une pièce centrale du produit et repose sur une bibliothèque
commerciale. Une clé de licence est actuellement présente **dans les fichiers de
configuration versionnés** du front existant.

| Option | Conséquence |
|---|---|
| **Conserver, licence en règle** *(proposé)* | l'agenda fonctionne, coût annuel |
| Alternative libre | pas de licence, mais réécriture de l'agenda et perte de fonctionnalités |
| Agenda maison | plusieurs semaines de travail pour un résultat inférieur |

> **Proposition :** conserver, en s'assurant que la licence couvre l'usage — et **sortir la
> clé du dépôt** pour la passer en variable d'environnement. Une clé de licence versionnée est
> un problème indépendamment du choix de bibliothèque.

## Q10 — Frais d'annulation anticipée

**Contexte.** Le prestataire ne restitue pas ses frais sur un remboursement. Sur une
annulation anticipée, la plateforme les absorbe.

| Option | Conséquence |
|---|---|
| **La plateforme absorbe** *(proposé)* | quelques euros perdus par annulation, confiance préservée |
| Retenir les frais | l'élève reçoit un montant « bizarre », difficile à annoncer d'avance |
| Frais d'annulation forfaitaires | punit la bonne foi, décourage la réservation |

> **Proposition :** ne rien changer. Retenir les frais rendrait la prévisualisation
> impossible : ils ne sont pas connus au moment du clic.
>
> **Indicateur de révision :** si le taux d'annulation anticipée devenait significatif, le
> coût cumulé pourrait justifier un forfait.

---

## Q11 — Remboursement partiel d'un litige : qui porte la commission ? — **tranchée**

> **Tranchée le 2026-09-17**, sur la proposition par défaut. Voir
> [ADR-0013](../40-decisions/ADR-0013-litige-partiel-a-charge-du-prof.md).

**Contexte.** Les [règles métier](../20-specs-fonctionnelles/01-regles-metier.md#résolution-dun-litige)
retirent au professeur `min(R, part professeur)` : la plateforme garde sa commission entière.
Le code retirait `R × (1 − commission)` : la plateforme rendait sa commission au prorata.

| Option | Conséquence |
|---|---|
| **Spec** *(proposé)* | une issue partielle à 100 % donne exactement le résultat d'une issue « élève » ; le professeur porte tout le remboursement jusqu'à sa part |
| Code actuel | la plateforme partage le coût ; deux façons d'exprimer la même décision donnent deux montants |

> **Proposition :** aligner le code sur la spec, dans la politique de résolution extraite au
> [lot 3](01-roadmap-refactoring.md#lot-3--structure), avec un test sur le cas « partiel à
> 100 % ».

## Q12 — Garder l'API et le front dans deux dépôts ?

**Contexte.** Le client TypeScript du front est généré depuis la description OpenAPI de
l'API. Dans deux dépôts, rien ne signale qu'un changement de contrat n'a pas été répercuté.
NewBonProf a choisi un dépôt unique pour cette raison.

| Option | Conséquence |
|---|---|
| **Deux dépôts, contrôle en intégration continue** *(proposé)* | la chaîne du front régénère le client depuis l'API de recette et échoue si le résultat diffère de ce qui est versionné |
| Dépôt unique | synchronisation garantie, mais migration d'historique et de chaînes de déploiement |

> **Proposition :** garder deux dépôts pendant le refactoring et ajouter le contrôle ; revoir
> la question au [lot 7](01-roadmap-refactoring.md#lot-7--contrat-dapi), qui renomme les routes.

## Q13 — Notifier l'élève d'une nouvelle carte de suivi ?

**Contexte.** Les [tableaux de suivi](../10-user-stories/E20-suivi-de-cours.md) sont en
lecture seule pour l'élève. Sans signal, il ne sait pas qu'un professeur lui a ajouté du
travail ; avec un signal par carte, un professeur qui prépare un tableau en dix cartes produit
dix notifications.

| Option | Conséquence |
|---|---|
| **Aucune notification en v1** *(proposé)* | rien à construire ; l'élève découvre les cartes en ouvrant son tableau |
| Une notification par carte créée | simple, mais bruyant à la mise en place d'un tableau |
| Un résumé groupé (par tableau, au plus une fois par jour) | utile et sobre, mais demande une tâche planifiée |

> **Proposition :** aucune notification en v1 ; revoir après usage réel, en privilégiant le
> résumé groupé.

## Q14 — Que devient un tableau de suivi quand un compte est supprimé ?

**Contexte.** Un tableau porte des notes nominatives sur un élève, écrites par un professeur.
L'effacement d'un compte ([E19](../10-user-stories/E19-transverse.md)) doit dire ce qu'il en
advient, d'un côté comme de l'autre.

| Option | Conséquence |
|---|---|
| **Suppression définitive des tableaux du compte effacé** *(proposé)* | conforme au droit à l'effacement ; le professeur perd son suivi si l'élève part |
| Anonymisation de l'élève, tableau conservé pour le professeur | le professeur garde ses notes, mais leur contenu peut rester identifiant |

> **Proposition :** suppression définitive, en cascade, dans les deux sens. Rien dans un
> tableau n'est une trace légale ou financière à conserver.

## Questions tranchées avant ce document

L'ancien `userStory.md` listait des questions ouvertes sur les remboursements. Toutes ont été
tranchées et implémentées ; elles sont reprises ici pour mémoire :

| Question | Décision | Où |
|---|---|---|
| Portée d'un remboursement | une réservation, jamais la commande entière | [politique d'annulation](../20-specs-fonctionnelles/05-politique-annulation.md) |
| Qui déclenche, et comment | l'élève ou le professeur par l'annulation, l'administrateur par l'arbitrage ; exécution différée par tâche | [E09](../10-user-stories/E09-annulation-remboursement.md) |
| Fenêtre et frais | barème à 48 h, commission retenue en annulation tardive, refus après le début du cours | [politique d'annulation](../20-specs-fonctionnelles/05-politique-annulation.md) |
| Avant ou après le cours | après le début : litige uniquement | [E11](../10-user-stories/E11-litiges.md) |
| Après versement | pas de *Transfer Reversal* : écriture `CLAWBACK`, recouvrée par compensation | [ADR-0006](../40-decisions/ADR-0006-dette-prof-compensation.md) |
| Représentation au grand livre | écriture négative `REFUND` adossée au crédit ; la dette se reconnaît au **type** | [invariant I8](../20-specs-fonctionnelles/01-regles-metier.md#i8--cest-le-type-pas-le-signe-qui-fait-la-dette) |

## Comment trancher

1. Chaque question tranchée devient un **ADR**, ou modifie un ADR existant.
2. Les specs concernées sont mises à jour dans la même contribution.
3. La question est retirée de ce document, avec un lien vers l'ADR qui la remplace.
4. Une question laissée sans réponse à l'ouverture du lot qui en dépend est arbitrée **sur la
   proposition par défaut** — l'inaction ne bloque pas le refactoring.
