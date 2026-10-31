using Documents.Core.Models;
using Documents.Core.Rules;

namespace Documents.Application.Common
{
    internal static class MemberValidation
    {
        public const string SelfTakenMessage = "Only one member can be 'Self'.";
        public const string RelationshipMessage = "Please choose a relationship.";

        /// <summary>Error message ya null. Relationship sach mein is user ko dikhta hai ya nahi — handler DB se check karta hai.</summary>
        public static string? Check(MemberRequest req, DateOnly today)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
            if (req.Name.Trim().Length > DomainRules.MemberNameMax) return "Name is too long.";
            if (req.RelationshipId is null || req.RelationshipId == Guid.Empty) return RelationshipMessage;
            if (req.DateOfBirth < DomainRules.MinDateOfBirth) return "Please enter a valid date of birth.";
            if (req.DateOfBirth > today) return "Date of birth cannot be in the future.";
            return null;
        }
    }
}
