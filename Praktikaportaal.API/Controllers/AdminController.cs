using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.Constants;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

/// <summary>Administration endpoints (Admin role only).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public AdminController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    /// <summary>
    /// Application statistics: total, by status, top internships, by field and per period.
    /// Example: <c>?from=2026-10-01&amp;to=2026-11-30&amp;period=Week&amp;top=5</c>.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApplicationStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationStatisticsDto>> GetStatistics(
        [FromQuery] StatisticsQueryParameters query, CancellationToken ct) =>
        Ok(await _statisticsService.GetApplicationStatisticsAsync(query, ct));
}
