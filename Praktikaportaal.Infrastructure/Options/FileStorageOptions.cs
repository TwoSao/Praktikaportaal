using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Infrastructure.Options;

/// <summary>Settings of the local file storage ("FileStorage" configuration section).</summary>
public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Directory for uploaded CVs. Must be outside wwwroot so files are never served as static content.
    /// A relative path is resolved against the application's content root.
    /// </summary>
    [Required]
    public string RootPath { get; set; } = "App_Data/cvs";
}
