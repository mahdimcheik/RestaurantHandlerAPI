using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Controllers
{
    [Produces("application/json")]
    [Consumes("application/json")]
    [Route("[controller]")]
    [ApiController]
    [EnableCors]
    public class LanguagesController(LanguagesService languagesService, MainContext context) : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("all")]
        public async Task<ActionResult<Response<List<LanguageDetails>>>> GetAllLanguages()
        {
            var response = await languagesService.GetAllLanguagesAsync();

            if (response.Status == 200)
            {
                return Ok(response);
            }

            return StatusCode(response.Status, response);
        }

        [Authorize]
        [HttpGet("user")]
        public async Task<ActionResult<Response<List<LanguageDetails>>>> GetLanguagesByUserId()
        {
            var response = await languagesService.GetLanguagesByTeacherIdAsync(await this.GetCallerAsync());

            if (response.Status == 200)
            {
                return Ok(response);
            }

            return StatusCode(response.Status, response);
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPost("create")]
        public async Task<ActionResult<Response<LanguageDetails>>> CreateLanguage(
            [FromBody] LanguageCreate languageDto)
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

            var response = await languagesService.CreateLanguageAsync(languageDto);

            return StatusCode(response.Status, response);
        }
        
        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpPut("update/{id:guid}")]
        public async Task<ActionResult<Response<LanguageDetails>>> UpdateLanguage(
            [FromRoute] Guid id,
            [FromBody] LanguageUpdate languageDto)
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

            var response = await languagesService.UpdateLanguageAsync(id, languageDto);

            return StatusCode(response.Status, response);
        }
        
        [Authorize(Roles = "Admin,SuperAdmin")]
        [HttpDelete("delete/{id:guid}")]
        public async Task<ActionResult<Response<object>>> DeleteLanguage(
            [FromRoute] Guid id)
        {
            var response = await languagesService.DeleteLanguageAsync(id);

            return StatusCode(response.Status, response);
        }
        
        [Authorize(Roles = "Teacher")]
        [HttpPost("teacher/update-languages")]
        public async Task<ActionResult<Response<List<LanguageDetails>>>> UpdateLanguagesForUser(
            [FromBody] Guid[] languagesIds)
        {
            var user = CheckUser.GetUserFromClaim(User, context);

            if (!ModelState.IsValid || user is null)
            {
                return BadRequest(new Response<object>
                {
                    Status = 400,
                    Message = "Données de validation invalides",
                    Data = ModelState
                });
            }
            var teacher = await context.Teachers.FindAsync(user.Id);
            if (teacher is null)
            {
                return BadRequest(new Response<object>
                {
                    Status = 400,
                    Message = "Données de validation invalides",
                    Data = ModelState
                });
            }

            var response = await languagesService.UpdateLanguagesForUser(languagesIds, await this.GetCallerAsync());

            return StatusCode(response.Status, response);
        }
    }
}
