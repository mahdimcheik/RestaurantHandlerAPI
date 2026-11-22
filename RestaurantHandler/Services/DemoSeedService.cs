using Bogus;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Seeds live-like demo data at scale using Bogus, for local browsing and load tests
/// (<c>tests/RestaurantHandler.LoadTests</c>). Reference/lookup tables are already seeded via EF
/// migrations; this only adds the data you log in as and browse. Three independent,
/// idempotent steps (chained by <see cref="SeedAllAsync"/>):
/// <list type="bullet">
///   <item><see cref="SeedUsersAsync"/> — teachers and students with a full profile
///   (main address, formations, experiences, cursuses).</item>
///   <item><see cref="SeedSlotsAsync"/> — 100 slots per demo teacher.</item>
///   <item><see cref="SeedReservationsAsync"/> — no-payment reservations on those slots.</item>
/// </list>
/// Demo accounts are <c>teacher{n}@bonprof.test</c> / <c>student{n}@bonprof.test</c>, all
/// sharing <see cref="DemoPassword"/>. Only those accounts are ever touched (see
/// <see cref="DemoUsers"/>), so real users and test fixtures are never modified.
///
/// Stripe is intentionally untouched: seeded teachers have no <c>StripeAccount</c>, so every
/// booking on them follows the no-payment flow (<c>Order.PaymentRequired == false</c>).
/// </summary>
public class DemoSeedService(MainContext context, UserManager<UserApp> userManager)
{
    public const string DemoPassword = "Password123!";
    public const string DemoEmailDomain = "@bonprof.test";
    public const string TeacherEmailPrefix = "teacher";
    public const string StudentEmailPrefix = "student";
    private const int MaxCount = 50_000;
    private const int UserBatchSize = 500;
    private const int TeacherBatchSize = 50;

    // 5 past + 5 future weeks × 10 slots/week = 100 slots per teacher (50 past, 50 future).
    private const int PastWeeks = 5;
    private const int FutureWeeks = 5;
    private const int SlotsPerWeek = 10;

    private static readonly string[] Subjects =
    [
        "Mathématiques", "Algorithmique", "Développement Web", "Bases de données",
        "Anglais", "Français", "Physique", "Chimie", "Design UX", "DevOps",
        "Machine Learning", "Comptabilité", "Marketing digital", "Guitare", "Photographie",
    ];

    private static readonly string[] Institutes =
    [
        "Université Paris Cité", "Sorbonne Université", "Université de Lyon", "INSA Toulouse",
        "Université de Bordeaux", "Aix-Marseille Université", "Université de Lille",
        "École Polytechnique", "CNAM", "Université de Strasbourg", "Conservatoire de Nantes",
    ];

    public sealed record DemoCity(string Name, string ZipCode, double Latitude, double Longitude);

    /// <summary>
    /// Cities demo addresses are drawn from (±0.05° jitter around the center). The k6 project
    /// keeps a copy in <c>tests/RestaurantHandler.LoadTests/lib/data.js</c> — keep both in sync so
    /// city / postal-code / radius searches hit seeded data.
    /// </summary>
    public static readonly DemoCity[] Cities =
    [
        new("Paris", "75011", 48.8589, 2.3800),
        new("Lyon", "69003", 45.7597, 4.8422),
        new("Marseille", "13001", 43.2965, 5.3698),
        new("Toulouse", "31000", 43.6047, 1.4442),
        new("Nice", "06000", 43.7102, 7.2620),
        new("Nantes", "44000", 47.2184, -1.5536),
        new("Strasbourg", "67000", 48.5734, 7.7521),
        new("Montpellier", "34000", 43.6108, 3.8767),
        new("Bordeaux", "33000", 44.8378, -0.5792),
        new("Lille", "59000", 50.6292, 3.0573),
        new("Rennes", "35000", 48.1173, -1.6778),
        new("Reims", "51100", 49.2583, 4.0317),
        new("Grenoble", "38000", 45.1885, 5.7245),
        new("Dijon", "21000", 47.3220, 5.0415),
        new("Angers", "49000", 47.4784, -0.5632),
    ];

