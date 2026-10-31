using Documents.Application.Common;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Options;
using Documents.Core.Rules;
using MediatR;
using Microsoft.Extensions.Options;

namespace Documents.Application.Commands
{
    public record SignupCommand(SignupRequest Request) : IRequest<Result>;

    /// <summary>
    /// One-time sign-up. Checks ka order mock jaisa (messages same rahein):
    /// band? → setup code → email unique → create (aur sign-up band).
    /// </summary>
    public class SignupCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        IOptions<SignupOptions> signup,
        TimeProvider clock) : IRequestHandler<SignupCommand, Result>
    {
        private const string ClosedMessage = "Sign up is closed. Ask the account owner for access.";

        public async Task<Result> Handle(SignupCommand command, CancellationToken ct)
        {
            var req = command.Request;
            var setupCode = signup.Value.SetupCode;

            if (string.IsNullOrWhiteSpace(setupCode) || await users.IsSetupClosedAsync(ct))
                return Result.Fail(ErrorKind.Forbidden, ClosedMessage);

            if (!SecurityTokens.FixedTimeEquals(req.SetupCode.Trim(), setupCode.Trim()))
                return Result.Fail(ErrorKind.Validation, "The setup code is not correct.");

            if (string.IsNullOrWhiteSpace(req.FullName))
                return Result.Fail(ErrorKind.Validation, "Name is required.");

            var passwordProblem = DomainRules.CheckPassword(req.Password);
            if (passwordProblem is not null) return Result.Fail(ErrorKind.Validation, passwordProblem);

            var email = DomainRules.NormalizeEmail(req.Email);
            if (await users.EmailExistsAsync(email, ct))
                return Result.Fail(ErrorKind.Conflict, "An account with this email already exists.");

            var now = clock.GetUtcNow().UtcDateTime;
            var user = new UserEntity
            {
                FullName = req.FullName.Trim(),
                Email = email,
                PasswordHash = hasher.Hash(req.Password),
                CreatedOn = now,
            };

            return await users.CreateFirstUserAsync(user, now, ct)
                ? Result.Success()
                : Result.Fail(ErrorKind.Forbidden, ClosedMessage);
        }
    }
}
