using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Registers a new student account and returns a JWT token.</summary>
    [HttpPost("register-student")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterStudent([FromBody] RegisterStudentDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(AuthResultDto.Failed("Email and password are required."));

        if (dto.Password.Length < 6)
            return BadRequest(AuthResultDto.Failed("Password must be at least 6 characters long."));

        var result = await _authService.RegisterStudentAsync(dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Registers a new company account and returns a JWT token.</summary>
    [HttpPost("register-company")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCompany([FromBody] RegisterCompanyDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(AuthResultDto.Failed("Email and password are required."));

        if (string.IsNullOrWhiteSpace(dto.CompanyName) || string.IsNullOrWhiteSpace(dto.RegistrationCode))
            return BadRequest(AuthResultDto.Failed("Company name and registration code are required."));

        if (dto.Password.Length < 6)
            return BadRequest(AuthResultDto.Failed("Password must be at least 6 characters long."));

        var result = await _authService.RegisterCompanyAsync(dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Authenticates user and returns JWT token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return Unauthorized(AuthResultDto.Failed("Email and password are required."));

        var result = await _authService.LoginAsync(dto, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Returns profile of the currently logged-in user.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await _authService.GetUserByIdAsync(userId, ct);
        return user != null ? Ok(user) : NotFound();
    }
}
