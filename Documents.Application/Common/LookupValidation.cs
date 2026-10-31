using System.Globalization;
using Documents.Core.Models;
using Documents.Core.Rules;

namespace Documents.Application.Common
{
    /// <summary>Apni categories / relationships ke checks (spec 013). Messages frontend quick-add dialog jaise.</summary>
    internal static class LookupValidation
    {
        public const string SystemMessage = "Built-in items cannot be changed or deleted.";
        public static readonly string TooManyMessage = $"You can add at most {DomainRules.CustomLookupMax} of your own items.";
        private const string HiddenCharactersMessage = "The text contains characters that are not allowed.";

        public static string? CheckCategory(CategoryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
            if (req.Name.Trim().Length > DomainRules.CategoryNameMax) return "Name is too long.";
            if (HasHiddenCharacters(req.Name) || HasHiddenCharacters(req.Examples)) return HiddenCharactersMessage;
            if ((req.Examples?.Trim().Length ?? 0) > DomainRules.CategoryExamplesMax) return "Hint is too long.";
            return null;
        }

        public static string? CheckRelationship(RelationshipRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return "Name is required.";
            if (req.Name.Trim().Length > DomainRules.RelationshipMax) return "Name is too long.";
            if (HasHiddenCharacters(req.Name)) return HiddenCharactersMessage;
            return null;
        }

        /// <summary>Control / invisible (zero-width, bidi) characters — "Self" + zero-width space jaisa naam asli "Self" ki nakal na bane.</summary>
        private static bool HasHiddenCharacters(string? text) =>
            text is not null && text.Any(c => char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format);
    }
}