    /// <summary>
    /// Ensures <c>teacher1..teacherCount</c> and <c>student1..studentCount</c> exist: calling
    /// again with bigger counts only adds the missing ones. Users are inserted in batches with
    /// a single precomputed password hash (one PBKDF2 per user would dominate at volume).
    /// Demo users left without an address by an older seeder version are backfilled.
    /// </summary>
    public async Task<object> SeedUsersAsync(int teacherCount = 50, int studentCount = 200, int seed = 12345)
    {
        teacherCount = Math.Clamp(teacherCount, 0, MaxCount);
        studentCount = Math.Clamp(studentCount, 0, MaxCount);

        var passwordHash = userManager.PasswordHasher.HashPassword(new UserApp(), DemoPassword);
        var categories = await context.CategoryCursuses.AsNoTracking().ToListAsync();

        var teachers = await SeedAccountsAsync(TeacherEmailPrefix, teacherCount, seed, passwordHash, categories);
        var students = await SeedAccountsAsync(StudentEmailPrefix, studentCount, seed, passwordHash, categories);
        var backfilled = await BackfillProfilesAsync(seed);

        return new
        {
            Password = DemoPassword,
            EmailPattern = $"{TeacherEmailPrefix}{{n}}{DemoEmailDomain} / {StudentEmailPrefix}{{n}}{DemoEmailDomain}",
            Teachers = new { Created = teachers.Created, Skipped = teachers.Skipped },
            Students = new { Created = students.Created, Skipped = students.Skipped },
            ProfilesBackfilled = backfilled,
        };
    }

    /// <summary>
    /// Adds 100 slots to each demo teacher that has none yet. Idempotent via the "already has
    /// slots" guard, so teachers added by a later <see cref="SeedUsersAsync"/> get theirs too.
    /// </summary>
    public async Task<object> SeedSlotsAsync(int seed = 12345)
    {
        Guid[] slotTypes = [HardCode.TYPE_SLOT_PRESENTIAL, HardCode.TYPE_SLOT_VISIO];

        var teacherIds = await DemoUsers(context.Users, TeacherEmailPrefix)
            .Where(u => u.Teacher != null && !u.Teacher.Slots.Any())
            .Select(u => u.Id)
            .ToListAsync();

        int slotsCreated = 0;
        foreach (var batch in teacherIds.Chunk(TeacherBatchSize))
        {
            foreach (var teacherId in batch)
            {
                var faker = FakerFor(seed, teacherId);
                slotsCreated += AddSlotsForTeacher(faker, teacherId, slotTypes);
            }
            await SaveBatchAsync();
        }

        return new
        {
            TeachersSeeded = teacherIds.Count,
            SlotsCreated = slotsCreated,
        };
    }

