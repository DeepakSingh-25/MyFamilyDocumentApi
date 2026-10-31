using Documents.Core.Entities;

namespace Documents.Core.Interfaces
{
    /// <summary>Users, one-time sign-up guard aur password reset tokens.</summary>
    public interface IUserRepository
    {
        Task<bool> IsSetupClosedAsync(CancellationToken ct);

        /// <summary>
        /// User + SetupState row ek hi SaveChanges (transaction) mein. False jab sign-up pehle hi band ho
        /// (do saath-saath requests mein se doosri yahin rukti hai).
        /// </summary>
        Task<bool> CreateFirstUserAsync(UserEntity user, DateTime closedOn, CancellationToken ct);

        Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);
        Task<UserEntity?> GetByEmailAsync(string normalizedEmail, CancellationToken ct);
        Task<UserEntity?> GetByIdAsync(Guid userId, CancellationToken ct);
        /// <summary>
        /// Har request par token check ke liye ek hi query: user ka PasswordVersion (null = user nahi mila)
        /// aur kya ye jti logout ho chuka hai.
        /// </summary>
        Task<(int? PasswordVersion, bool Revoked)> GetSessionStateAsync(Guid userId, string jti, CancellationToken ct);

        /// <summary>
        /// Token ko logout (revoke) karta hai. Isi jti ka saath-saath doosra call bhi safe hai. Baad mein expired rows
        /// saaf karta hai (best effort). expiresOn = token ka exp + clock skew.
        /// </summary>
        Task RevokeTokenAsync(Guid userId, string jti, DateTime expiresOn, DateTime now, CancellationToken ct);

        /// <summary>
        /// Naya hash save karta hai, PasswordVersion badhata hai (purane JWT band) aur PasswordChangedOn = now —
        /// teeno ek hi UPDATE mein.
        /// </summary>
        Task UpdatePasswordAsync(Guid userId, string passwordHash, DateTime now, CancellationToken ct);

        /// <summary>
        /// Wahi update, lekin sirf tab jab PasswordVersion abhi bhi expectedVersion ho (beech mein kisi aur ne password
        /// nahi badla). False = row nahi badli.
        /// </summary>
        Task<bool> UpdatePasswordIfVersionAsync(Guid userId, int? expectedVersion, string passwordHash, DateTime now, CancellationToken ct);

        /// <summary>Sirf naam badalta hai (already trimmed + validated). False = user nahi mila.</summary>
        Task<bool> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken ct);

        /// <summary>
        /// Photo key + type set karta hai (dono null = photo hatao), sirf tab jab PhotoKey abhi bhi expectedKey ho
        /// (beech mein doosri request ne photo nahi badli). False = row nahi badli.
        /// </summary>
        Task<bool> UpdatePhotoIfCurrentAsync(Guid userId, string? expectedKey, string? photoKey, string? photoType, CancellationToken ct);

        /// <summary>Naya reset token; us user ke purane unused tokens band ho jate hain.</summary>
        Task AddResetTokenAsync(Guid userId, string tokenHash, DateTime expiresOn, DateTime now, CancellationToken ct);

        /// <summary>Valid token ho to use "used" mark karke naya password save karta hai. False = invalid/expired.</summary>
        Task<bool> ResetPasswordWithTokenAsync(string tokenHash, string passwordHash, DateTime now, CancellationToken ct);
    }
}
