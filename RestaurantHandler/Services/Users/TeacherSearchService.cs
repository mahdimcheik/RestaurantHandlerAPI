using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class TeacherSearchService
{
    private readonly MainContext _context;
    private readonly StorageService _storage;

    public TeacherSearchService(MainContext context, StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<Response<List<UserDetails>>> GetAllTeacherProfilesAsync(
        FilterTeacher filters,
        CallerIdentity? principal
    )
    {
        // Requête de FILTRE seulement : le profil complet (cursus, adresses) n'est chargé
        // qu'à la fin, pour la page retenue (cf. étape 2 plus bas).
        var query = _context
            .Users
            // Même règle que la réservation : un professeur non actif n'est pas réservable,
            // il n'a donc rien à faire dans la recherche.
            .Where(u =>
                u.Teacher != null
                && u.ArchivedAt == null
                && u.StatusId == HardCode.ACCOUNT_ACTIVE
            );

        if (filters.Long is not null && filters.Lat is not null)
        {
            var bounders = GeoHelper.GetBounders(
                filters.Lat.Value,
                filters.Long.Value,
                filters.Radius
            );
            query = query.Where(u =>
                u.Addresses.Any(a =>
                    a.TypeId == HardCode.TYPE_ADDRESS_MAIN
                    && a.Latitude >= bounders.minLat
                    && a.Latitude <= bounders.maxLat
                    && a.Longitude >= bounders.minLon
                    && a.Longitude <= bounders.maxLon
                )
            );
        }
        else
        {
            if (filters.PostalCode is not null)
            {
                query = query.Where(u =>
                        u.Addresses.Any(a =>
                            a.TypeId == HardCode.TYPE_ADDRESS_MAIN
                            && a.ZipCode.ToLower() == filters.PostalCode.ToLower()
                        )
                    );
            }

            if (filters.City is not null)
            {
                query = query.Where(u =>
                        u.Addresses.Any(a =>
                            a.TypeId == HardCode.TYPE_ADDRESS_MAIN
                            && a.City.ToLower().Contains(filters.City.ToLower())
                        )
                    );
            }
        }

        if (filters.CursusName is not null)
        {
            query = query.Where(x =>
                x.Teacher != null
                && x.Teacher.Cursuses.Any(p =>
                    p.Name.ToLower().Contains(filters.CursusName.ToLower())
                )
            );
        }

        if (filters.FullName is not null)
        {
            query = query.Where(u =>
                (u.FirstName.ToLower() + " " + u.LastName.ToLower()).Contains(
                    filters.FullName.ToLower()
                )
                || (u.LastName + " " + u.FirstName).Contains(filters.FullName)
                || u.Email.Contains(filters.FullName)
            );
        }

        if (filters.InFavorites == true)
        {
            var userId = CheckUser.GetUser(principal, _context)?.Id;
            query = query.Where(u =>
                _context.Favorites.Any(f => f.StudentId == userId && f.TeacherId == u.Id)
            );
        }

        // Catégorie et prix dans un seul Any : c'est le MÊME cursus qui doit être dans la
        // matière et dans la fourchette (« maths entre 20 et 30 € »), pas un cours de maths
        // à 50 € et un cours d'anglais à 25 €.
        var categoryIds = filters.CategoryIds ?? [];
        var priceMin = filters.PriceMin;
        var priceMax = filters.PriceMax;
        if (priceMin is not null && priceMax is not null && priceMin > priceMax)
        {
            (priceMin, priceMax) = (priceMax, priceMin);
        }

        var byCategory = categoryIds.Count > 0;
        if (byCategory || priceMin is not null || priceMax is not null)
        {
            query = query.Where(u =>
                u.Teacher.Cursuses.Any(p =>
                    (!byCategory || p.Categories.Any(c => categoryIds.Contains(c.Id)))
                    && (priceMin == null || p.Price >= priceMin)
                    && (priceMax == null || p.Price <= priceMax)
                )
            );
        }

        // Disponibilités. Un seul prédicat, TeacherAvailability.IsFree, sert les trois
        // usages ci-dessous (filtrer / trier / compter) : trois copies divergentes et
        // un prof serait listé sur une dispo non réservable, ou affiché avec un compte
        // incohérent avec son rang.
        var now = DateTimeOffset.UtcNow;
        var hasWindow =
            (filters.DateFrom is not null || filters.DateTo is not null)
            && filters.AvailableOnly;

        // Borne basse = max(DateFrom, maintenant) : un créneau passé n'est pas une
        // disponibilité, même jamais réservé — sans ce plancher un prof qui a laissé
        // traîner de vieux créneaux libres resterait premier pour toujours.
        var lowerBound = filters.DateFrom > now ? filters.DateFrom.Value : now;
        var freeSlot = TeacherAvailability.IsFree(lowerBound, filters.DateTo);

        // FILTRE — l'élève cherche une semaine précise : on ne renvoie que les profs qui
        // ont effectivement une dispo dedans, les autres n'ont rien à faire dans la
        // liste. EXISTS et non un COUNT > 0 : il s'arrête au premier créneau libre
        // trouvé au lieu de tous les compter.
        //
        // Volontairement conditionnel : sans fenêtre (listing par défaut), on continue
        // d'afficher TOUS les profs, y compris ceux qui n'ont encore aucun créneau —
        // un prof fraîchement inscrit ne doit pas être invisible sur la plateforme.
        if (hasWindow)
        {
            query = query.Where(u => u.Teacher.Slots.AsQueryable().Any(freeSlot));
        }

        // Prochaine dispo et nombre de créneaux libres de CHAQUE prof, en une seule agrégation
        // (GROUP BY TeacherId) plutôt qu'une sous-requête corrélée par prof : le tri porte sur
        // tous les profs filtrés avant la pagination, ce calcul est donc le coût dominant de la
        // recherche et doit rester un seul passage sur les créneaux libres.
        var availability = _context
            .Slots.Where(freeSlot)
            .GroupBy(sl => sl.TeacherId)
            .Select(g => new
            {
                TeacherId = g.Key,
                NextSlotAt = g.Min(sl => sl.DateFrom),
                FreeSlots = g.Count(),
            });

        // ÉTAPE 1 — trier et paginer des identifiants seulement. Charger le profil complet ici
        // ferait rejouer tout le tri pour chaque collection incluse (SplitQuery : une requête
        // par Include).
        //
        // TRI — dispo la plus proche d'abord. PostgreSQL trie ASC en NULLS LAST : les profs sans
        // dispo (possible seulement sans fenêtre, la jointure interne les ayant écartés sinon)
        // finissent en queue de liste. Clé finale déterministe (Id), sinon les ex æquo se
        // réordonnent d'une page à l'autre.
        var ranked = hasWindow
            ? from u in query
              join a in availability on u.Teacher.Id equals a.TeacherId
              select new
              {
                  u.Id,
                  NextSlotAt = (DateTimeOffset?)a.NextSlotAt,
                  // Affiché à côté du prof (« 4 créneaux libres cette semaine »).
                  FreeSlots = (int?)a.FreeSlots,
              }
            : from u in query
              join a in availability on u.Teacher.Id equals a.TeacherId into avail
              from a in avail.DefaultIfEmpty()
              select new
              {
                  u.Id,
                  NextSlotAt = (DateTimeOffset?)a.NextSlotAt,
                  // Sans fenêtre : tous les créneaux libres à venir. La carte du catalogue
                  // n'affiche « disponible » que sur ce compte — null ici la ferait passer
                  // à « aucun créneau » pour tout prof.
                  FreeSlots = (int?)a.FreeSlots,
              };

        // Total pour la pagination, compté sur `query` (filtres + EXISTS de la fenêtre) : ni la
        // jointure interne ni le tri ne changent la cardinalité, inutile d'embarquer l'agrégat.
        var count = await query.CountAsync();

        var take = filters.Row != 0 ? filters.Row ?? 10 : 10;
        var skip = filters.Row != 0 ? filters.First : 0;
        var page = await ranked
            .OrderBy(x => x.NextSlotAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        // ÉTAPE 2 — profil complet des seuls profs de la page, puis remise dans l'ordre du tri.
        var ids = page.Select(x => x.Id).ToList();
        var users = await _context
            .Users.AsNoTracking()
            .Include(u => u.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(u => u.Addresses)
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var rows = page.Where(x => users.ContainsKey(x.Id))
            .Select(x => new { User = users[x.Id], x.NextSlotAt, x.FreeSlots })
            .ToList();
        foreach (var row in rows)
        {
            if (row.User.ImgUrl is not null)
            {
                row.User.ImgUrl = _storage.Url(row.User.ImgUrl);
            }
        }

        return new Response<List<UserDetails>>
        {
            Status = 200,
            Message = "Profils enseignants récupérés avec succès",
            Data = rows.Select(x => new UserDetails(x.User, null)
                {
                    Email = "",
                    AvailableSlotsCount = x.FreeSlots,
                    NextAvailableSlotAt = x.NextSlotAt,
                })
                .ToList(),
            Count = count,
        };
    }

    /// <summary>
    /// Widget "profs les mieux notés" de la landing page (Feature 2). Contrairement à
    /// <see cref="GetAllTeacherProfilesAsync"/> (listing paginé, trié par disponibilité),
    /// ici on tire au hasard <see cref="HardCode.FEATURED_TEACHERS_COUNT"/> profs parmi le
    /// pool des <see cref="HardCode.FEATURED_TEACHERS_POOL_SIZE"/> mieux notés — la
    /// randomisation fait tourner la sélection au lieu d'épingler éternellement les 10
    /// mêmes profs, tout en restant cantonnée aux mieux notés. S'il y a moins de profs notés
    /// que de places, on complète avec les derniers inscrits, pour que le widget ne
    /// disparaisse pas tant que les avis manquent.
    /// </summary>
    public async Task<Response<List<UserDetails>>> GetFeaturedTeachersAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var freeSlot = TeacherAvailability.IsFree(now, null);

        var activeTeachers = _context
            .Users.Include(p => p.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(p => p.Addresses)
            .Where(u =>
                u.Teacher != null
                && u.ArchivedAt == null
                && u.StatusId == HardCode.ACCOUNT_ACTIVE
            );

        Task<List<FeaturedRow>> LoadAsync(IQueryable<UserApp> users) =>
            users
                .Select(u => new FeaturedRow
                {
                    User = u,
                    NextSlotAt = u
                        .Teacher.Slots.AsQueryable()
                        .Where(freeSlot)
                        .Min(s => (DateTimeOffset?)s.DateFrom),
                    FreeSlots = (int?)u.Teacher.Slots.AsQueryable().Where(freeSlot).Count(),
                })
                .ToListAsync();

        var pool = await LoadAsync(
            activeTeachers
                .Where(u => u.Teacher.ReviewsCount > 0)
                .OrderByDescending(u => u.Teacher.AverageRating)
                .ThenByDescending(u => u.Teacher.ReviewsCount)
                .Take(HardCode.FEATURED_TEACHERS_POOL_SIZE)
        );

        // Randomise uniquement s'il y a plus de candidats que de places à pourvoir —
        // sinon le pool entier est déjà la sélection, pas la peine de le mélanger.
        var selected =
            pool.Count > HardCode.FEATURED_TEACHERS_COUNT
                ? pool.OrderBy(_ => Guid.NewGuid())
                    .Take(HardCode.FEATURED_TEACHERS_COUNT)
                    .ToList()
                : pool;

        // Re-tri par note après le tirage au sort : la sélection est aléatoire, son
        // affichage reste meilleur-en-tête.
        selected = selected.OrderByDescending(x => x.User.Teacher.AverageRating).ToList();

        var missing = HardCode.FEATURED_TEACHERS_COUNT - selected.Count;
        if (missing > 0)
        {
            var selectedIds = selected.Select(x => x.User.Id).ToList();
            var newest = await LoadAsync(
                activeTeachers
                    .Where(u => !selectedIds.Contains(u.Id))
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(missing)
            );
            selected.AddRange(newest.OrderByDescending(x => x.User.CreatedAt));
        }

        foreach (var row in selected)
        {
            if (row.User.ImgUrl is not null)
            {
                row.User.ImgUrl = _storage.Url(row.User.ImgUrl);
            }
        }

        return new Response<List<UserDetails>>
        {
            Status = 200,
            Message = "Profs mis en avant récupérés avec succès",
            Data = selected
                .Select(x => new UserDetails(x.User, null)
                {
                    Email = "",
                    AvailableSlotsCount = x.FreeSlots,
                    NextAvailableSlotAt = x.NextSlotAt,
                })
                .ToList(),
        };
    }

    private sealed class FeaturedRow
    {
        public required UserApp User { get; init; }
        public DateTimeOffset? NextSlotAt { get; init; }
        public int? FreeSlots { get; init; }
    }
}
