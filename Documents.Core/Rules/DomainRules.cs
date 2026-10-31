namespace Documents.Core.Rules
{
    /// <summary>Frontend ke saath shared rules (spec 001 §4). Dono taraf same rehne chahiye.</summary>
    public static class DomainRules
    {
        /// <summary>System "Medical Documents" category (purana id 3) — web app ke saath contract (spec 014).</summary>
        public static readonly Guid MedicalCategoryId = SystemCategoryId(3);

        public const int FullNameMax = 100;
        public const int EmailMax = 256;
        public const int MemberNameMax = 100;
        public const int RelationshipMax = 30;
        public const int DocumentNameMax = 150;
        public const int HospitalOrDoctorMax = 150;
        public const int NotesMax = 1000;
        /// <summary>Documents list ka search text (spec 009 §B.1).</summary>
        public const int SearchMax = 100;

        /// <summary>frontend/src/app/shared/validators.ts STRONG_PASSWORD</summary>
        public const string StrongPasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";
        public const string StrongPasswordMessage = "Use 8+ characters with upper case, lower case and a number.";
        /// <summary>BCrypt 72 bytes ke baad ignore karta hai.</summary>
        public const int PasswordMaxBytes = 72;

        /// <summary>Account owner khud — system relationship "Self"; har account mein sirf ek active Self (spec 007 §B.2).</summary>
        public static readonly Guid SelfRelationshipId = SystemRelationshipId(1);
        /// <summary>Isse pehle ki date of birth nahi (spec 007 §B.1).</summary>
        public static readonly DateOnly MinDateOfBirth = new(1900, 1, 1);

        /// <summary>System relationships ka seed — number = index + 1 (Self = 1, Other = 13), id = SystemRelationshipId(number). Order mat badlo (spec 013).</summary>
        public static readonly IReadOnlyList<string> SystemRelationships =
        [
            "Self", "Spouse", "Father", "Mother", "Son", "Daughter", "Brother", "Sister",
            "Grandfather", "Grandmother", "Daughter-in-law", "Son-in-law", "Other",
        ];

        // Apni categories / relationships (spec 013)
        public const int CategoryNameMax = 60;
        public const int CategoryExamplesMax = 300;
        /// <summary>Har user ki apni categories aur apne relationships — dono alag-alag itne tak.</summary>
        public const int CustomLookupMax = 50;
        /// <summary>Custom rows system rows ke baad lekin "Other" (1000) se pehle dikhte hain.</summary>
        public const int CustomSortOrder = 500;
        public const int OtherSortOrder = 1000;

        /// <summary>
        /// System rows ke fixed GUIDs (spec 014): aakhri 12 digits = purana int id (decimal), zero padded.
        /// Seed (HasData) aur migration ConvertIdsToGuid dono yahi values use karte hain.
        /// </summary>
        public static Guid SystemCategoryId(int number) => SystemGuid("10000000", number);

        /// <summary>Relationships: 20000000-0000-0000-0000-0000000000NN, NN = 01…13.</summary>
        public static Guid SystemRelationshipId(int number) => SystemGuid("20000000", number);

        private static Guid SystemGuid(string prefix, int number) =>
            number is >= 1 and <= 999_999
                ? Guid.Parse($"{prefix}-0000-0000-0000-{number:D12}")
                : throw new ArgumentOutOfRangeException(nameof(number));

        public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        /// <summary>Strength regex ke alawa BCrypt ki 72-byte limit. Error message ya null.</summary>
        public static string? CheckPassword(string password)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, StrongPasswordPattern)) return StrongPasswordMessage;
            if (System.Text.Encoding.UTF8.GetByteCount(password) > PasswordMaxBytes) return "Password is too long (maximum 72 characters).";
            return null;
        }
    }
}
