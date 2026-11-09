using Praktikaportaal.Core.Exceptions;

namespace Praktikaportaal.Core.Services;

/// <summary>A CV format the portal accepts.</summary>
public sealed record CvFileType(string Extension, string ContentType);

/// <summary>
/// Validation of uploaded CV files: allowed type (PDF, DOCX), maximum size and file signature
/// ("magic bytes"), so a renamed executable is not accepted just because its name ends in .pdf.
/// </summary>
public static class CvFileRules
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, CvFileType> ByExtension = new()
    {
        [".pdf"] = new(".pdf", "application/pdf"),
        [".docx"] = new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
    };

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04]; // DOCX is a ZIP container

    /// <summary>Number of leading bytes needed by <see cref="HasValidSignature"/>.</summary>
    public const int SignatureLength = 8;

    /// <summary>Checks name, declared content type and size. Returns the accepted file type.</summary>
    public static CvFileType Validate(string? fileName, string? contentType, long length)
    {
        if (length <= 0)
            throw new BusinessRuleException("The uploaded file is empty.");

        if (length > MaxSizeBytes)
            throw new BusinessRuleException($"The file is too large. Maximum size is {MaxSizeBytes / (1024 * 1024)} MB.");

        string extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!ByExtension.TryGetValue(extension, out var type))
            throw new BusinessRuleException("Only PDF (.pdf) and Word (.docx) files are allowed.");

        // Browsers sometimes send application/octet-stream; anything else must match the extension.
        string declared = (contentType ?? string.Empty).Split(';')[0].Trim();
        if (declared.Length > 0 &&
            !declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) &&
            !declared.Equals(type.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException($"The content type '{declared}' does not match a {type.Extension} file.");
        }

        return type;
    }

    /// <summary>True if the first bytes of the file match the format.</summary>
    public static bool HasValidSignature(CvFileType type, ReadOnlySpan<byte> header) => type.Extension switch
    {
        ".pdf" => header.StartsWith(PdfSignature),
        ".docx" => header.StartsWith(ZipSignature),
        _ => false
    };

    /// <summary>Strips any path and unsafe characters from the client-supplied name; limited to 150 characters.</summary>
    public static string SanitizeFileName(string? fileName, CvFileType type)
    {
        string name = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName ?? string.Empty));
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim();

        if (name.Length > 150)
            name = name[..150];
        if (name.Length == 0)
            name = "cv";

        return name + type.Extension;
    }
}
