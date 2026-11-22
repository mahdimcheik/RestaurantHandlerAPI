using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class StudentsController : ControllerBase
{
    private readonly ProfileService _studentService;
    private readonly TeacherContactService _contactService;
    private readonly CallerResolver _callerResolver;

    public StudentsController(
        ProfileService studentService,
        TeacherContactService contactService,
        CallerResolver callerResolver
    )
    {
        _studentService = studentService;
        _contactService = contactService;
        _callerResolver = callerResolver;
    }


    [HttpGet("my-profile")]
    [Authorize]
    public async Task<ActionResult<Response<UserDetails>>> GetMyProfile()
    {
        var response = await _studentService.GetStudentFullProfileAsync(await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpGet("profile/{userId:guid}")]
    [Authorize]
    public async Task<ActionResult<Response<UserDetails>>> GetStudentProfile(Guid userId)
    {
        var caller = await _callerResolver.ResolveAsync(User);
        if (caller is null)
            return Unauthorized();

        var response = await _studentService.GetStudentpublicProfileAsync(userId, caller);
        return StatusCode(response.Status, response);
    }

    [Authorize]
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<Response<UserDetails>>> GetStudentByUserId(
        [FromRoute] Guid userId)
    {
        var caller = await _callerResolver.ResolveAsync(User);
        if (caller is null)
            return Unauthorized();

        var response = await _studentService.GetStudentByUserIdAsync(userId, caller);
        return StatusCode(response.Status, response);
    }

    [HttpPut("update-profile")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<UserDetails>>> UpdateStudent(
        [FromBody] UserUpdate student)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await _studentService.UpdateStudentProfileAsync(student, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [HttpPost("contact-teacher")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<object>>> ContactTeacher(
        [FromBody] ContactTeacher message)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await _contactService.SendMessageToTeacherAsync(message, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }
}
