namespace Praktikaportaal.Core.DTOs;

/// <summary>Metadata of a student's current CV (the file itself is downloaded separately).</summary>
public class CvDto
{
    public Guid Id { get; set; }

    /// <example>Mari_Tamm_CV.pdf</example>
    public string FileName { get; set; } = string.Empty;

    /// <example>application/pdf</example>
    public string ContentType { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }
}

/// <summary>A CV file ready to be streamed to the client. The caller disposes <see cref="Content"/>.</summary>
public sealed record CvFile(Stream Content, string FileName, string ContentType);
