using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

/// <summary>Everything a logged-in student does: profile and skills, CV, own applications, recommendations.</summary>
[Authorize(Roles = nameof(UserRole.Student))]
public class StudentController : Controller
{
    private const int PageSize = 10;
    private const long MaxCvBytes = 5 * 1024 * 1024;

    private readonly IApiClient _api;

    public StudentController(IApiClient api)
    {
        _api = api;
    }

    // ── profile & skills ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Profile() => View(await LoadProfileAsync());

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _api.UpdateMyProfileAsync(new UpdateStudentProfileDto
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    SchoolName = model.SchoolName.Trim(),
                    StudyProgram = model.StudyProgram.Trim(),
                    Course = model.Course,
                    Description = Blank(model.Description),
                    PhoneNumber = Blank(model.PhoneNumber),
                    Location = Blank(model.Location),
                    Skills = model.Skills
                        .Where(s => s.Selected)
                        .Select(s => new StudentSkillInputDto { SkillId = s.SkillId, Level = s.Level })
                        .ToList()
                });

                TempData["SuccessMessage"] = "Profiil on salvestatud.";
                return RedirectToAction(nameof(Profile));
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                ModelState.AddApiError(ex);
            }
        }

        // Show the form again with what the user typed; the skill names and the CV are not posted, so reload them.
        var names = (await _api.GetSkillsAsync()).ToDictionary(s => s.Id, s => s.Name);
        foreach (var skill in model.Skills)
            skill.Name = names.GetValueOrDefault(skill.SkillId, skill.Name);
        model.Cv = await _api.GetMyCvAsync();
        return View(model);
    }

    // ── CV ──────────────────────────────────────────────────────────────────

    [HttpPost]
    [RequestSizeLimit(MaxCvBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadCv(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Vali CV fail (PDF või DOCX).";
            return RedirectToAction(nameof(Profile), null, "cv");
        }

        if (file.Length > MaxCvBytes)
        {
            TempData["ErrorMessage"] = "Fail on liiga suur. Maksimaalne suurus on 5 MB.";
            return RedirectToAction(nameof(Profile), null, "cv");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await _api.UploadMyCvAsync(stream, file.FileName, file.ContentType);
            TempData["SuccessMessage"] = "CV on üles laaditud.";
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            TempData["ErrorMessage"] = ex.UserMessage(); // e.g. wrong file type or a file that is not really a PDF
        }

        return RedirectToAction(nameof(Profile), null, "cv");
    }

    [HttpGet]
    public async Task<IActionResult> DownloadCv()
    {
        var file = await _api.DownloadMyCvAsync();
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCv()
    {
        await _api.DeleteMyCvAsync();
        TempData["SuccessMessage"] = "CV on kustutatud.";
        return RedirectToAction(nameof(Profile), null, "cv");
    }

    // ── applications ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Applications(ApplicationStatus? status, int page = 1)
    {
        var result = await _api.GetMyApplicationsAsync(new ApplicationQueryParameters
        {
            Status = status, Page = Math.Max(1, page), PageSize = PageSize
        });
        return View(new StudentApplicationsViewModel { Result = result, Status = status });
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        try
        {
            await _api.WithdrawApplicationAsync(id);
            TempData["SuccessMessage"] = "Avaldus on tagasi võetud.";
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            TempData["ErrorMessage"] = ex.UserMessage();
        }

        return RedirectToAction(nameof(Applications));
    }

    // ── recommendations ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Recommendations(double minOverlap = 1, int page = 1)
    {
        minOverlap = Math.Clamp(minOverlap, 0, 100);
        var profile = await _api.GetMyProfileAsync();
        var result = await _api.GetRecommendationsAsync(profile.Id, new RecommendationQueryParameters
        {
            MinOverlap = minOverlap, Page = Math.Max(1, page), PageSize = PageSize
        });

        return View(new RecommendationsViewModel { Result = result, MinOverlap = minOverlap, HasSkills = profile.Skills.Count > 0 });
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<ProfileViewModel> LoadProfileAsync()
    {
        var profile = await _api.GetMyProfileAsync();
        var allSkills = await _api.GetSkillsAsync();
        var mine = profile.Skills.ToDictionary(s => s.SkillId, s => s.Level);

        return new ProfileViewModel
        {
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            SchoolName = profile.SchoolName,
            StudyProgram = profile.StudyProgram,
            Course = profile.Course,
            Description = profile.Description,
            PhoneNumber = profile.PhoneNumber,
            Location = profile.Location,
            Cv = profile.HasCv ? await _api.GetMyCvAsync() : null,
            Skills = allSkills.Select(s => new SkillSelectionViewModel
            {
                SkillId = s.Id,
                Name = s.Name,
                Selected = mine.ContainsKey(s.Id),
                Level = mine.GetValueOrDefault(s.Id, SkillLevel.Beginner)
            }).ToList()
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
