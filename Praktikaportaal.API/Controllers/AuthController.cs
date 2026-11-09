using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Registers a new student account and returns a JWT token.</summary>
    [HttpPost("register-student")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResultDto>> RegisterStudent([FromBody] RegisterStudentDto dto, CancellationToken ct)
    {
        var result = await _authService.RegisterStudentAsync(dto, ct);
        return CreatedAtAction(nameof(GetCurrentUser), null, result);
    }

    /// <summary>Registers a new company account and returns a JWT token.</summary>
    [HttpPost("register-company")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResultDto>> RegisterCompany([FromBody] RegisterCompanyDto dto, CancellationToken ct)
    {
        var result = await _authService.RegisterCompanyAsync(dto, ct);
        return CreatedAtAction(nameof(GetCurrentUser), null, result);
    }

    /// <summary>Authenticates a user and returns a JWT token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResultDto>> Login([FromBody] LoginDto dto, CancellationToken ct) =>
        Ok(await _authService.LoginAsync(dto, ct));

    /// <summary>Returns the currently logged-in user.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetCurrentUser(CancellationToken ct) =>
        Ok(await _authService.GetUserByIdAsync(User.GetUserId(), ct));
}
