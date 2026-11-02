namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Base class for all domain entities.
/// Provides a stable Guid identifier and UTC audit timestamps.
/// Soft-delete flag (IsDeleted) prevents accidental history loss when
/// users or companies are deactivated.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Unique identifier (primary key). Assigned by the application, not the database.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>UTC timestamp of creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update. Null if the entity has never been modified.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete flag. Deleted entities are hidden from normal queries but
    /// retained in the database to preserve referential history
    /// (applications, internship records, etc.).
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