    /// <summary>
    /// Books demo slots for demo students, no payment involved: each still-free slot of a demo
    /// teacher is taken with probability <paramref name="fillRate"/>, so roughly
    /// <c>1 - fillRate</c> of future slots stay bookable for load tests. Past slots end up
    /// <c>DONE</c>, future ones <c>PENDING</c> or <c>ACCEPTED</c>. Orders follow the
    /// no-payment flow: one PENDING order per (student, teacher), reused if it already exists.
    /// Idempotent in the sense that only free slots are considered; re-running fills
    /// <paramref name="fillRate"/> of what is left.
    /// <para>
    /// Seeded PENDING reservations are auto-rejected by
    /// <c>JobChron.ExpireStaleNoPaymentReservations</c> once
    /// <see cref="HardCode.TEACHER_RESPONSE_WINDOW"/> elapses — re-run to refill.
    /// </para>
    /// </summary>
    public async Task<object> SeedReservationsAsync(int seed = 12345, double fillRate = 0.6)
    {
        fillRate = Math.Clamp(fillRate, 0, 1);
        var random = new Random(seed);
        var now = DateTimeOffset.UtcNow;

        var studentIds = await DemoUsers(context.Users, StudentEmailPrefix)
            .Where(u => u.Student != null)
            .Select(u => u.Id)
            .ToListAsync();
        var teacherIds = await DemoUsers(context.Users, TeacherEmailPrefix)
            .Where(u => u.Teacher != null)
            .Select(u => u.Id)
            .ToListAsync();

        if (studentIds.Count == 0 || teacherIds.Count == 0)
        {
            return new { ReservationsCreated = 0, OrdersCreated = 0, Message = "Aucun élève ou professeur démo" };
        }

        int reservationsCreated = 0,
            ordersCreated = 0;
        var byStatus = new Dictionary<string, int> { ["PENDING"] = 0, ["ACCEPTED"] = 0, ["DONE"] = 0 };

        foreach (var batch in teacherIds.Chunk(TeacherBatchSize))
        {
            var freeSlots = await context
                .Slots.AsNoTracking()
                .Where(s =>
                    batch.Contains(s.TeacherId)
                    && s.ArchivedAt == null
                    && !s.Reservations!.Any(r =>
                        r.ArchivedAt == null && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                    )
                )
                .Select(s => new { s.Id, s.TeacherId, s.DateFrom, s.DateTo })
                .ToListAsync();

            var cursuses = (
                await context
                    .Cursuses.AsNoTracking()
                    .Where(p => batch.Contains(p.TeacherId) && p.ArchivedAt == null)
                    .Select(p => new { p.Id, p.TeacherId, p.Name, p.Price })
                    .ToListAsync()
            )
                .GroupBy(p => p.TeacherId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var orders = await context
                .Orders.Where(o =>
                    batch.Contains(o.TeacherId)
                    && o.ArchivedAt == null
                    && o.StatusId == HardCode.STATUS_ORDER_PENDING
                    && !o.PaymentRequired
                )
                .ToListAsync();
            var orderByPair = orders
                .GroupBy(o => (o.StudentId, o.TeacherId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.CreatedAt).First());

            foreach (var slot in freeSlots)
            {
                if (random.NextDouble() >= fillRate || !cursuses.TryGetValue(slot.TeacherId, out var teacherCursuses))
                {
                    continue;
                }

                var studentId = studentIds[random.Next(studentIds.Count)];
                var cursus = teacherCursuses[random.Next(teacherCursuses.Count)];

                if (!orderByPair.TryGetValue((studentId, slot.TeacherId), out var order))
                {
                    var orderId = Guid.NewGuid();
                    order = new Order
                    {
                        Id = orderId,
                        OrderNumber = $"DEMO_{orderId:N}",
                        OrderDate = now,
                        StudentId = studentId,
                        TeacherId = slot.TeacherId,
                        PaymentRequired = false,
                        CreatedAt = now,
                    };
                    context.Orders.Add(order);
                    orderByPair[(studentId, slot.TeacherId)] = order;
                    ordersCreated++;
                }

                var hours = (decimal)(slot.DateTo - slot.DateFrom).Ticks / TimeSpan.TicksPerHour;
                var reservation = new Reservation
                {
                    Id = Guid.NewGuid(),
                    Title = cursus.Name.Length > 64 ? cursus.Name[..64] : cursus.Name,
                    Description = "Réservation de démonstration",
                    SlotId = slot.Id,
                    CursusId = cursus.Id,
                    StudentId = studentId,
                    OrderId = order.Id,
                    StatusId = HardCode.RESERVATION_PENDING,
                    UnitPrice = cursus.Price,
                    CommissionRate = HardCode.PLATFORM_COMMISSION_RATE,
                    Amount = decimal.Round(cursus.Price * hours, 2, MidpointRounding.AwayFromZero),
                    CreatedAt = now,
                };
                reservation.RecordStatusCreation(studentId, "Réservation créée (seed démo)");

                if (slot.DateTo <= now)
                {
                    AddSeedTransition(reservation, HardCode.RESERVATION_ACCEPTED, slot.TeacherId);
                    AddSeedTransition(reservation, HardCode.RESERVATION_DONE, null);
                    byStatus["DONE"]++;
                }
                else if (random.NextDouble() < 0.5)
                {
                    AddSeedTransition(reservation, HardCode.RESERVATION_ACCEPTED, slot.TeacherId);
                    byStatus["ACCEPTED"]++;
                }
                else
                {
                    reservation.TeacherResponseDeadline = now.Add(HardCode.TEACHER_RESPONSE_WINDOW);
                    byStatus["PENDING"]++;
                }

                context.Reservations.Add(reservation);
                order.TotalAmount += reservation.Amount;
                order.UpdatedAt = now;
                reservationsCreated++;
            }

            await SaveBatchAsync();
        }

        return new
        {
            ReservationsCreated = reservationsCreated,
            OrdersCreated = ordersCreated,
            ByStatus = byStatus,
        };
    }

    /// <summary>Users → slots → reservations, in that order.</summary>
    public async Task<object> SeedAllAsync(
        int teacherCount = 50,
        int studentCount = 200,
        int seed = 12345,
        double fillRate = 0.6
    ) =>
        new
        {
            Users = await SeedUsersAsync(teacherCount, studentCount, seed),
            Slots = await SeedSlotsAsync(seed),
            Reservations = await SeedReservationsAsync(seed, fillRate),
        };

    /// <summary>
    /// Demo accounts only: <c>{prefix}{n}@bonprof.test</c>. The "no dot before @" clause keeps
    /// out integration-test fixtures (<c>teacher.{guid}@bonprof.test</c>) and the test admins,
    /// which share the domain.
    /// </summary>
    private static IQueryable<UserApp> DemoUsers(IQueryable<UserApp> users, string prefix) =>
        users.Where(u =>
            u.Email != null
            && EF.Functions.Like(u.Email, prefix + "%" + DemoEmailDomain)
            && !EF.Functions.Like(u.Email, "%.%@%")
        );

    private async Task<(int Created, int Skipped)> SeedAccountsAsync(
        string prefix,
        int count,
        int seed,
        string passwordHash,
        List<CategoryCursus> categories
    )
    {
        var isTeacher = prefix == TeacherEmailPrefix;
        var roleId = isTeacher ? HardCode.ROLE_TEACHER : HardCode.ROLE_STUDENT;
        int created = 0,
            skipped = 0;

        for (int start = 1; start <= count; start += UserBatchSize)
        {
            var indexes = Enumerable.Range(start, Math.Min(UserBatchSize, count - start + 1)).ToList();
            var emails = indexes.Select(i => $"{prefix}{i}{DemoEmailDomain}").ToList();
            var existing = (
                await context.Users.Where(u => emails.Contains(u.Email!)).Select(u => u.Email!).ToListAsync()
            ).ToHashSet();

            if (isTeacher)
            {
                context.CategoryCursuses.AttachRange(categories);
            }

            foreach (var i in indexes)
            {
                var email = $"{prefix}{i}{DemoEmailDomain}";
                if (existing.Contains(email))
                {
                    skipped++;
                    continue;
                }

                // One Randomizer per (prefix, index): user n gets the same profile whatever
                // the batch it is created in, so incremental runs stay reproducible.
                var faker = new Faker("fr") { Random = new Randomizer(unchecked(seed * 31 + (isTeacher ? 1 : 2) * 1_000_003 + i)) };
                var user = BuildUser(faker, email, userManager.NormalizeEmail(email), passwordHash);
                context.Users.Add(user);
                context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roleId });

                if (isTeacher)
                {
                    AddTeacherProfile(faker, user.Id, categories);
                }
                else
                {
                    context.Students.Add(new Student { Id = user.Id, UserId = user.Id });
                }
                AddCommonProfile(faker, user.Id, isTeacher);
                created++;
            }

            await SaveBatchAsync();
        }

