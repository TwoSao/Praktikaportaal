using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken ct = default);
    Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
}

