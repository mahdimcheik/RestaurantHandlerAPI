using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Commentaires d'articles : écrits par tout compte connecté, publiés tout de suite, sur un seul
/// niveau de réponse. La modération est a posteriori : l'auteur ou un admin supprime, un admin
/// masque.
/// </summary>
public class ArticleCommentService(MainContext context, StorageService storage, HybridCache cache)
{
    /// <summary>Page de commentaires racine (du plus récent au plus ancien), chacun avec ses réponses.</summary>
    public async Task<Response<List<ArticleCommentDetails>>> ListAsync(Guid articleId, CustomTableState query)
    {
        if (!await IsPublishedAsync(articleId))
            return Fail<List<ArticleCommentDetails>>(404, "Article introuvable");

        var rows = query.Rows is > 0 and <= 50 ? query.Rows : 20;
        var roots = context.ArticleComments.AsNoTracking().Where(c => c.ArticleId == articleId && c.ParentId == null);

        var total = await roots.CountAsync();
        var page = await roots
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip(Math.Max(0, query.First))
            .Take(rows)
            .ToListAsync();

        var rootIds = page.Select(c => c.Id).ToList();
        var replies = await context
            .ArticleComments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.ParentId != null && rootIds.Contains(c.ParentId.Value))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync();
        var repliesByParent = replies.ToLookup(r => r.ParentId!.Value);

        var data = page
            .Select(root =>
            {
                var details = Resolve(new ArticleCommentDetails(root));
                details.Replies = repliesByParent[root.Id].Select(r => Resolve(new ArticleCommentDetails(r))).ToList();
                return details;
            })
            .ToList();

        return new Response<List<ArticleCommentDetails>>
        {
            Status = 200,
            Message = "Commentaires récupérés avec succès",
            Data = data,
            Count = total,
        };
    }

    public async Task<Response<ArticleCommentDetails>> CreateAsync(
        Guid articleId,
        ArticleCommentCreate dto,
        CallerIdentity? caller
    )
    {
        var user = CheckUser.GetUser(caller, context);
        if (user is null)
            return Fail<ArticleCommentDetails>(401, "Utilisateur non authentifié");

        if (!await IsPublishedAsync(articleId))
            return Fail<ArticleCommentDetails>(404, "Article introuvable");

        var content = dto.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
            return Fail<ArticleCommentDetails>(400, "Le commentaire est vide");
        if (content.Length > HardCode.ARTICLE_COMMENT_MAX_LENGTH)
            return Fail<ArticleCommentDetails>(400, $"Le commentaire ne peut dépasser {HardCode.ARTICLE_COMMENT_MAX_LENGTH} caractères");

        if (dto.ParentId is { } parentId)
        {
            var parent = await context
                .ArticleComments.AsNoTracking()
                .Where(c => c.Id == parentId && c.ArticleId == articleId)
                .Select(c => new { c.ParentId })
                .FirstOrDefaultAsync();
            if (parent is null)
                return Fail<ArticleCommentDetails>(404, "Commentaire parent introuvable");
            if (parent.ParentId is not null)
                return Fail<ArticleCommentDetails>(400, "On ne peut répondre qu'à un commentaire, pas à une réponse");
        }

        var comment = new ArticleComment
        {
            Id = Guid.NewGuid(),
            ArticleId = articleId,
            UserId = user.Id,
            ParentId = dto.ParentId,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.ArticleComments.Add(comment);
        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.ARTICLES_TAG);

        comment.User = user;
        return new Response<ArticleCommentDetails>
        {
            Status = 201,
            Message = "Commentaire publié",
            Data = Resolve(new ArticleCommentDetails(comment)),
        };
    }

    /// <summary>Suppression définitive, par l'auteur ou un admin ; les réponses partent avec.</summary>
    public async Task<Response<object>> DeleteAsync(Guid commentId, CallerIdentity? caller)
    {
        if (caller is null)
            return Fail<object>(401, "Utilisateur non authentifié");

        var comment = await context.ArticleComments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            return Fail<object>(404, "Commentaire introuvable");
        if (comment.UserId != caller.UserId && !caller.IsAdmin)
            return Fail<object>(403, "Vous ne pouvez supprimer que vos propres commentaires");

        context.ArticleComments.Remove(comment);
        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.ARTICLES_TAG);

        return new Response<object> { Status = 200, Message = "Commentaire supprimé" };
    }

    /// <summary>Masquage par un admin : le commentaire (et ses réponses) disparaît des lectures.</summary>
    public async Task<Response<object>> HideAsync(Guid commentId)
    {
        var comment = await context.ArticleComments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            return Fail<object>(404, "Commentaire introuvable");

        comment.ArchivedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        await cache.RemoveByTagAsync(CacheKeys.ARTICLES_TAG);

        return new Response<object> { Status = 200, Message = "Commentaire masqué" };
    }

    private Task<bool> IsPublishedAsync(Guid articleId) =>
        context.Articles.AnyAsync(a => a.Id == articleId && a.Status == ArticleStatus.Published);

    private ArticleCommentDetails Resolve(ArticleCommentDetails details)
    {
        if (!string.IsNullOrEmpty(details.AuthorImgUrl))
            details.AuthorImgUrl = storage.Url(details.AuthorImgUrl);
        return details;
    }

    private static Response<T> Fail<T>(int status, string message) => new() { Status = status, Message = message };
}
