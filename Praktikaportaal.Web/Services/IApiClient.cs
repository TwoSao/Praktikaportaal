using Praktikaportaal.Web.Models;

namespace Praktikaportaal.Web.Services;

public interface IApiClient
{
    Task<(bool Success, string? Token, string? Error, dynamic? User)> LoginAsync(LoginViewModel model);
    Task<(bool Success, string? Token, string? Error, dynamic? User)> RegisterStudentAsync(RegisterStudentViewModel model);
    Task<(bool Success, string? Token, string? Error, dynamic? User)> RegisterCompanyAsync(RegisterCompanyViewModel model);

    Task<List<InternshipItemViewModel>> GetPublishedInternshipsAsync(string? search = null, bool? isRemote = null);
    Task<InternshipItemViewModel?> GetInternshipByIdAsync(Guid id);
    Task<List<InternshipItemViewModel>> GetMyInternshipsAsync();
    Task<(bool Success, string? Error, InternshipItemViewModel? Item)> CreateInternshipAsync(CreateInternshipViewModel model);
    Task<bool> UpdateInternshipStatusAsync(Guid id, string status);

    Task<(bool Success, string? Error)> ApplyAsync(ApplyViewModel model);
    Task<List<ApplicationItemViewModel>> GetMyApplicationsAsync();
    Task<List<ApplicationItemViewModel>> GetInternshipApplicationsAsync(Guid internshipId);
    Task<bool> UpdateApplicationStatusAsync(Guid applicationId, string status);
    Task<bool> WithdrawApplicationAsync(Guid applicationId);
}

