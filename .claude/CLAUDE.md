# RestaurantHandler

Application de commande d'**un seul** fast-food à emporter : click & collect payé **uniquement
par carte en ligne** (SumUp), écran cuisine (KDS) temps réel, statistiques. Ni comptoir, ni
paiement au retrait, ni annulation par le client (ADR-0013). Développement solo, ouverture visée sous 3 mois.

**Source de vérité : [`docs/README.md`](../docs/README.md).** Avant toute fonctionnalité, lire
la story concernée (`docs/10-cahier-des-charges/`), les règles métier
(`docs/20-regles-metier/`) et le jalon en cours
(`docs/50-feuille-de-route/01-feuille-de-route.md`). Après : mettre à jour l'**État** des
stories et lancer `./docs/check-docs.sh`.

## Points non négociables

- **Rôles :** `Client` (défaut, seul rôle public, compte obligatoire pour commander) et
  `Manager` (tout le personnel). Seul un Manager crée un Manager (`POST /Users`) ; le Manager
  initial vient de `ADMIN_EMAIL` / `ADMIN_PASSWORD` (`AdminAccountsSeeder`).
- **Paiement en ligne seul** (ADR-0013, remplace ADR-0006 et ADR-0010) : aucune commande ni
  aucun encaissement sur place dans l'app, qui n'est pas une caisse. Le client n'annule pas : il
  appelle le restaurant ou le demande sur place ; seul un Manager annule (remboursement SumUp).
- **Argent :** montants en centimes (`int`), total **toujours recalculé côté serveur**,
  snapshots nom/prix/TVA dans les lignes de commande, catalogue archivé jamais supprimé
  (ADR-0008). Notifications SumUp relues et idempotentes (ADR-0007).
- **États de commande :** voir `docs/20-regles-metier/02-machine-etats-commande.md` ; toute
  transition non listée est refusée.
- **Architecture :** contrôleurs sans logique → services → EF Core ; règles pures et testées
  dans `Domain/Policies/` ; aucune entité exposée ; autorisation sur l'opération, refus par
  défaut (ADR-0001).
- **Langue :** français uniquement ; la carte n'a pas de `Translations` (ADR-0011).

## Traductions `jsonb` (socle)

Les référentiels du socle (`StatusAccount`, `Notification`) dérivent de
`TranslatableModelOption` (`Domain/Entities/BaseModel.cs`) : colonne `Translations`
(`Dictionary<string,string>` en `jsonb`, clé = code langue minuscule), configurée une fois
dans `MainContext.OnModelCreating` (`EnableDynamicJson()` dans `Program.cs`,
`TranslationsComparer` pour éviter les migrations fantômes). `Name` reste le fallback
canonique et la clé d'unicité. Les DTO create/update qui exposent `Translations` sont validés
par `RequiresDefaultLanguageAttribute` (clé `HardCode.DEFAULT_LANGUAGE_CODE` = `"fr"`).

## Migrations

Une seule `InitialCreate` tant que rien n'est en production ; ensuite, une migration nommée
par changement, jamais de régénération.
