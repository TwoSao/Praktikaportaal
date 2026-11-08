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

    public InternshipsController(IInternshipService internshipService)
    {
        _internshipService = internshipService;
    }

    /// <summary>Returns published internships with optional search and remote filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InternshipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InternshipDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] bool? isRemote, CancellationToken ct) =>
        Ok(await _internshipService.GetPublishedInternshipsAsync(search, isRemote, ct));

    /// <summary>Returns full details of an internship by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InternshipDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _internshipService.GetInternshipByIdAsync(id, ct));

    /// <summary>Returns all internships of the authenticated company.</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<InternshipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InternshipDto>>> GetMyInternships(CancellationToken ct) =>
        Ok(await _internshipService.GetCompanyInternshipsAsync(User.RequireProfileId(), ct));

    /// <summary>Creates a new internship listing (Company only).</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpPost]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InternshipDto>> Create([FromBody] CreateInternshipDto dto, CancellationToken ct)
    {
        var created = await _internshipService.CreateInternshipAsync(User.RequireProfileId(), dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates status of an internship listing (Draft, Published, Closed, Archived) — owning Company only.</summary>
    [Authorize(Roles = Roles.Company)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateInternshipStatusDto dto, CancellationToken ct)
    {
        await _internshipService.UpdateInternshipStatusAsync(id, User.RequireProfileId(), dto.Status, ct);
        return NoContent();
    }
}
