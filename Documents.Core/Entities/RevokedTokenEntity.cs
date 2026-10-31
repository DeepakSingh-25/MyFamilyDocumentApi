namespace Documents.Core.Entities
{
    /// <summary>
    /// Logout kiya hua token (uske "jti" se). Token apni expiry tak yahan rehta hai; uske baad row bekaar hai
    /// aur agle logout par saaf ho jati hai.
    /// </summary>
    public class RevokedTokenEntity
    {
        public string Jti { get; set; } = null!;
        public Guid UserId { get; set; }
        public DateTime ExpiresOn { get; set; }
        public UserEntity User { get; set; } = null!;
    }
}
