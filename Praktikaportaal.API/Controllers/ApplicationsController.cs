using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.Constants;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Apply([FromBody] CreateApplicationDto dto, CancellationToken ct)
    {
        var studentProfileId = User.GetProfileId();
        if (studentProfileId == null)
            return Forbid();

        var (success, error, appDto) = await _applicationService.ApplyAsync(studentProfileId.Value, dto, ct);
        if (!success)
            return BadRequest(new { error });

        return Ok(appDto);
    }

    /// <summary>Returns list of applications submitted by the logged-in student.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyApplications(CancellationToken ct)
    {
        var studentProfileId = User.GetProfileId();
        if (studentProfileId == null)
            return Forbid();

        var list = await _applicationService.GetStudentApplicationsAsync(studentProfileId.Value, ct);
        return Ok(list);
    }

    /// <summary>Returns all applications received for a specific internship listing (Company only).</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpGet("internship/{internshipId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInternshipApplications(Guid internshipId, CancellationToken ct)
    {
        var companyProfileId = User.GetProfileId();
        if (companyProfileId == null)
            return Forbid();

        var list = await _applicationService.GetInternshipApplicationsAsync(internshipId, companyProfileId.Value, ct);
        return Ok(list);
    }

    /// <summary>Updates candidate's application status (Viewed, Interview, Accepted, Rejected) by company.</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateApplicationStatusDto dto, CancellationToken ct)
    {
        var companyProfileId = User.GetProfileId();
        if (companyProfileId == null)
            return Forbid();

        bool updated = await _applicationService.UpdateApplicationStatusAsync(id, companyProfileId.Value, dto.Status, ct);
        return updated ? NoContent() : BadRequest(new { error = "Unable to update application status." });
    }

    /// <summary>Withdraws student's application.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var studentProfileId = User.GetProfileId();
        if (studentProfileId == null)
            return Forbid();

        bool withdrawn = await _applicationService.WithdrawApplicationAsync(id, studentProfileId.Value, ct);
        return withdrawn ? NoContent() : BadRequest(new { error = "Unable to withdraw application." });
    }
}
