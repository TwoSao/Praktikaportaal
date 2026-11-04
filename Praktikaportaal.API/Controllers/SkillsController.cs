using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly ISkillService _skillService;

    public SkillsController(ISkillService skillService)
    {
        _skillService = skillService;
    }

    /// <summary>Returns all available skill tags.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SkillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _skillService.GetAllSkillsAsync(ct);
        return Ok(list);
    }

    /// <summary>Creates a new skill tag.</summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSkillDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Skill name is required." });

        var created = await _skillService.CreateSkillAsync(dto, ct);
        return CreatedAtAction(nameof(GetAll), null, created);
    }
}

