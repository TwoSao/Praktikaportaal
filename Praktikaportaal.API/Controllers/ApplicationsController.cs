using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.Constants;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
/// <summary>
/// Application endpoints by application id. Applying and listing per internship live under
/// /api/internships/{id}/applications.
/// </summary>
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    /// <summary>Returns one application (its student, the owning company or an admin).</summary>
    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _applicationService.GetByIdAsync(id, User.ToCurrentUser(), ct));

    /// <summary>Returns the logged-in student's applications (newest first), optionally by status.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<ApplicationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ApplicationDto>>> GetMyApplications(
        [FromQuery] ApplicationQueryParameters query, CancellationToken ct) =>
        Ok(await _applicationService.GetStudentApplicationsAsync(User.RequireProfileId(), query, ct));

    /// <summary>Updates an application's status (Viewed, Interview, Accepted, Rejected) — owning Company only.</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateApplicationStatusDto dto, CancellationToken ct)
    {
        await _applicationService.UpdateApplicationStatusAsync(id, User.RequireProfileId(), dto.Status, ct);
        return NoContent();
    }

    /// <summary>Withdraws the student's own open application.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        await _applicationService.WithdrawApplicationAsync(id, User.RequireProfileId(), ct);
        return NoContent();
    }
}
