namespace RestaurantHandler.Infrastructure.Errors;

/// <summary>
/// Refus métier prévu, traduit en réponse <c>ProblemDetails</c> par
/// <see cref="BusinessExceptionHandler"/>. Son message est destiné à l'utilisateur : il est
/// renvoyé tel quel, contrairement à celui d'une exception inattendue.
/// </summary>
public abstract class BusinessException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

/// <summary>La demande est mal formée ou hors bornes (400).</summary>
public sealed class BusinessRuleException(string message) : BusinessException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>La ressource n'existe pas, ou pas pour l'appelant (404).</summary>
public sealed class ResourceNotFoundException(string message) : BusinessException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>L'état actuel de la ressource interdit l'opération (409).</summary>
public sealed class BusinessConflictException(string message) : BusinessException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>L'appelant n'a pas le droit d'agir sur cette ressource (403).</summary>
public sealed class BusinessForbiddenException(string message) : BusinessException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>Quota métier atteint, distinct de la limite technique par minute (429).</summary>
public sealed class BusinessQuotaException(string message) : BusinessException(message)
{
    public override int StatusCode => StatusCodes.Status429TooManyRequests;
}
