using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<Result>;

    public class ResetPasswordCommandHandler(IUserRepository users, IPasswordHasher hasher, TimeProvider clock)
        : IRequestHandler<ResetPasswordCommand, Result>
    {
        public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken ct)
        {
            var problem = DomainRules.CheckPassword(command.Request.NewPassword);
            if (problem is not null) return Result.Fail(ErrorKind.Validation, problem);

            var ok = await users.ResetPasswordWithTokenAsync(
                SecurityTokens.Sha256Hex(command.Request.Token.Trim()),
                hasher.Hash(command.Request.NewPassword),
                clock.GetUtcNow().UtcDateTime,
                ct);

            return ok ? Result.Success() : Result.Fail(ErrorKind.Validation, "This reset link is invalid or has expired.");
        }
    }
}
