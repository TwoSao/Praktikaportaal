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
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    /// <summary>Returns the logged-in student's profile with skills.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("me")]
    [ProducesResponseType(typeof(StudentProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentProfileDto>> GetMe(CancellationToken ct) =>
        Ok(await _studentService.GetProfileAsync(User.RequireProfileId(), ct));

    /// <summary>
    /// Updates the logged-in student's profile. The <c>skills</c> list replaces the existing skills
    /// (get ids from GET /api/skills).
    /// </summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPut("me")]
    [ProducesResponseType(typeof(StudentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StudentProfileDto>> UpdateMe([FromBody] UpdateStudentProfileDto dto, CancellationToken ct) =>
        Ok(await _studentService.UpdateProfileAsync(User.RequireProfileId(), dto, ct));

    /// <summary>Lists all students, paged (Service account for the Worker, or Admin).</summary>
    [Authorize(Roles = Roles.ServiceOrAdmin)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StudentProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StudentProfileDto>>> GetAll([FromQuery] PagingParameters paging, CancellationToken ct) =>
        Ok(await _studentService.GetStudentsAsync(paging, ct));
}
