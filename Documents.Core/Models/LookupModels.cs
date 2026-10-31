using System.ComponentModel.DataAnnotations;
using Documents.Core.Rules;

namespace Documents.Core.Models
{
    /// <summary>Apni category banana / badalna (spec 013). Examples = dropdown ke neeche ka hint (optional).</summary>
    public sealed record CategoryRequest(
        [Required(ErrorMessage = "Name is required."), MaxLength(DomainRules.CategoryNameMax, ErrorMessage = "Name is too long.")]
        string Name,
        [MaxLength(DomainRules.CategoryExamplesMax, ErrorMessage = "Hint is too long.")]
        string? Examples);

    /// <summary>Apna relationship banana / badalna (spec 013).</summary>
    public sealed record RelationshipRequest(
        [Required(ErrorMessage = "Name is required."), MaxLength(DomainRules.RelationshipMax, ErrorMessage = "Name is too long.")]
        string Name);

    /// <summary>IsSystem = built-in (rename / delete nahi ho sakta). SortOrder: list order.</summary>
    public sealed record RelationshipDto(Guid Id, string Name, bool IsSystem, int SortOrder);
}
