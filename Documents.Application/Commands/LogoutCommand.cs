using Documents.Core.Interfaces;
using Documents.Core.Options;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>Server-side logout: is token ka jti revoke — uske baad ye token kahin bhi kaam nahi karega.</summary>
    public record LogoutCommand(Guid UserId, string Jti, DateTime TokenExpiresOn) : IRequest;

    public class LogoutCommandHandler(IUserRepository users, TimeProvider clock) : IRequestHandler<LogoutCommand>
    {
        // Row tab tak rakho jab tak token sach mein accept ho sakta hai: exp + clock skew
        public Task Handle(LogoutCommand command, CancellationToken ct) =>
            users.RevokeTokenAsync(command.UserId, command.Jti, command.TokenExpiresOn + JwtOptions.ClockSkew,
                clock.GetUtcNow().UtcDateTime, ct);
    }
}
