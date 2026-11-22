using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;

namespace RestaurantHandler.Controllers;

/// <summary>Enveloppe de succès ; les refus métier passent par les BusinessException (ProblemDetails).</summary>
public static class ApiResponses
{
    public static ActionResult<Response<T>> Success<T>(T data, string message = "Ok") =>
        new OkObjectResult(new Response<T> { Status = 200, Message = message, Data = data });

    public static ActionResult<Response<T>> CreatedWith<T>(T data, string message = "Créé") =>
        new ObjectResult(new Response<T> { Status = 201, Message = message, Data = data }) { StatusCode = 201 };

    public static ActionResult<Response<object>> Done(string message) =>
        new OkObjectResult(new Response<object> { Status = 200, Message = message });
}
