namespace Documents.Core.Entities
{
    /// <summary>Reset token — sirf SHA-256 hash store hota hai, token kabhi nahi.</summary>
    public class PasswordResetTokenEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresOn { get; set; }
        public DateTime? UsedOn { get; set; }
        public UserEntity User { get; set; } = null!;
    }
}
