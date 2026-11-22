using Microsoft.AspNetCore.Authorization;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Dev-only utilities for local/test environments. Gated so it can never run
/// against production: allowed when the host is in Development, or when the
/// ALLOW_DEMO_SEED env var is set to "true".
/// </summary>
[Produces("application/json")]
[Route("[controller]")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[EnableCors]
public class DevController(DemoSeedService seeder, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("seed-users")]
    public async Task<ActionResult<Response<object>>> SeedUsers(
        [FromQuery] int teachers = 50,
        [FromQuery] int students = 200,
        [FromQuery] int seed = 12345
    )
    {
        if (!IsAllowed())
        {
            return Disabled();
        }

        var result = await seeder.SeedUsersAsync(teachers, students, seed);

        return Ok(new Response<object>
        {
            Status = 200,
            Message = "Seed utilisateurs terminé",
            Data = result,
        });
    }

    [HttpPost("seed-slots")]
    public async Task<ActionResult<Response<object>>> SeedSlots([FromQuery] int seed = 12345)
    {
        if (!IsAllowed())
        {
            return Disabled();
        }

        var result = await seeder.SeedSlotsAsync(seed);

        return Ok(new Response<object>
        {
            Status = 200,
            Message = "Seed créneaux terminé",
            Data = result,
        });
    }

    [HttpPost("seed-reservations")]
    public async Task<ActionResult<Response<object>>> SeedReservations(
        [FromQuery] int seed = 12345,
        [FromQuery] double fillRate = 0.6
    )
    {
        if (!IsAllowed())
        {
            return Disabled();
        }

        var result = await seeder.SeedReservationsAsync(seed, fillRate);

        return Ok(new Response<object>
        {
            Status = 200,
            Message = "Seed réservations terminé",
            Data = result,
        });
    }

    /// <summary>
    /// Utilisateurs (profil complet) → créneaux → réservations sans paiement. Rappeler avec
    /// des compteurs plus grands n'ajoute que les comptes manquants.
    /// </summary>
    [HttpPost("seed-all")]
    public async Task<ActionResult<Response<object>>> SeedAll(
        [FromQuery] int teachers = 50,
        [FromQuery] int students = 200,
        [FromQuery] int seed = 12345,
        [FromQuery] double fillRate = 0.6
    )
    {
        if (!IsAllowed())
        {
            return Disabled();
        }

        var result = await seeder.SeedAllAsync(teachers, students, seed, fillRate);

        return Ok(new Response<object>
        {
            Status = 200,
            Message = "Seed complet terminé",
            Data = result,
        });
    }

    private bool IsAllowed() =>
        env.IsDevelopment()
        || string.Equals(
            Environment.GetEnvironmentVariable("ALLOW_DEMO_SEED"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );

    private ActionResult<Response<object>> Disabled() =>
        StatusCode(
            403,
            new Response<object>
            {
                Status = 403,
                Message = "Seeding désactivé dans cet environnement",
            }
        );
}
