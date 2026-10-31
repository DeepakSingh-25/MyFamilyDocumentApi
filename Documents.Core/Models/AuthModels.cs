using System.ComponentModel.DataAnnotations;
using Documents.Core.Rules;

namespace Documents.Core.Models
{
    // Auth request / response DTOs (spec 001 §3.3 — Auth). JSON camelCase.

    public sealed record SignupRequest(
        [Required, MaxLength(DomainRules.FullNameMax)] string FullName,
        [Required, EmailAddress, MaxLength(DomainRules.EmailMax)] string Email,
        [Required] string Password,
        [Required] string SetupCode);

    public sealed record LoginRequest(
        [Required, EmailAddress, MaxLength(DomainRules.EmailMax)] string Email,
        [Required] string Password);

    public sealed record ForgotPasswordRequest(
        [Required, EmailAddress, MaxLength(DomainRules.EmailMax)] string Email);

    public sealed record ResetPasswordRequest(
        [Required] string Token,
        [Required] string NewPassword);

    public sealed record ChangePasswordRequest(
        [Required] string CurrentPassword,
        [Required] string NewPassword);

    public sealed record RegistrationStatusDto(bool Open);

    public sealed record UserDto(Guid Id, string FullName, string Email, DateTime CreatedOn);

    public sealed record LoginResponse(string Token, DateTime ExpiresOn, UserDto User);

    // My Account (spec 005 §B.1–B.2, B.5)

    /// <summary>Trim aur 1–100 characters ka check handler mein (trimmed value par).</summary>
    public sealed record UpdateProfileRequest(
        [Required(ErrorMessage = "Full name is required.")] string FullName);

    /// <summary>PasswordChangedOn UTC; pehle password change/reset tak null. HasPhoto = GET /api/account/photo milega.</summary>
    public sealed record ProfileDto(Guid Id, string FullName, string Email, DateTime CreatedOn, DateTime? PasswordChangedOn, bool HasPhoto)
    {
        public static ProfileDto From(Entities.UserEntity u) =>
            new(u.Id, u.FullName, u.Email, u.CreatedOn, u.PasswordChangedOn, u.PhotoKey is not null);
    }
}
