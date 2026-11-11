using Microsoft.AspNetCore.Mvc;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    public IActionResult Get()
    {
        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "The template weather endpoint is not part of the label verification API."
        });
    }
}
