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

# note à faire pour pour sumup

Pour passer le test sandbox réel
La procédure est détaillée dans docs/30-technique/03-exploitation.md. En résumé :

1. Créez un sandbox SumUp et une clé API, puis renseignez SUMUP_API_KEY et SUMUP_MERCHANT_CODE dans .env.
2. Exposez l'API en HTTPS avec un tunnel, pour que SumUp puisse joindre /payments/sumup/notifications, et mettez cette adresse dans API_BACK_URL.
3. Payez une commande avec la carte de test 4200 0000 0000 0091.
