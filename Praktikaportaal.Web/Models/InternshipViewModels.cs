using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Web.Models;

/// <summary>The public internship list with its filter form.</summary>
public class InternshipListViewModel
{
    public required PagedResult<InternshipDto> Result { get; init; }
    public required InternshipQueryParameters Query { get; init; }
    public required InternshipFiltersDto Filters { get; init; }

    public bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(Query.Search) || !string.IsNullOrWhiteSpace(Query.Field) ||
        !string.IsNullOrWhiteSpace(Query.Location) || Query.IsRemote.HasValue;
}

public class InternshipDetailsViewModel
{
    public required InternshipDto Internship { get; init; }

    /// <summary>The logged-in student already has an active (not withdrawn) application for this internship.</summary>
    public ApplicationDto? ExistingApplication { get; init; }

    /// <summary>The logged-in company owns the internship, or the user is an admin.</summary>
    public bool CanManage { get; init; }

    public bool IsAdmin { get; init; }
}

public class HomeViewModel
{
    public IReadOnlyList<InternshipDto> Latest { get; init; } = [];
    public InternshipFiltersDto Filters { get; init; } = new();
    public int TotalInternships { get; init; }
    public bool ApiAvailable { get; init; } = true;
}
