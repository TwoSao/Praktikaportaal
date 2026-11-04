namespace Praktikaportaal.Core.Entities;

/// <summary>
/// CV (curriculum vitae) metadata record attached to a student.
/// Only file metadata and a storage reference are persisted here;
/// the binary content is stored in an external file storage (blob storage / file system).
/// A student may have multiple CVs (e.g. language variants, updated versions).
/// </summary>
public class CV : BaseEntity
{
    /// <summary>FK to the owning student.</summary>
    public Guid StudentProfileId { get; set; }

    /// <summary>Original file name as uploaded by the student. Max 255 chars.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Storage key / relative path used to retrieve the file from blob storage.
    /// Max 500 chars. Never stores raw file bytes.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>MIME type of the file (e.g. "application/pdf"). Max 100 chars.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the file was uploaded.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    public StudentProfile StudentProfile { get; set; } = null!;
}
