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
public class InternshipsController : ControllerBase
{
    private readonly IInternshipService _internshipService;
    private readonly IApplicationService _applicationService;

    public InternshipsController(IInternshipService internshipService, IApplicationService applicationService)
    {
        _internshipService = internshipService;
        _applicationService = applicationService;
    }

    /// <summary>Returns published internships with optional search and remote filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InternshipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InternshipDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] bool? isRemote, CancellationToken ct) =>
        Ok(await _internshipService.GetPublishedInternshipsAsync(search, isRemote, ct));

    /// <summary>
    /// Returns an internship by id. Draft/closed listings are visible only to the owning company and admins.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InternshipDto>> GetById(Guid id, CancellationToken ct)
    {
        var caller = User.Identity?.IsAuthenticated == true ? User.ToCurrentUser() : null;
        return Ok(await _internshipService.GetInternshipByIdAsync(id, caller, ct));
    }

    /// <summary>Returns all internships of the authenticated company (any status).</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<InternshipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InternshipDto>>> GetMyInternships(CancellationToken ct) =>
        Ok(await _internshipService.GetCompanyInternshipsAsync(User.RequireProfileId(), ct));

    /// <summary>Creates a new internship listing (Company only). It is published immediately.</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpPost]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InternshipDto>> Create([FromBody] CreateInternshipDto dto, CancellationToken ct)
    {
        var created = await _internshipService.CreateInternshipAsync(User.RequireProfileId(), dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an internship listing (owning Company or Admin).</summary>
    [Authorize(Roles = Roles.CompanyOrAdmin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InternshipDto>> Update(Guid id, [FromBody] UpdateInternshipDto dto, CancellationToken ct) =>
        Ok(await _internshipService.UpdateInternshipAsync(id, dto, User.ToCurrentUser(), ct));

    /// <summary>Deletes (soft-deletes) an internship listing (owning Company or Admin). Applications are kept.</summary>
    [Authorize(Roles = Roles.CompanyOrAdmin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _internshipService.DeleteInternshipAsync(id, User.ToCurrentUser(), ct);
        return NoContent();
    }

    /// <summary>Changes the listing status (Draft, Published, Closed, Archived) — owning Company or Admin (moderation).</summary>
    [Authorize(Roles = Roles.CompanyOrAdmin)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateInternshipStatusDto dto, CancellationToken ct)
    {
        await _internshipService.UpdateInternshipStatusAsync(id, dto.Status, User.ToCurrentUser(), ct);
        return NoContent();
    }

    /// <summary>Applies to the internship (Student only). One active application per internship.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost("{id:guid}/applications")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> Apply(Guid id, [FromBody] CreateApplicationDto dto, CancellationToken ct)
    {
        var created = await _applicationService.ApplyAsync(User.RequireProfileId(), id, dto, ct);
        return CreatedAtAction(nameof(ApplicationsController.GetById), "Applications", new { id = created.Id }, created);
    }

    /// <summary>Returns the applications received for this internship (owning Company or Admin).</summary>
    [Authorize(Roles = Roles.CompanyOrAdmin)]
    [HttpGet("{id:guid}/applications")]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetApplications(Guid id, CancellationToken ct) =>
        Ok(await _applicationService.GetInternshipApplicationsAsync(id, User.ToCurrentUser(), ct));
}
