using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Web.Services;

/// <summary>Estonian labels and Bootstrap badge classes for enums shown in the UI.</summary>
public static class UiText
{
    public static string Label(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Submitted => "Esitatud",
        ApplicationStatus.Viewed => "Vaadatud",
        ApplicationStatus.Interview => "Intervjuu",
        ApplicationStatus.Accepted => "Aktsepteeritud",
        ApplicationStatus.Rejected => "Tagasi lükatud",
        ApplicationStatus.Withdrawn => "Tagasi võetud",
        _ => status.ToString()
    };

    public static string BadgeClass(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Submitted => "bg-secondary",
        ApplicationStatus.Viewed => "bg-info text-dark",
        ApplicationStatus.Interview => "bg-warning text-dark",
        ApplicationStatus.Accepted => "bg-success",
        ApplicationStatus.Rejected => "bg-danger",
        ApplicationStatus.Withdrawn => "bg-light text-muted border",
        _ => "bg-secondary"
    };

    /// <summary>Text of the button that moves an application to <paramref name="status"/>.</summary>
    public static string ActionLabel(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Viewed => "Märgi vaadatuks",
        ApplicationStatus.Interview => "Kutsu intervjuule",
        ApplicationStatus.Accepted => "Aktsepteeri",
        ApplicationStatus.Rejected => "Lükka tagasi",
        _ => status.Label()
    };

    public static string ActionButtonClass(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Accepted => "btn-success",
        ApplicationStatus.Rejected => "btn-outline-danger",
        ApplicationStatus.Interview => "btn-warning",
        _ => "btn-outline-primary"
    };

    public static string Label(this InternshipStatus status) => status switch
    {
        InternshipStatus.Draft => "Mustand",
        InternshipStatus.Published => "Avaldatud",
        InternshipStatus.Closed => "Suletud",
        InternshipStatus.Archived => "Arhiveeritud",
        _ => status.ToString()
    };

    public static string BadgeClass(this InternshipStatus status) => status switch
    {
        InternshipStatus.Published => "bg-success",
        InternshipStatus.Draft => "bg-secondary",
        InternshipStatus.Closed => "bg-warning text-dark",
        InternshipStatus.Archived => "bg-dark",
        _ => "bg-secondary"
    };

    public static string Label(this SkillLevel level) => level switch
    {
        SkillLevel.Beginner => "Algaja",
        SkillLevel.Intermediate => "Kesktase",
        SkillLevel.Advanced => "Edasijõudnud",
        SkillLevel.Expert => "Ekspert",
        _ => level.ToString()
    };

    public static string Label(this InternshipSort sort) => sort switch
    {
        InternshipSort.Newest => "Uuemad enne",
        InternshipSort.Oldest => "Vanemad enne",
        InternshipSort.Deadline => "Tähtaeg lähim",
        InternshipSort.Title => "Pealkirja järgi",
        _ => sort.ToString()
    };

    public static string Label(this StatisticsPeriod period) => period switch
    {
        StatisticsPeriod.Day => "Päev",
        StatisticsPeriod.Week => "Nädal",
        StatisticsPeriod.Month => "Kuu",
        _ => period.ToString()
    };

    public static string Label(this UserRole role) => role switch
    {
        UserRole.Student => "Õppur",
        UserRole.Company => "Ettevõte",
        UserRole.Admin => "Administraator",
        UserRole.Service => "Teenusekonto",
        _ => role.ToString()
    };

    public static string Date(DateTime? value) => value?.ToString("dd.MM.yyyy") ?? "—";

    public static string DateTimeText(DateTime? value) => value?.ToString("dd.MM.yyyy HH:mm") ?? "—";

    /// <summary>Short relative text for notifications ("2 h tagasi"); the API stores UTC.</summary>
    public static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (span.TotalMinutes < 1) return "just praegu";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min tagasi";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h tagasi";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} p tagasi";
        return Date(utc);
    }
}
