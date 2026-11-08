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
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    /// <summary>Submits an application to an internship (Student only).</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> Apply([FromBody] CreateApplicationDto dto, CancellationToken ct)
    {
        var created = await _applicationService.ApplyAsync(User.RequireProfileId(), dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Returns one application (its student, the owning company or an admin).</summary>
    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _applicationService.GetByIdAsync(id, User.ToCurrentUser(), ct));

    /// <summary>Returns list of applications submitted by the logged-in student.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetMyApplications(CancellationToken ct) =>
        Ok(await _applicationService.GetStudentApplicationsAsync(User.RequireProfileId(), ct));

    /// <summary>Returns all applications received for a specific internship listing (owning Company only).</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpGet("internship/{internshipId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetInternshipApplications(Guid internshipId, CancellationToken ct) =>
        Ok(await _applicationService.GetInternshipApplicationsAsync(internshipId, User.RequireProfileId(), ct));

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
