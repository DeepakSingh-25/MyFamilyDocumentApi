namespace Documents.Core.Entities
{
    /// <summary>Login account. Sirf ek account banta hai (one-time sign-up).</summary>
    public class UserEntity
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        /// <summary>Hamesha trimmed + lower-case.</summary>
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        /// <summary>Password change/reset par badhta hai; JWT ka "pwv" claim isse match hona chahiye.</summary>
        public int PasswordVersion { get; set; }
        public DateTime CreatedOn { get; set; }
        /// <summary>UTC. Change-password ya reset-password par set hota hai; pehli baar tak null.</summary>
        public DateTime? PasswordChangedOn { get; set; }
        /// <summary>Profile photo ka private storage key; photo nahi to null.</summary>
        public string? PhotoKey { get; set; }
        /// <summary>"jpg" ya "png" — PhotoKey ke saath hi set/clear.</summary>
        public string? PhotoType { get; set; }
    }
}