        return (created, skipped);
    }

    private async Task<int> BackfillProfilesAsync(int seed)
    {
        var missing = await context
            .Users.Where(u =>
                (
                    EF.Functions.Like(u.Email!, TeacherEmailPrefix + "%" + DemoEmailDomain)
                    || EF.Functions.Like(u.Email!, StudentEmailPrefix + "%" + DemoEmailDomain)
                )
                && !EF.Functions.Like(u.Email!, "%.%@%")
                && !u.Addresses.Any()
            )
            .Select(u => new { u.Id, IsTeacher = u.Teacher != null })
            .ToListAsync();

        foreach (var batch in missing.Chunk(UserBatchSize))
        {
            foreach (var user in batch)
            {
                AddCommonProfile(FakerFor(seed, user.Id), user.Id, user.IsTeacher);
            }
            await SaveBatchAsync();
        }

        return missing.Count;
    }

    private void AddTeacherProfile(Faker faker, Guid userId, List<CategoryCursus> categories)
    {
        Guid[] levels =
        [
            HardCode.LEVEL_ALL, HardCode.LEVEL_BEGINNER,
            HardCode.LEVEL_INTERMEDIATE, HardCode.LEVEL_ADVANCED,
        ];

        context.Teachers.Add(
            new Teacher
            {
                Id = userId,
                UserId = userId,
                Siret = faker.Random.Long(10000000000000, 99999999999999),
                IsProfessional = true,
                PriceIndicative = decimal.Round(faker.Random.Decimal(20m, 80m), 2),
                LinkedIn = faker.Internet.Url(),
                IsStripeConfirmed = false,
                StripeAccount = null,
                AverageRating = 0,
                ReviewsCount = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );

        // Cursuses (1-6) = what the teacher advertises on their profile and sells.
        int cursusCount = faker.Random.Int(1, 6);
        for (int p = 0; p < cursusCount; p++)
        {
            var subject = faker.PickRandom(Subjects);
            context.Cursuses.Add(
                new Cursus
                {
                    Id = Guid.NewGuid(),
                    Name = $"{subject} — {faker.Commerce.ProductAdjective()}",
                    Description = $"<p>{faker.Lorem.Sentence(faker.Random.Int(5, 12))}</p>",
                    Price = decimal.Round(faker.Random.Decimal(15m, 60m), 2),
                    TeacherId = userId,
                    LevelId = faker.PickRandom(levels),
                    Categories = faker
                        .PickRandom(categories, faker.Random.Int(1, Math.Min(2, categories.Count)))
                        .ToList(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }
    }

    /// <summary>Main address (drives city / postal-code / radius search), formations, and
    /// for teachers, experiences.</summary>
    private void AddCommonProfile(Faker faker, Guid userId, bool isTeacher)
    {
        var city = faker.PickRandom(Cities);
        context.Addresses.Add(
            new Address
            {
                Id = Guid.NewGuid(),
                Street = faker.Address.StreetAddress(),
                City = city.Name,
                Country = "France",
                ZipCode = city.ZipCode,
                Latitude = (float)(city.Latitude + faker.Random.Double(-0.05, 0.05)),
                Longitude = (float)(city.Longitude + faker.Random.Double(-0.05, 0.05)),
                UserId = userId,
                TypeId = HardCode.TYPE_ADDRESS_MAIN,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );

        int formationCount = isTeacher ? faker.Random.Int(1, 3) : faker.Random.Int(0, 2);
        for (int f = 0; f < formationCount; f++)
        {
            var from = faker.Date.PastOffset(20, DateTimeOffset.UtcNow.AddYears(-2));
            context.Formations.Add(
                new Formation
                {
                    Id = Guid.NewGuid(),
                    Title = $"{faker.PickRandom("Licence", "Master", "Doctorat", "BTS", "Certificat")} {faker.PickRandom(Subjects)}",
                    Institute = faker.PickRandom(Institutes),
                    Description = $"<p>{faker.Lorem.Sentence(faker.Random.Int(8, 16))}</p>",
                    DateFrom = from,
                    DateTo = from.AddYears(faker.Random.Int(1, 5)),
                    UserId = userId,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        if (!isTeacher)
        {
            return;
        }

        int experienceCount = faker.Random.Int(1, 3);
        for (int e = 0; e < experienceCount; e++)
        {
            var from = faker.Date.PastOffset(15, DateTimeOffset.UtcNow.AddYears(-1));
            context.Experiences.Add(
                new Experience
                {
                    Id = Guid.NewGuid(),
                    Title = $"Professeur de {faker.PickRandom(Subjects)}",
                    Company = faker.Company.CompanyName(),
                    Description = faker.Lorem.Sentence(faker.Random.Int(8, 16)),
                    DateFrom = from,
                    DateTo = faker.Random.Bool() ? from.AddYears(faker.Random.Int(1, 4)) : null,
                    TeacherId = userId,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }
    }

    /// <summary>
    /// Creates 100 non-overlapping 1-hour slots for a teacher: <see cref="PastWeeks"/>
    /// weeks in the past and <see cref="FutureWeeks"/> weeks in the near future,
    /// <see cref="SlotsPerWeek"/> per week (Mon–Fri at 09:00 and 12:00). Distinct
    /// day+hour per slot guarantees no overlap.
    /// </summary>
    private int AddSlotsForTeacher(Faker faker, Guid teacherId, Guid[] slotTypes)
    {
        var today = DateTimeOffset.UtcNow.Date;
        int diffToMonday = ((int)today.DayOfWeek + 6) % 7; // Monday = 0
        var currentMonday = today.AddDays(-diffToMonday);

        // Past weeks (-PastWeeks..-1), then future weeks (1..FutureWeeks). The
        // current week is skipped so no slot straddles "now".
        var weekOffsets = Enumerable
            .Range(-PastWeeks, PastWeeks)
            .Concat(Enumerable.Range(1, FutureWeeks));

        int created = 0;
        foreach (var w in weekOffsets)
        {
            var weekMonday = currentMonday.AddDays(w * 7);
            for (int j = 0; j < SlotsPerWeek; j++)
            {
                var day = weekMonday.AddDays(j % 5); // Mon..Fri
                int hour = 9 + (j / 5) * 3; // 09:00 or 12:00
                var dateFrom = new DateTimeOffset(day.AddHours(hour), TimeSpan.Zero);
                context.Slots.Add(
                    new Slot
                    {
                        Id = Guid.NewGuid(),
                        DateFrom = dateFrom,
                        DateTo = dateFrom.AddHours(1),
                        TeacherId = teacherId,
                        TypeId = faker.PickRandom(slotTypes),
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
                created++;
            }
        }
        return created;
    }

    private static void AddSeedTransition(Reservation reservation, Guid toStatusId, Guid? changedById)
    {
        reservation.StatusHistory.Add(
            new ReservationStatusHistory(reservation.Id, reservation.StatusId, toStatusId, changedById, "Seed démo")
        );
        reservation.StatusId = toStatusId;
    }

    /// <summary>Saves and detaches the batch so the change tracker never grows with the seed.</summary>
    private async Task SaveBatchAsync()
    {
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static Faker FakerFor(int seed, Guid id) =>
        new("fr") { Random = new Randomizer(seed ^ BitConverter.ToInt32(id.ToByteArray(), 0)) };

    private static UserApp BuildUser(Faker faker, string email, string normalizedEmail, string passwordHash)
    {
        var gender = faker.PickRandom<Bogus.DataSets.Name.Gender>();
        return new UserApp
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = normalizedEmail,
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            PasswordHash = passwordHash,
            SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            StatusId = HardCode.ACCOUNT_ACTIVE,
            GenderId = gender == Bogus.DataSets.Name.Gender.Male
                ? HardCode.GENDER_MALE
                : HardCode.GENDER_FEMALE,
            FirstName = faker.Name.FirstName(gender),
            LastName = faker.Name.LastName(gender),
            DateOfBirth = faker.Date.BetweenOffset(
                DateTimeOffset.UtcNow.AddYears(-60),
                DateTimeOffset.UtcNow.AddYears(-18)
            ),
            Title = faker.Name.JobTitle(),
            Description = faker.Lorem.Sentence(faker.Random.Int(8, 18)),
            DataProcessingConsent = true,
            PrivacyPolicyConsent = true,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
