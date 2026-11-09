using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Core.DTOs;

public class SkillDto
{
    public Guid Id { get; set; }

    /// <example>C#</example>
    public string Name { get; set; } = string.Empty;

    /// <example>Object-oriented programming language for .NET.</example>
    public string? Description { get; set; }
}

public class CreateSkillDto
{
    /// <example>Kubernetes</example>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <example>Container orchestration platform.</example>
    [MaxLength(500)]
    public string? Description { get; set; }
}
