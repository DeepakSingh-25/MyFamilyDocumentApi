using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>
    /// Password badalta hai aur isi device ke liye naya token deta hai (spec 005 §B.4). Naye token mein naya pwv hai,
    /// isliye ye tab signed-in rehta hai; baaki sab tokens (purana wala bhi) agli request par 401.
    /// </summary>
    public record ChangePasswordCommand(Guid UserId, ChangePasswordRequest Request) : IRequest<Result<LoginResponse>>;

    public class ChangePasswordCommandHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, TimeProvider clock)
        : IRequestHandler<ChangePasswordCommand, Result<LoginResponse>>
    {
        private const string SessionEndedMessage = "Your session has ended. Please log in again.";

        public async Task<Result<LoginResponse>> Handle(ChangePasswordCommand command, CancellationToken ct)
        {
            var req = command.Request;
            var user = await users.GetByIdAsync(command.UserId, ct);
            if (user is null) return Result.Fail<LoginResponse>(ErrorKind.Unauthorized, SessionEndedMessage);

            if (!hasher.Verify(req.CurrentPassword, user.PasswordHash))
                return Result.Fail<LoginResponse>(ErrorKind.Validation, "Current password is not correct.");
            if (req.CurrentPassword == req.NewPassword)
                return Result.Fail<LoginResponse>(ErrorKind.Validation, "New password must be different from the current one.");

            var problem = DomainRules.CheckPassword(req.NewPassword);
            if (problem is not null) return Result.Fail<LoginResponse>(ErrorKind.Validation, problem);

            // PasswordVersion badhta hai → baaki sab sessions ke token band; PasswordChangedOn = abhi (UTC)
            // Sirf tab jab version wahi ho jo humne check kiya — do saath-saath requests mein se ek hi jeetti hai
            var changed = await users.UpdatePasswordIfVersionAsync(
                user.Id, user.PasswordVersion, hasher.Hash(req.NewPassword), clock.GetUtcNow().UtcDateTime, ct);
            if (!changed)
                return Result.Fail<LoginResponse>(ErrorKind.Conflict, "Your password was just changed. Please try again.");

            // Dobara load karo taaki naye token mein naya PasswordVersion (pwv) jaye
            var updated = await users.GetByIdAsync(user.Id, ct);
            if (updated is null) return Result.Fail<LoginResponse>(ErrorKind.Unauthorized, SessionEndedMessage);

            var (token, expiresOn) = tokens.CreateToken(updated);
            var dto = new UserDto(updated.Id, updated.FullName, updated.Email, updated.CreatedOn);
            return Result.Success(new LoginResponse(token, expiresOn, dto));
        }
    }
}
