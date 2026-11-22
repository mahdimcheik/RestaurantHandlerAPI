using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Tableaux de suivi (E20, ADR-0014). Le professeur écrit sur ses tableaux ; l'élève lit ceux
/// ouverts pour lui tant qu'ils ne sont pas archivés ; tout autre appelant, administration
/// comprise, reçoit un 404. Listes et cartes sont toujours résolues à travers leur tableau.
/// </summary>
public class BoardService(MainContext context, StorageService storage, RichTextSanitizer sanitizer)
{
    private const int DefaultRows = 20;
    private const int MaxRows = 100;

    // ---- Mes élèves -------------------------------------------------------------------------

    /// <summary>Élèves ayant au moins une réservation sur un créneau du professeur, une fois chacun.</summary>
    public async Task<Response<List<MyStudentDetails>>> SearchMyStudentsAsync(
        CustomTableState query,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);

        var students = context.Students.Where(s =>
            context.Reservations.Any(r => r.StudentId == s.Id && r.Slot!.TeacherId == me)
        );

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            students = students.Where(s =>
                EF.Functions.ILike(s.User!.FirstName + " " + s.User.LastName, $"%{search}%")
                || EF.Functions.ILike(s.User.LastName + " " + s.User.FirstName, $"%{search}%")
            );
        }

        var count = await students.CountAsync();

        var projected = students.Select(s => new MyStudentDetails
        {
            Id = s.Id,
            FirstName = s.User!.FirstName,
            LastName = s.User.LastName,
            ImgUrl = s.User.ImgUrl,
            ActiveBoardCount = context.Boards.Count(b =>
                b.TeacherId == me && b.StudentId == s.Id && b.ArchivedAt == null
            ),
        });

        var sort = query.Sorts?.FirstOrDefault(s => s.Order != 0);
        var descending = sort?.Order < 0;
        projected = sort?.Field switch
        {
            "firstName" => descending
                ? projected.OrderByDescending(s => s.FirstName).ThenByDescending(s => s.LastName).ThenBy(s => s.Id)
                : projected.OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ThenBy(s => s.Id),
            "activeBoardCount" => descending
                ? projected.OrderByDescending(s => s.ActiveBoardCount).ThenBy(s => s.LastName).ThenBy(s => s.Id)
                : projected.OrderBy(s => s.ActiveBoardCount).ThenBy(s => s.LastName).ThenBy(s => s.Id),
            _ => descending
                ? projected.OrderByDescending(s => s.LastName).ThenByDescending(s => s.FirstName).ThenBy(s => s.Id)
                : projected.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id),
        };

        var results = await projected.Skip(Math.Max(query.First, 0)).Take(Rows(query)).ToListAsync();
        results.ForEach(HydrateAvatar);

        return new Response<List<MyStudentDetails>>
        {
            Status = 200,
            Message = "Élèves récupérés avec succès",
            Data = results,
            Count = count,
        };
    }

    // ---- Tableaux ---------------------------------------------------------------------------

    /// <summary>
    /// Tableaux de l'appelant : ceux qu'il a ouverts (professeur) ou ceux ouverts pour lui
    /// (élève, jamais archivés). Filtres : <c>archived</c>, <c>studentId</c>, <c>teacherId</c> ;
    /// recherche sur le titre et les noms ; tri par dernière modification.
    /// </summary>
    public async Task<Response<List<BoardSummary>>> SearchBoardsAsync(
        CustomTableState query,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);
        var archived = FilterBool(query, "archived");

        var boards = archived
            ? context.Boards.Where(b => b.TeacherId == me && b.ArchivedAt != null)
            : context.Boards.Where(b =>
                (b.TeacherId == me || b.StudentId == me) && b.ArchivedAt == null
            );

        if (FilterGuid(query, "studentId") is { } studentId)
            boards = boards.Where(b => b.StudentId == studentId);
        if (FilterGuid(query, "teacherId") is { } teacherId)
            boards = boards.Where(b => b.TeacherId == teacherId);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            boards = boards.Where(b =>
                EF.Functions.ILike(b.Title, $"%{search}%")
                || EF.Functions.ILike(
                    b.Student!.User!.FirstName + " " + b.Student.User.LastName,
                    $"%{search}%"
                )
                || EF.Functions.ILike(
                    b.Teacher!.User!.FirstName + " " + b.Teacher.User.LastName,
                    $"%{search}%"
                )
            );
        }

        var count = await boards.CountAsync();

        var results = await boards
            .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip(Math.Max(query.First, 0))
            .Take(Rows(query))
            .Select(b => new BoardSummary
            {
                Id = b.Id,
                Title = b.Title,
                Student = new BoardPerson
                {
                    Id = b.StudentId,
                    FirstName = b.Student!.User!.FirstName,
                    LastName = b.Student.User.LastName,
                    ImgUrl = b.Student.User.ImgUrl,
                },
                Teacher = new BoardPerson
                {
                    Id = b.TeacherId,
                    FirstName = b.Teacher!.User!.FirstName,
                    LastName = b.Teacher.User.LastName,
                    ImgUrl = b.Teacher.User.ImgUrl,
                },
                ActiveCardCount = context.BoardCards.Count(c =>
                    c.List!.BoardId == b.Id
                    && c.ArchivedAt == null
                    && (b.TeacherId == me || c.SharedAt != null)
                ),
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                ArchivedAt = b.ArchivedAt,
            })
            .ToListAsync();

        foreach (var board in results)
        {
            HydrateAvatar(board.Student);
            HydrateAvatar(board.Teacher);
        }

        return new Response<List<BoardSummary>>
        {
            Status = 200,
            Message = "Tableaux récupérés avec succès",
            Data = results,
            Count = count,
        };
    }

    /// <summary>
    /// Tableau entier, listes et cartes actives dans l'ordre, sans description complète. L'élève
    /// ne reçoit que les cartes montrées : ses positions peuvent avoir des trous.
    /// </summary>
    public async Task<Response<BoardDetails>> GetBoardAsync(Guid boardId, CallerIdentity? caller)
    {
        var me = RequireCaller(caller);
        var board =
            await ReadableBoards(me)
                .Where(b => b.Id == boardId)
                .Select(b => new BoardDetails
                {
                    Id = b.Id,
                    Title = b.Title,
                    Student = new BoardPerson
                    {
                        Id = b.StudentId,
                        FirstName = b.Student!.User!.FirstName,
                        LastName = b.Student.User.LastName,
                        ImgUrl = b.Student.User.ImgUrl,
                    },
                    Teacher = new BoardPerson
                    {
                        Id = b.TeacherId,
                        FirstName = b.Teacher!.User!.FirstName,
                        LastName = b.Teacher.User.LastName,
                        ImgUrl = b.Teacher.User.ImgUrl,
                    },
                    UpdatedAt = b.UpdatedAt,
                    ArchivedAt = b.ArchivedAt,
                })
                .FirstOrDefaultAsync()
            ?? throw BoardNotFound();
        var isOwner = board.Teacher.Id == me;

        HydrateAvatar(board.Student);
        HydrateAvatar(board.Teacher);

        var cards = (
            await CardRows(context.BoardCards.Where(c =>
                    c.List!.BoardId == boardId
                    && c.ArchivedAt == null
                    && (isOwner || c.SharedAt != null)
                ))
                .ToListAsync()
        )
            .Select(ToCard<BoardCardTile>)
            .ToLookup(c => c.BoardListId);

        board.Lists = await context
            .BoardLists.Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Position)
            .Select(l => new BoardListDetails
            {
                Id = l.Id,
                Title = l.Title,
                Position = l.Position,
            })
            .ToListAsync();
        foreach (var list in board.Lists)
            list.Cards = cards[list.Id].OrderBy(c => c.Position).ToList();

        return new Response<BoardDetails>
        {
            Status = 200,
            Message = "Tableau récupéré avec succès",
            Data = board,
        };
    }

    /// <summary>
    /// Ouvre un tableau pour un élève qui partage au moins une réservation avec le professeur,
    /// avec les listes par défaut.
    /// </summary>
    public async Task<Response<BoardDetails>> CreateBoardAsync(BoardCreate dto, CallerIdentity? caller)
    {
        var me = RequireCaller(caller);

        var student = await context
            .Students.Where(s => s.Id == dto.StudentId)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.User!.StatusId,
                s.User.ArchivedAt,
            })
            .FirstOrDefaultAsync();

        if (
            student is null
            || student.ArchivedAt != null
            || student.StatusId == HardCode.ACCOUNT_BANNED
            || !await ReservationAccess.SharesReservationAsync(context, student.UserId, me)
        )
        {
            throw new BusinessRuleException(
                "studentId : aucune réservation commune avec cet élève"
            );
        }

        var board = new Board
        {
            TeacherId = me,
            StudentId = student.Id,
            Title = dto.Title.Trim(),
            Lists = HardCode
                .BOARD_DEFAULT_LISTS.Select((title, index) => new BoardList
                {
                    Title = title,
                    Position = index,
                })
                .ToList(),
        };
        context.Boards.Add(board);
        await context.SaveChangesAsync();

        var created = await GetBoardAsync(board.Id, caller);
        created.Status = 201;
        created.Message = "Tableau créé avec succès";
        return created;
    }

    public async Task<Response<BoardSummary>> RenameBoardAsync(
        Guid boardId,
        BoardUpdate dto,
        CallerIdentity? caller
    )
    {
        var board = await WritableBoardAsync(boardId, caller);
        board.Title = dto.Title.Trim();
        board.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        return await SummaryAsync(board.Id, "Tableau renommé avec succès");
    }

    /// <summary>Archiver garde listes et cartes ; l'élève ne voit plus le tableau.</summary>
    public async Task<Response<BoardSummary>> SetBoardArchivedAsync(
        Guid boardId,
        bool archived,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);
        var board =
            await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.TeacherId == me)
            ?? throw BoardNotFound();

        if ((board.ArchivedAt != null) != archived)
        {
            board.ArchivedAt = archived ? DateTimeOffset.UtcNow : null;
            board.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        return await SummaryAsync(
            board.Id,
            archived ? "Tableau archivé avec succès" : "Tableau désarchivé avec succès"
        );
    }

    // ---- Listes ------------------------------------------------------------------------------

    /// <summary>Nouvelle liste en dernière position, dans la limite de <see cref="HardCode.LIST_MAX_PER_BOARD"/>.</summary>
    public async Task<Response<BoardListDetails>> AddListAsync(
        Guid boardId,
        BoardListCreate dto,
        CallerIdentity? caller
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        var board = await LockWritableBoardAsync(boardId, caller);

        var count = await context.BoardLists.CountAsync(l => l.BoardId == board.Id);
        if (count >= HardCode.LIST_MAX_PER_BOARD)
            throw new BusinessConflictException(
                $"Un tableau compte au plus {HardCode.LIST_MAX_PER_BOARD} listes"
            );

        var list = new BoardList
        {
            BoardId = board.Id,
            Title = dto.Title.Trim(),
            Position = count,
        };
        context.BoardLists.Add(list);
        Touch(board);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return new Response<BoardListDetails>
        {
            Status = 201,
            Message = "Liste ajoutée avec succès",
            Data = new BoardListDetails
            {
                Id = list.Id,
                Title = list.Title,
                Position = list.Position,
            },
        };
    }

    public async Task<Response<BoardListDetails>> RenameListAsync(
        Guid boardId,
        Guid listId,
        BoardListUpdate dto,
        CallerIdentity? caller
    )
    {
        var board = await WritableBoardAsync(boardId, caller);
        var list = await ListOfAsync(board.Id, listId);

        list.Title = dto.Title.Trim();
        list.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(board);
        await context.SaveChangesAsync();

        return new Response<BoardListDetails>
        {
            Status = 200,
            Message = "Liste renommée avec succès",
            Data = new BoardListDetails
            {
                Id = list.Id,
                Title = list.Title,
                Position = list.Position,
            },
        };
    }

    /// <summary>Ordre complet : chaque liste du tableau exactement une fois, positions réécrites de 0 à n-1.</summary>
    public async Task<Response<List<BoardListDetails>>> ReorderListsAsync(
        Guid boardId,
        BoardListOrder dto,
        CallerIdentity? caller
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        var board = await LockWritableBoardAsync(boardId, caller);

        var lists = await context.BoardLists.Where(l => l.BoardId == board.Id).ToListAsync();
        if (
            dto.ListIds.Count != lists.Count
            || dto.ListIds.Distinct().Count() != lists.Count
            || !lists.All(l => dto.ListIds.Contains(l.Id))
        )
        {
            throw new BusinessRuleException("listIds : chaque liste du tableau doit figurer une fois");
        }

        var byId = lists.ToDictionary(l => l.Id);
        for (var i = 0; i < dto.ListIds.Count; i++)
            byId[dto.ListIds[i]].Position = i;
        Touch(board);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return new Response<List<BoardListDetails>>
        {
            Status = 200,
            Message = "Listes réordonnées avec succès",
            Data = lists
                .OrderBy(l => l.Position)
                .Select(l => new BoardListDetails
                {
                    Id = l.Id,
                    Title = l.Title,
                    Position = l.Position,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Supprime une liste vide de cartes actives. Ses cartes archivées passent dans la première
    /// liste restante, où leur restauration les placera.
    /// </summary>
    public async Task<Response<object>> DeleteListAsync(
        Guid boardId,
        Guid listId,
        CallerIdentity? caller
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        var board = await LockWritableBoardAsync(boardId, caller);
        var list = await ListOfAsync(board.Id, listId);

        if (await context.BoardCards.AnyAsync(c => c.BoardListId == list.Id && c.ArchivedAt == null))
            throw new BusinessConflictException("La liste contient encore des cartes");

        var others = await context
            .BoardLists.Where(l => l.BoardId == board.Id && l.Id != list.Id)
            .OrderBy(l => l.Position)
            .ToListAsync();
        if (others.Count == 0)
            throw new BusinessConflictException("Un tableau garde au moins une liste");

        await context
            .BoardCards.Where(c => c.BoardListId == list.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.BoardListId, others[0].Id));

        context.BoardLists.Remove(list);
        for (var i = 0; i < others.Count; i++)
            others[i].Position = i;
        Touch(board);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return new Response<object> { Status = 200, Message = "Liste supprimée avec succès" };
    }

    // ---- Cartes ------------------------------------------------------------------------------

    /// <summary>Carte complète, description comprise. L'élève ne voit ni les cartes archivées ni les cachées.</summary>
    public async Task<Response<BoardCardDetails>> GetCardAsync(
        Guid boardId,
        Guid cardId,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);
        var board =
            await ReadableBoards(me).FirstOrDefaultAsync(b => b.Id == boardId)
            ?? throw BoardNotFound();
        var isOwner = board.TeacherId == me;

        var card =
            await CardRows(
                    context.BoardCards.Where(c =>
                        c.Id == cardId
                        && c.List!.BoardId == board.Id
                        && (isOwner || (c.ArchivedAt == null && c.SharedAt != null))
                    )
                )
                .FirstOrDefaultAsync()
            ?? throw CardNotFound();

        var details = ToCard<BoardCardDetails>(card);
        details.Attachments = await AttachmentsOfAsync(card.Id);

        return new Response<BoardCardDetails>
        {
            Status = 200,
            Message = "Carte récupérée avec succès",
            Data = details,
        };
    }

    /// <summary>
    /// Cartes filtrées du tableau : <c>importance</c> (une ou plusieurs), <c>cursusId</c>,
    /// <c>archived</c> (professeur seulement). L'élève ne reçoit que les cartes montrées. Le
    /// filtrage ne touche à aucune position.
    /// </summary>
    public async Task<Response<List<BoardCardTile>>> SearchCardsAsync(
        Guid boardId,
        CustomTableState query,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);
        var board =
            await ReadableBoards(me).FirstOrDefaultAsync(b => b.Id == boardId)
            ?? throw BoardNotFound();
        var isOwner = board.TeacherId == me;
        var archived = isOwner && FilterBool(query, "archived");

        var cards = context.BoardCards.Where(c =>
            c.List!.BoardId == board.Id
            && (archived ? c.ArchivedAt != null : c.ArchivedAt == null)
            && (isOwner || c.SharedAt != null)
        );

        var importances = FilterImportances(query);
        if (importances.Count > 0)
            cards = cards.Where(c => importances.Contains(c.Importance));
        if (FilterGuid(query, "cursusId") is { } cursusId)
            cards = cards.Where(c => c.CursusId == cursusId);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            cards = cards.Where(c => EF.Functions.ILike(c.Title, $"%{search}%"));

        var count = await cards.CountAsync();
        var ordered = archived
            ? cards.OrderByDescending(c => c.ArchivedAt).ThenBy(c => c.Id)
            : cards.OrderBy(c => c.List!.Position).ThenBy(c => c.Position).ThenBy(c => c.Id);

        var rows = await CardRows(ordered.Skip(Math.Max(query.First, 0)).Take(Rows(query))).ToListAsync();

        return new Response<List<BoardCardTile>>
        {
            Status = 200,
            Message = "Cartes récupérées avec succès",
            Data = rows.Select(ToCard<BoardCardTile>).ToList(),
            Count = count,
        };
    }

    /// <summary>Nouvelle carte en dernière position de la liste choisie, cachée à l'élève.</summary>
    public async Task<Response<BoardCardDetails>> CreateCardAsync(
        Guid boardId,
        BoardCardCreate dto,
        CallerIdentity? caller
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        var board = await LockWritableBoardAsync(boardId, caller);
        var list = await ListOfAsync(board.Id, dto.BoardListId);
        await CheckReferencesAsync(board, dto.CursusId, dto.ReservationId);

        var card = new BoardCard
        {
            BoardListId = list.Id,
            Title = dto.Title.Trim(),
            Importance = CheckImportance(dto.Importance),
            CursusId = dto.CursusId,
            ReservationId = dto.ReservationId,
            Description = CleanDescription(dto.Description),
            Position = await context.BoardCards.CountAsync(c =>
                c.BoardListId == list.Id && c.ArchivedAt == null
            ),
            SharedAt = null,
        };
        context.BoardCards.Add(card);
        Touch(board);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return await CardResponseAsync(card.Id, 201, "Carte créée avec succès");
    }

    public async Task<Response<BoardCardDetails>> UpdateCardAsync(
        Guid boardId,
        Guid cardId,
        BoardCardUpdate dto,
        CallerIdentity? caller
    )
    {
        var board = await WritableBoardAsync(boardId, caller);
        var card = await ActiveCardOfAsync(board.Id, cardId);
        await CheckReferencesAsync(board, dto.CursusId, dto.ReservationId);

        card.Title = dto.Title.Trim();
        card.Importance = CheckImportance(dto.Importance);
        card.CursusId = dto.CursusId;
        card.ReservationId = dto.ReservationId;
        card.Description = CleanDescription(dto.Description);
        card.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(board);
        await context.SaveChangesAsync();

        return await CardResponseAsync(card.Id, 200, "Carte modifiée avec succès");
    }

    /// <summary>
    /// Déplacement atomique sous verrou du tableau : la carte quitte sa liste, entre dans la
    /// liste cible à la position demandée (bornée à la fin), et les deux listes sont renumérotées.
    /// </summary>
    public async Task<Response<BoardCardDetails>> MoveCardAsync(
        Guid boardId,
        Guid cardId,
        BoardCardMove dto,
        CallerIdentity? caller
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        var board = await LockWritableBoardAsync(boardId, caller);
        var card = await ActiveCardOfAsync(board.Id, cardId);
        var target = await ListOfAsync(board.Id, dto.BoardListId);

        var targetCards = await ActiveCardsOfListAsync(target.Id);
        targetCards.RemoveAll(c => c.Id == card.Id);
        targetCards.Insert(Math.Clamp(dto.Position, 0, targetCards.Count), card);

        if (card.BoardListId != target.Id)
        {
            var sourceCards = await ActiveCardsOfListAsync(card.BoardListId);
            sourceCards.RemoveAll(c => c.Id == card.Id);
            Renumber(sourceCards);
            card.BoardListId = target.Id;
        }
        Renumber(targetCards);
        card.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(board);
        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return await CardResponseAsync(card.Id, 200, "Carte déplacée avec succès");
    }

    /// <summary>
    /// Archiver retire la carte du tableau et recompacte sa liste ; restaurer la remet en dernier
    /// dans sa liste (la première du tableau si la sienne a été supprimée entre-temps).
    /// </summary>
    public async Task<Response<BoardCardDetails>> SetCardArchivedAsync(
        Guid boardId,
        Guid cardId,
        bool archived,
        CallerIdentity? caller
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        var board = await LockWritableBoardAsync(boardId, caller);
        var card =
            await context.BoardCards.FirstOrDefaultAsync(c =>
                c.Id == cardId && c.List!.BoardId == board.Id
            ) ?? throw CardNotFound();

        if ((card.ArchivedAt != null) != archived)
        {
            if (archived)
            {
                card.ArchivedAt = DateTimeOffset.UtcNow;
                var rest = await ActiveCardsOfListAsync(card.BoardListId);
                rest.RemoveAll(c => c.Id == card.Id);
                Renumber(rest);
            }
            else
            {
                card.ArchivedAt = null;
                card.Position = await context.BoardCards.CountAsync(c =>
                    c.BoardListId == card.BoardListId && c.ArchivedAt == null
                );
            }
            card.UpdatedAt = DateTimeOffset.UtcNow;
            Touch(board);
            await context.SaveChangesAsync();
        }
        await tx.CommitAsync();

        return await CardResponseAsync(
            card.Id,
            200,
            archived ? "Carte archivée avec succès" : "Carte restaurée avec succès"
        );
    }

    /// <summary>Montre une carte active à l'élève ou la lui cache ; sa position ne change pas.</summary>
    public async Task<Response<BoardCardDetails>> SetCardSharedAsync(
        Guid boardId,
        Guid cardId,
        bool shared,
        CallerIdentity? caller
    )
    {
        var board = await WritableBoardAsync(boardId, caller);
        var card = await ActiveCardOfAsync(board.Id, cardId);

        if ((card.SharedAt != null) != shared)
        {
            card.SharedAt = shared ? DateTimeOffset.UtcNow : null;
            card.UpdatedAt = DateTimeOffset.UtcNow;
            Touch(board);
            await context.SaveChangesAsync();
        }

        return await CardResponseAsync(
            card.Id,
            200,
            shared ? "Carte montrée à l'élève" : "Carte cachée à l'élève"
        );
    }

    // ---- Pièces jointes ------------------------------------------------------------------------

    /// <summary>
    /// Joint un fichier à une carte active (ADR-0015). Le plafond par carte est vérifié sous le
    /// verrou du tableau ; un objet déposé dont la ligne n'a pu être enregistrée est supprimé.
    /// </summary>
    public async Task<Response<BoardCardAttachmentDetails>> AddAttachmentAsync(
        Guid boardId,
        Guid cardId,
        IFormFile? file,
        CallerIdentity? caller,
        CancellationToken ct = default
    )
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var board = await LockWritableBoardAsync(boardId, caller);
        var card = await ActiveCardOfAsync(board.Id, cardId);

        if (AttachmentRules.Refusal(file, out var refusal) is { } message)
            return new Response<BoardCardAttachmentDetails> { Status = refusal, Message = message };

        var count = await context.BoardCardAttachments.CountAsync(a => a.BoardCardId == card.Id, ct);
        if (count >= HardCode.CARD_ATTACHMENT_MAX_PER_CARD)
            throw new BusinessConflictException(
                $"Une carte porte au plus {HardCode.CARD_ATTACHMENT_MAX_PER_CARD} pièces jointes"
            );

        var upload = await storage.Upload(file!, $"boards/{board.Id}/cards/{card.Id}", ct);
        if (upload.Status != 201 || upload.Data is null)
            return new Response<BoardCardAttachmentDetails> { Status = upload.Status, Message = upload.Message };

        var attachment = new BoardCardAttachment
        {
            BoardCardId = card.Id,
            TeacherId = board.TeacherId,
            FileName = Path.GetFileName(file!.FileName),
            FilePath = upload.Data,
            MimeType = StorageService.ContentTypeFor(upload.Data),
            Size = file.Length,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        context.BoardCardAttachments.Add(attachment);
        card.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(board);

        try
        {
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await storage.Delete(upload.Data, CancellationToken.None);
            throw;
        }

        return new Response<BoardCardAttachmentDetails>
        {
            Status = 201,
            Message = "Pièce jointe ajoutée avec succès",
            Data = new BoardCardAttachmentDetails
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                MimeType = attachment.MimeType,
                Size = attachment.Size,
                Url = storage.Url(attachment.FilePath),
                UploadedAt = attachment.UploadedAt,
            },
        };
    }

    /// <summary>Supprime définitivement une pièce jointe d'une carte active, objet stocké compris.</summary>
    public async Task<Response<object>> DeleteAttachmentAsync(
        Guid boardId,
        Guid cardId,
        Guid attachmentId,
        CallerIdentity? caller,
        CancellationToken ct = default
    )
    {
        var board = await WritableBoardAsync(boardId, caller);
        var card = await ActiveCardOfAsync(board.Id, cardId);
        var attachment =
            await context.BoardCardAttachments.FirstOrDefaultAsync(
                a => a.Id == attachmentId && a.BoardCardId == card.Id,
                ct
            ) ?? throw new ResourceNotFoundException("Pièce jointe introuvable");

        context.BoardCardAttachments.Remove(attachment);
        card.UpdatedAt = DateTimeOffset.UtcNow;
        Touch(board);
        await context.SaveChangesAsync(ct);
        await storage.Delete(attachment.FilePath, ct);

        return new Response<object> { Status = 200, Message = "Pièce jointe supprimée avec succès" };
    }

    // ---- Outils -----------------------------------------------------------------------------

    private static Guid RequireCaller(CallerIdentity? caller) =>
        caller?.UserId ?? throw new ResourceNotFoundException("Utilisateur non trouvé");

    private static ResourceNotFoundException BoardNotFound() => new("Tableau introuvable");

    private static ResourceNotFoundException CardNotFound() => new("Carte introuvable");

    /// <summary>
    /// À appeler dans une transaction ouverte : verrouille la ligne du tableau jusqu'à sa fin,
    /// ce qui sérialise toutes les écritures de positions sur ce tableau.
    /// </summary>
    private async Task<Board> LockWritableBoardAsync(Guid boardId, CallerIdentity? caller)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Boards\" WHERE \"Id\" = {boardId} FOR UPDATE"
        );
        return await WritableBoardAsync(boardId, caller);
    }

    private async Task<BoardList> ListOfAsync(Guid boardId, Guid listId) =>
        await context.BoardLists.FirstOrDefaultAsync(l => l.Id == listId && l.BoardId == boardId)
        ?? throw new ResourceNotFoundException("Liste introuvable");

    private async Task<BoardCard> ActiveCardOfAsync(Guid boardId, Guid cardId) =>
        await context.BoardCards.FirstOrDefaultAsync(c =>
            c.Id == cardId && c.List!.BoardId == boardId && c.ArchivedAt == null
        ) ?? throw CardNotFound();

    private Task<List<BoardCard>> ActiveCardsOfListAsync(Guid listId) =>
        context
            .BoardCards.Where(c => c.BoardListId == listId && c.ArchivedAt == null)
            .OrderBy(c => c.Position)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();

    private static void Renumber(List<BoardCard> cards)
    {
        for (var i = 0; i < cards.Count; i++)
            cards[i].Position = i;
    }

    private static void Touch(Board board) => board.UpdatedAt = DateTimeOffset.UtcNow;

    /// <summary>Le cursus est au professeur ; la séance relie ce professeur à l'élève du tableau.</summary>
    private async Task CheckReferencesAsync(Board board, Guid? cursusId, Guid? reservationId)
    {
        if (
            cursusId is { } pid
            && !await context.Cursuses.AnyAsync(p => p.Id == pid && p.TeacherId == board.TeacherId)
        )
        {
            throw new BusinessRuleException("cursusId : ce cours n'est pas le vôtre");
        }

        if (
            reservationId is { } rid
            && !await context.Reservations.AnyAsync(r =>
                r.Id == rid && r.StudentId == board.StudentId && r.Slot!.TeacherId == board.TeacherId
            )
        )
        {
            throw new BusinessRuleException(
                "reservationId : cette séance ne vous relie pas à l'élève du tableau"
            );
        }
    }

    private static BoardCardImportance CheckImportance(BoardCardImportance importance) =>
        Enum.IsDefined(importance)
            ? importance
            : throw new BusinessRuleException("importance : valeur inconnue");

    private string? CleanDescription(string? html)
    {
        var clean = sanitizer.Sanitize(html);
        if (sanitizer.TextLength(clean) > HardCode.CARD_DESCRIPTION_MAX_LENGTH)
            throw new BusinessRuleException(
                $"description : {HardCode.CARD_DESCRIPTION_MAX_LENGTH} caractères au plus"
            );
        return clean;
    }

    private async Task<Response<BoardCardDetails>> CardResponseAsync(Guid cardId, int status, string message)
    {
        var details = ToCard<BoardCardDetails>(
            await CardRows(context.BoardCards.Where(c => c.Id == cardId)).FirstAsync()
        );
        details.Attachments = await AttachmentsOfAsync(cardId);
        return new() { Status = status, Message = message, Data = details };
    }

    /// <summary>Pièces jointes d'une carte, dans l'ordre d'envoi, avec un lien signé neuf.</summary>
    private async Task<List<BoardCardAttachmentDetails>> AttachmentsOfAsync(Guid cardId)
    {
        var rows = await context
            .BoardCardAttachments.Where(a => a.BoardCardId == cardId)
            .OrderBy(a => a.UploadedAt)
            .ThenBy(a => a.Id)
            .Select(a => new { a.Id, a.FileName, a.MimeType, a.Size, a.FilePath, a.UploadedAt })
            .ToListAsync();
        return rows.Select(a => new BoardCardAttachmentDetails
            {
                Id = a.Id,
                FileName = a.FileName,
                MimeType = a.MimeType,
                Size = a.Size,
                Url = storage.Url(a.FilePath),
                UploadedAt = a.UploadedAt,
            })
            .ToList();
    }

    private static List<BoardCardImportance> FilterImportances(CustomTableState query)
    {
        if (query.Filters?.TryGetValue("importance", out var filter) != true || filter?.Value is null)
            return [];

        var raw = filter.Value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } array
            ? array.EnumerateArray().Select(e => e.ToString())
            : [filter.Value.ToString() ?? ""];

        return raw.Select(v => Enum.TryParse<BoardCardImportance>(v, true, out var i) ? (BoardCardImportance?)i : null)
            .OfType<BoardCardImportance>()
            .Distinct()
            .ToList();
    }

    /// <summary>Le professeur voit tous ses tableaux ; l'élève, ceux ouverts pour lui et actifs.</summary>
    private IQueryable<Board> ReadableBoards(Guid me) =>
        context.Boards.Where(b =>
            b.TeacherId == me || (b.StudentId == me && b.ArchivedAt == null)
        );

    /// <summary>Tableau du professeur appelant, ouvert à l'écriture : 404 sinon, 409 s'il est archivé.</summary>
    private async Task<Board> WritableBoardAsync(Guid boardId, CallerIdentity? caller)
    {
        var me = RequireCaller(caller);
        var board =
            await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.TeacherId == me)
            ?? throw BoardNotFound();
        if (board.ArchivedAt != null)
            throw new BusinessConflictException("Ce tableau est archivé");
        return board;
    }

    private async Task<Response<BoardSummary>> SummaryAsync(Guid boardId, string message)
    {
        var summary = await context
            .Boards.Where(b => b.Id == boardId)
            .Select(b => new BoardSummary
            {
                Id = b.Id,
                Title = b.Title,
                Student = new BoardPerson
                {
                    Id = b.StudentId,
                    FirstName = b.Student!.User!.FirstName,
                    LastName = b.Student.User.LastName,
                    ImgUrl = b.Student.User.ImgUrl,
                },
                Teacher = new BoardPerson
                {
                    Id = b.TeacherId,
                    FirstName = b.Teacher!.User!.FirstName,
                    LastName = b.Teacher.User.LastName,
                    ImgUrl = b.Teacher.User.ImgUrl,
                },
                ActiveCardCount = context.BoardCards.Count(c =>
                    c.List!.BoardId == b.Id && c.ArchivedAt == null
                ),
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                ArchivedAt = b.ArchivedAt,
            })
            .FirstAsync();
        HydrateAvatar(summary.Student);
        HydrateAvatar(summary.Teacher);
        return new Response<BoardSummary> { Status = 200, Message = message, Data = summary };
    }

    private sealed record CardRow(
        Guid Id,
        Guid BoardListId,
        string Title,
        BoardCardImportance Importance,
        int Position,
        Guid? CursusId,
        string? CursusName,
        Guid? ReservationId,
        DateTimeOffset? ReservationDateFrom,
        string? Description,
        int AttachmentCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset? SharedAt
    );

    private static IQueryable<CardRow> CardRows(IQueryable<BoardCard> cards) =>
        cards.Select(c => new CardRow(
            c.Id,
            c.BoardListId,
            c.Title,
            c.Importance,
            c.Position,
            c.CursusId,
            c.Cursus != null ? c.Cursus.Name : null,
            c.ReservationId,
            c.Reservation != null && c.Reservation.Slot != null
                ? c.Reservation.Slot.DateFrom
                : null,
            c.Description,
            c.Attachments.Count,
            c.CreatedAt,
            c.UpdatedAt,
            c.ArchivedAt,
            c.SharedAt
        ));

    private T ToCard<T>(CardRow c)
        where T : BoardCardTile, new()
    {
        var card = new T
        {
            Id = c.Id,
            BoardListId = c.BoardListId,
            Title = c.Title,
            Importance = c.Importance,
            Position = c.Position,
            CursusId = c.CursusId,
            CursusName = c.CursusName,
            ReservationId = c.ReservationId,
            ReservationDateFrom = c.ReservationDateFrom,
            HasDescription = !string.IsNullOrEmpty(c.Description),
            Excerpt = sanitizer.Excerpt(c.Description),
            AttachmentCount = c.AttachmentCount,
            UpdatedAt = c.UpdatedAt,
            ArchivedAt = c.ArchivedAt,
            SharedAt = c.SharedAt,
        };
        if (card is BoardCardDetails details)
        {
            details.Description = c.Description;
            details.CreatedAt = c.CreatedAt;
        }
        return card;
    }

    private static int Rows(CustomTableState query) =>
        query.Rows <= 0 ? DefaultRows : Math.Min(query.Rows, MaxRows);

    private static bool FilterBool(CustomTableState query, string key) =>
        query.Filters?.TryGetValue(key, out var filter) == true
        && bool.TryParse(filter?.Value?.ToString(), out var value)
        && value;

    private static Guid? FilterGuid(CustomTableState query, string key) =>
        query.Filters?.TryGetValue(key, out var filter) == true
        && Guid.TryParse(filter?.Value?.ToString(), out var value)
            ? value
            : null;

    private void HydrateAvatar(BoardPerson person) => person.ImgUrl = storage.Url(person.ImgUrl);
}
