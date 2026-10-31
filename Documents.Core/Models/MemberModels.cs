using System.ComponentModel.DataAnnotations;
using Documents.Core.Rules;

namespace Documents.Core.Models
{
    /// <summary>Attribute messages MemberValidation jaise hi (spec 007 §B.3). RelationshipId: system ya apna (spec 013).</summary>
    public sealed record MemberRequest(
        [Required(ErrorMessage = "Name is required."), MaxLength(DomainRules.MemberNameMax, ErrorMessage = "Name is too long.")]
        string Name,
        [Required(ErrorMessage = "Please choose a relationship.")]
        Guid? RelationshipId,
        DateOnly? DateOfBirth);

    /// <summary>Relationship = naam (display ke liye), RelationshipId = dropdown value.</summary>
    public sealed record FamilyMemberDto(Guid Id, string Name, Guid RelationshipId, string Relationship, DateOnly? DateOfBirth, int DocumentCount);
}
