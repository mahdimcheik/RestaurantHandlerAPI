using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class SlotsService(MainContext context, StorageService storage)
{
    public async Task<Response<SlotDetails>> AddSlotByTeacherAsync(
        SlotCreate slotDto,
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, context);
        if (user == null)
        {
            return new Response<SlotDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id);

        if (teacher == null)
        {
            return new Response<SlotDetails>
            {
                Status = 403,
                Message = "Vous devez être un enseignant pour créer des créneaux",
                Data = null,
            };
        }

        // Validation des dates
        if (slotDto.DateFrom >= slotDto.DateTo)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }

        if (slotDto.DateFrom < DateTimeOffset.UtcNow)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "Le créneau doit être dans le futur",
                Data = null,
            };
        }

        var typeExists = await context.TypeSlots.AnyAsync(t =>
            t.Id == slotDto.TypeId && t.ArchivedAt == null
        );
        if (!typeExists)
        {
            return new Response<SlotDetails>
            {
                Status = 404,
                Message = "Type de créneau non trouvé",
                Data = null,
            };
        }

        var hasOverlap = await context.Slots.AnyAsync(s =>
            s.TeacherId == teacher.Id
            && s.ArchivedAt == null
            && (
                (slotDto.DateFrom >= s.DateFrom && slotDto.DateFrom < s.DateTo)
                || (slotDto.DateTo > s.DateFrom && slotDto.DateTo <= s.DateTo)
                || (slotDto.DateFrom <= s.DateFrom && slotDto.DateTo >= s.DateTo)
            )
        );

        if (hasOverlap)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "Ce créneau chevauche un créneau existant",
                Data = null,
            };
        }

        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            DateFrom = slotDto.DateFrom,
            DateTo = slotDto.DateTo,
            TeacherId = teacher.Id,
            TypeId = slotDto.TypeId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Slots.Add(slot);
        await context.SaveChangesAsync();

        var createdSlot = await context
            .Slots.Include(s => s.Teacher)
            .ThenInclude(t => t.User)
            .Include(s => s.Type)
            .FirstAsync(s => s.Id == slot.Id);

        return new Response<SlotDetails>
        {
            Status = 201,
            Message = "Créneau créé avec succès",
            Data = new SlotDetails(createdSlot),
        };
    }

    /// <summary>
    /// Crée une série de créneaux indépendants. Une occurrence passée, invalide ou qui
    /// chevauche un créneau (existant ou du lot) est ignorée et signalée, les autres sont créées.
    /// </summary>
    public async Task<Response<SlotBatchResult>> AddSlotsBatchByTeacherAsync(
        SlotBatchCreate batchDto,
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, context);
        if (user == null)
        {
            return new Response<SlotBatchResult>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id);
        if (teacher == null)
        {
            return new Response<SlotBatchResult>
            {
                Status = 403,
                Message = "Vous devez être un enseignant pour créer des créneaux",
                Data = null,
            };
        }

        if (batchDto.Slots.Count == 0 || batchDto.Slots.Count > SlotBatchLimits.MaxSlots)
        {
            return new Response<SlotBatchResult>
            {
                Status = 400,
                Message = $"Le nombre de créneaux doit être compris entre 1 et {SlotBatchLimits.MaxSlots}",
                Data = null,
            };
        }

        var rangeFrom = batchDto.Slots.Min(s => s.DateFrom);
        var rangeTo = batchDto.Slots.Max(s => s.DateTo);
        if (rangeTo - rangeFrom > TimeSpan.FromDays(SlotBatchLimits.MaxSpanDays))
        {
            return new Response<SlotBatchResult>
            {
                Status = 400,
                Message = "La période de répétition ne peut pas dépasser 6 mois",
                Data = null,
            };
        }

        if (batchDto.Slots.Any(s => (s.TypeId ?? batchDto.TypeId) == null))
        {
            return new Response<SlotBatchResult>
            {
                Status = 400,
                Message = "L'identifiant du type de créneau est requis",
                Data = null,
            };
        }

        var typeIds = batchDto.Slots.Select(s => (s.TypeId ?? batchDto.TypeId)!.Value).Distinct().ToList();
        var knownTypes = await context.TypeSlots.CountAsync(t =>
            typeIds.Contains(t.Id) && t.ArchivedAt == null
        );
        if (knownTypes != typeIds.Count)
        {
            return new Response<SlotBatchResult>
            {
                Status = 404,
                Message = "Type de créneau non trouvé",
                Data = null,
            };
        }

        var taken = await context
            .Slots.AsNoTracking()
            .Where(s =>
                s.TeacherId == teacher.Id
                && s.ArchivedAt == null
                && s.DateFrom < rangeTo
                && s.DateTo > rangeFrom
            )
            .Select(s => new { s.DateFrom, s.DateTo })
            .ToListAsync();
        var occupied = taken.Select(s => (s.DateFrom, s.DateTo)).ToList();

        var now = DateTimeOffset.UtcNow;
        var result = new SlotBatchResult();
        var created = new List<Slot>();

        foreach (var period in batchDto.Slots.OrderBy(s => s.DateFrom))
        {
            var reason =
                period.DateFrom >= period.DateTo ? "invalid"
                : period.DateFrom < now ? "past"
                : occupied.Any(o => period.DateFrom < o.DateTo && o.DateFrom < period.DateTo)
                    ? "overlap"
                : null;

            if (reason != null)
            {
                result.Skipped.Add(
                    new SlotBatchSkipped
                    {
                        DateFrom = period.DateFrom,
                        DateTo = period.DateTo,
                        Reason = reason,
                    }
                );
                continue;
            }

            occupied.Add((period.DateFrom, period.DateTo));
            created.Add(
                new Slot
                {
                    Id = Guid.NewGuid(),
                    DateFrom = period.DateFrom,
                    DateTo = period.DateTo,
                    TeacherId = teacher.Id,
                    TypeId = (period.TypeId ?? batchDto.TypeId)!.Value,
                    CreatedAt = now,
                }
            );
        }

        if (created.Count > 0)
        {
            context.Slots.AddRange(created);
            await context.SaveChangesAsync();
        }

        result.CreatedCount = created.Count;

        return new Response<SlotBatchResult>
        {
            Status = 201,
            Message = $"{created.Count} créneau(x) créé(s), {result.Skipped.Count} ignoré(s)",
            Data = result,
        };
    }

    public async Task<Response<SlotDetails>> UpdateSlotByTeacherAsync(
        SlotUpdate slotDto,
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, context);
        if (user == null)
        {
            return new Response<SlotDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id);

        if (teacher == null)
        {
            return new Response<SlotDetails>
            {
                Status = 403,
                Message = "Vous devez être un enseignant pour créer des créneaux",
                Data = null,
            };
        }

        // Validation des dates
        if (slotDto.DateFrom >= slotDto.DateTo)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }

        if (slotDto.DateFrom < DateTimeOffset.UtcNow)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "Le créneau doit être dans le futur",
                Data = null,
            };
        }

        var typeExists = await context.TypeSlots.AnyAsync(t =>
            t.Id == slotDto.TypeId && t.ArchivedAt == null
        );
        if (!typeExists)
        {
            return new Response<SlotDetails>
            {
                Status = 404,
                Message = "Type de créneau non trouvé",
                Data = null,
            };
        }

        // Mêmes gardes que la suppression : un créneau d'autrui est traité comme inexistant,
        // et un créneau réservé ou passé ne se modifie plus.
        var slot = await context
            .Slots.Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .FirstOrDefaultAsync(s =>
                s.Id == slotDto.Id && s.ArchivedAt == null && s.TeacherId == teacher.Id
            );

        if (slot is null)
        {
            return new Response<SlotDetails>
            {
                Status = 404,
                Message = "Créneau non trouvé",
                Data = null,
            };
        }

        if (slot.Reservations != null && slot.Reservations.Any())
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "Impossible de modifier un créneau réservé",
                Data = null,
            };
        }

        if (slot.DateTo <= DateTimeOffset.UtcNow)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "Impossible de modifier un créneau passé",
                Data = null,
            };
        }

        var hasOverlap = await context.Slots.AnyAsync(s =>
            s.TeacherId == teacher.Id
            && s.Id != slotDto.Id
            && s.ArchivedAt == null
            && (
                (slotDto.DateFrom >= s.DateFrom && slotDto.DateFrom < s.DateTo)
                || (slotDto.DateTo > s.DateFrom && slotDto.DateTo <= s.DateTo)
                || (slotDto.DateFrom <= s.DateFrom && slotDto.DateTo >= s.DateTo)
            )
        );

        if (hasOverlap)
        {
            return new Response<SlotDetails>
            {
                Status = 400,
                Message = "Ce créneau chevauche un créneau existant",
                Data = null,
            };
        }

        slotDto.UpdateSlot(slot);
        await context.SaveChangesAsync();

        return new Response<SlotDetails>
        {
            Status = 201,
            Message = "Créneau créé avec succès",
            Data = new SlotDetails(slot),
        };
    }

    public async Task<Response<bool>> RemoveSlotByTeacherAsync(
        Guid slotId,
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, context);
        if (user == null)
        {
            return new Response<bool>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = false,
            };
        }

        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id);

        if (teacher == null)
        {
            return new Response<bool>
            {
                Status = 403,
                Message = "Vous devez être un enseignant pour supprimer des créneaux",
                Data = false,
            };
        }

        var slot = await context
            .Slots.Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .FirstOrDefaultAsync(s => s.Id == slotId && s.ArchivedAt == null);

        if (slot == null)
        {
            return new Response<bool>
            {
                Status = 404,
                Message = "Créneau non trouvé",
                Data = false,
            };
        }

        if (slot.TeacherId != teacher.Id)
        {
            return new Response<bool>
            {
                Status = 404,
                Message = "Créneau non trouvé",
                Data = false,
            };
        }

        if (slot.Reservations != null && slot.Reservations.Any())
        {
            return new Response<bool>
            {
                Status = 400,
                Message = "Impossible de supprimer un créneau réservé",
                Data = false,
            };
        }

        // Un créneau passé n'est plus modifiable : l'archiver effacerait des
        // données figées (cours délivrés, avis attachés via Slot.TeacherId).
        if (slot.DateTo <= DateTimeOffset.UtcNow)
        {
            return new Response<bool>
            {
                Status = 400,
                Message = "Impossible de supprimer un créneau passé",
                Data = false,
            };
        }

        slot.ArchivedAt = DateTimeOffset.UtcNow;
        slot.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();

        return new Response<bool>
        {
            Status = 200,
            Message = "Créneau supprimé avec succès",
            Data = true,
        };
    }

    public async Task<Response<List<SlotDetails>>> GetSlotsByTeacherAndDatesAsync(
        CallerIdentity? userPrincipal,
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo
    )
    {
        if (userPrincipal == null)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await context
            .Teachers.AsNoTracking()
            .Where(t => t.UserId == userPrincipal.UserId)
            .Select(t => new { t.Id, t.User.ImgUrl })
            .FirstOrDefaultAsync();

        if (teacher == null)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 403,
                Message = "Vous devez être un enseignant pour consulter des créneaux",
                Data = null,
            };
        }

        // Validation des dates
        if (dateFrom >= dateTo)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }

        var slots = await context
            .Slots.AsNoTracking()
            .Include(s => s.Teacher)
            .ThenInclude(t => t.User)
            .Include(s => s.Type)
            .Where(s =>
                s.TeacherId == teacher.Id
                && s.ArchivedAt == null
                && s.DateFrom >= dateFrom
                && s.DateTo <= dateTo
            )
            .Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .ThenInclude(r => r.Student)
            .ThenInclude(s => s.User)
            .Include(s => s.Reservations)
            .ThenInclude(r => r.Status)
            .Include(s => s.Reservations)
            .ThenInclude(r => r.Order)
            .OrderBy(s => s.DateFrom)
            .ToListAsync();

        // Distinct : une même instance d'élève signée deux fois perdrait son URL.
        var students = slots
            .SelectMany(s => s.Reservations)
            .Select(r => r.Student?.User)
            .OfType<UserApp>()
            .Distinct();
        foreach (var studentUser in students)
        {
            studentUser.ImgUrl = storage.Url(studentUser.ImgUrl);
        }

        var teacherImgUrl = storage.Url(teacher.ImgUrl);
        foreach (var slot in slots)
        {
            slot.Teacher.User.ImgUrl = teacherImgUrl;
        }

        return new Response<List<SlotDetails>>
        {
            Status = 200,
            Message = "Créneaux récupérés avec succès",
            Data = slots.Select(s => new SlotDetails(s)).ToList(),
            Count = slots.Count,
        };
    }

    public async Task<Response<List<SlotDetails>>> GetSlotsByStudentWithTeacherAsync(
        Guid teacherId,
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CallerIdentity? principals
    )
    {
        var student = CheckUser.GetUser(principals, context);
        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);

        if (teacher == null)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 404,
                Message = "Enseignant non trouvé",
                Data = null,
            };
        }

        if (dateFrom >= dateTo)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }
        var minDateFrom = dateFrom > DateTimeOffset.UtcNow ? dateFrom : DateTimeOffset.UtcNow;
        var slots = await context
            .Slots.AsNoTracking()
            .Include(s => s.Teacher)
            .ThenInclude(t => t.User)
            .Include(s => s.Type)
            .Where(s =>
                s.TeacherId == teacherId
                && s.ArchivedAt == null
                && s.DateFrom >= minDateFrom
                && s.DateTo <= dateTo
                && !s.Reservations.Any(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                ) // Seulement les créneaux disponibles
            )
            .OrderBy(s => s.DateFrom)
            .Select(s => new SlotDetails(s, false))
            .ToListAsync();

        if (student is null)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 200,
                Message = "Créneaux disponibles récupérés avec succès",
                Data = slots,
                Count = slots.Count,
            };
        }

        var studentReservations = await context
            .Slots.Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .ThenInclude(r => r.Cursus)
            .Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .ThenInclude(r => r.Status)
            .Include(s => s.Teacher)
            .ThenInclude(t => t.User)
            .Include(s => s.Type)
            .Where(s =>
                s.Reservations.Any(r =>
                    r.StudentId == student.Id
                    && r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .Select(s => new SlotDetails(s, true))
            .ToListAsync();

        try
        {
            var teachers = studentReservations.Select(x => x.Teacher).Distinct().ToList();
            foreach (var t in teachers)
            {
                if (t?.User?.ImgUrl is not null && !string.IsNullOrEmpty(t?.User?.ImgUrl))
                {
                    t.User.ImgUrl = storage.Url(t.User.ImgUrl);
                }
            }
        }
        catch { }

        slots.AddRange(studentReservations);

        return new Response<List<SlotDetails>>
        {
            Status = 200,
            Message = "Créneaux disponibles récupérés avec succès",
            Data = slots,
            Count = slots.Count,
        };
    }

}
