using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.Controllers
{

    [Produces("application/json")]
    [Consumes("application/json")]
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    [EnableCors]
    public class AddressesController(AddressesService addressesService) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<Response<List<AddressDetails>>>> GetAddressesByUserId()
        {
            var response = await addressesService.GetAddressesByUserIdAsync(await this.GetCallerAsync());

            if (response.Status == 200)
            {
                return Ok(response);
            }

            return StatusCode(response.Status, response);
        }

        [HttpPost]
        public async Task<ActionResult<Response<AddressDetails>>> CreateAddress(
            [FromBody] AddressCreate addressDto)
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

            var response = await addressesService.CreateAddressAsync(addressDto, await this.GetCallerAsync());

            return StatusCode(response.Status, response);
        }

        [HttpPut]
        public async Task<ActionResult<Response<AddressDetails>>> UpdateAddress(
            [FromBody] AddressUpdate addressDto)
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

            var response = await addressesService.UpdateAddressAsync(addressDto, await this.GetCallerAsync());

            return StatusCode(response.Status, response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<Response<object>>> DeleteAddress(
            [FromRoute] Guid id)
        {
            var response = await addressesService.DeleteAddressAsync(id, await this.GetCallerAsync());

            return StatusCode(response.Status, response);
        }
    }
}
