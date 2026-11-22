# RestaurantHandler

Application de commande d'un fast-food à emporter : click & collect avec paiement en ligne
(SumUp) ou au retrait, saisie au comptoir, écran cuisine (KDS) temps réel, statistiques.

- **API** : .NET 10, EF Core / PostgreSQL, SignalR, Hangfire — dossier `RestaurantHandler/`.
- **Front** : PWA Angular mobile-first (dépôt séparé, à créer — cf. feuille de route J4).

## Documentation

Tout est dans [`docs/`](docs/README.md) : vision, cahier des charges (user stories), règles
métier, technique, décisions (ADR) et **[feuille de route](docs/50-feuille-de-route/01-feuille-de-route.md)**.

## Démarrage local

1. PostgreSQL accessible et fichier `.env` renseigné (variables listées dans
   [exploitation](docs/30-technique/03-exploitation.md#configuration)).
2. `dotnet tool restore`
3. `dotnet run --project RestaurantHandler` — les migrations et le compte Manager initial
   (`ADMIN_EMAIL` / `ADMIN_PASSWORD`) sont appliqués au démarrage ; Swagger sur `/swagger`.

Vérifier la cohérence de la doc : `./docs/check-docs.sh`.
