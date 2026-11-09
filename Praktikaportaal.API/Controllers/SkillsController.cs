using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.Constants;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SkillsController : ControllerBase
{
    private readonly ISkillService _skillService;

    public SkillsController(ISkillService skillService)
    {
        _skillService = skillService;
    }

    /// <summary>Returns all available skill tags, sorted by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SkillDto>>> GetAll(CancellationToken ct) =>
        Ok(await _skillService.GetAllSkillsAsync(ct));

    /// <summary>
    /// Creates a new skill tag (Company or Admin). If a skill with the same name exists, it is returned instead.
    /// </summary>
    [Authorize(Roles = Roles.CompanyOrAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SkillDto>> Create([FromBody] CreateSkillDto dto, CancellationToken ct)
    {
        var created = await _skillService.CreateSkillAsync(dto, ct);
        return CreatedAtAction(nameof(GetAll), null, created);
    }
}
