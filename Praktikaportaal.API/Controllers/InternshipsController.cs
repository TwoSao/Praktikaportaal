using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InternshipsController : ControllerBase
{
    private readonly IInternshipService _internshipService;

    public InternshipsController(IInternshipService internshipService)
    {
        _internshipService = internshipService;
    }

    /// <summary>Returns list of published internships with optional search and remote filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InternshipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? isRemote, CancellationToken ct)
    {
        var list = await _internshipService.GetPublishedInternshipsAsync(search, isRemote, ct);
        return Ok(list);
    }

    /// <summary>Returns full details of an internship by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _internshipService.GetInternshipByIdAsync(id, ct);
        return item != null ? Ok(item) : NotFound();
    }

    /// <summary>Returns all internships published by the authenticated company.</summary>
    [Authorize(Roles = "Company")]
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<InternshipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyInternships(CancellationToken ct)
    {
        var companyProfileId = User.GetProfileId();
        if (companyProfileId == null)
            return Forbid();

        var list = await _internshipService.GetCompanyInternshipsAsync(companyProfileId.Value, ct);
        return Ok(list);
    }

    /// <summary>Creates a new internship listing.</summary>
    [Authorize(Roles = "Company")]
    [HttpPost]
    [ProducesResponseType(typeof(InternshipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateInternshipDto dto, CancellationToken ct)
    {
        var companyProfileId = User.GetProfileId();
        if (companyProfileId == null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest(new { error = "Title and Description are required." });

        var created = await _internshipService.CreateInternshipAsync(companyProfileId.Value, dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates status of an internship listing (Draft, Published, Closed, Archived).</summary>
    [Authorize(Roles = "Company")]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateInternshipStatusDto dto, CancellationToken ct)
    {
        var companyProfileId = User.GetProfileId();
        if (companyProfileId == null)
            return Forbid();

        bool updated = await _internshipService.UpdateInternshipStatusAsync(id, companyProfileId.Value, dto.Status, ct);
        return updated ? NoContent() : NotFound(new { error = "Internship not found or does not belong to your company." });
    }
}

