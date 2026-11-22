using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TypeAddressesController(TypeAddressService typeAddressService) : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("all")]
        public async Task<ActionResult<Response<List<TypeAddressDetails>>>> GetAllTypeAddresses()
        {
            var response = await typeAddressService.GetAllAddresseTypesAsync();
            if (response.Status == 200)
            {
                return Ok(response);
            }
            return StatusCode(response.Status, response);
        }
    }
}
