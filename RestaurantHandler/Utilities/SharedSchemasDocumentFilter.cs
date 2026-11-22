using RestaurantHandler.Models.Developpement;

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Publie dans le schéma OpenAPI les types que le front consomme sans qu'aucune opération ne
/// les expose (énumérations, messages SignalR). Le client Angular est généré depuis ce schéma.
/// </summary>
public sealed class SharedSchemasDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context) =>
        context.SchemaGenerator.GenerateSchema(typeof(GenApi), context.SchemaRepository);
}
