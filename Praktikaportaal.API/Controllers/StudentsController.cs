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
    private readonly ICvService _cvService;

    public StudentsController(IStudentService studentService, ICvService cvService)
    {
        _studentService = studentService;
        _cvService = cvService;
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

    /// <summary>
    /// Uploads (or replaces) the student's CV. Multipart form field <c>file</c>; PDF or DOCX, max 5 MB.
    /// The content is checked, not only the file name.
    /// </summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost("me/cv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(CvRequestLimitBytes)]
    [ProducesResponseType(typeof(CvDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CvDto>> UploadCv(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var cv = await _cvService.UploadAsync(User.RequireProfileId(), file.FileName, file.ContentType, file.Length, stream, ct);
        return CreatedAtAction(nameof(GetMyCv), cv);
    }

    /// <summary>Returns metadata of the student's current CV.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("me/cv")]
    [ProducesResponseType(typeof(CvDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CvDto>> GetMyCv(CancellationToken ct) =>
        Ok(await _cvService.GetMyCvAsync(User.RequireProfileId(), ct));

    /// <summary>Downloads the student's own CV file.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("me/cv/file")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadMyCv(CancellationToken ct)
    {
        var file = await _cvService.DownloadMyCvAsync(User.RequireProfileId(), ct);
        return ToFileResult(file);
    }

    /// <summary>Deletes the student's CV.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpDelete("me/cv")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyCv(CancellationToken ct)
    {
        await _cvService.DeleteMyCvAsync(User.RequireProfileId(), ct);
        return NoContent();
    }

    // 5 MB file + multipart overhead
    private const long CvRequestLimitBytes = 6 * 1024 * 1024;

    /// <summary>Streams the file as an attachment; the browser must not sniff or render it.</summary>
    private IActionResult ToFileResult(CvFile file)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
