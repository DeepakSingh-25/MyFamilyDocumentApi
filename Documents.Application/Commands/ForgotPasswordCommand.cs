using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Options;
using Documents.Core.Rules;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Application.Commands
{
    /// <summary>Hamesha success — taaki koi pata na laga sake kaun sa email registered hai.</summary>
    public record ForgotPasswordCommand(string Email) : IRequest;

    public class ForgotPasswordCommandHandler(
        IUserRepository users,
        IEmailSender email,
        IOptions<AppOptions> app,
        TimeProvider clock,
        ILogger<ForgotPasswordCommandHandler> logger) : IRequestHandler<ForgotPasswordCommand>
    {
        private const int TokenMinutes = 30;

        public async Task Handle(ForgotPasswordCommand command, CancellationToken ct)
        {
            var user = await users.GetByEmailAsync(DomainRules.NormalizeEmail(command.Email), ct);
            if (user is null) return;

            var token = SecurityTokens.NewToken();
            var now = clock.GetUtcNow().UtcDateTime;
            await users.AddResetTokenAsync(user.Id, SecurityTokens.Sha256Hex(token), now.AddMinutes(TokenMinutes), now, ct);

            // Link sirf email mein jata hai — response ya log mein kabhi nahi
            var link = $"{app.Value.WebBaseUrl.TrimEnd('/')}/reset-password?token={token}";
            var body =
                $"Hello {user.FullName},\n\n" +
                $"Someone asked to reset the password of your My Family Documents account.\n" +
                $"Open this link within {TokenMinutes} minutes to choose a new password:\n\n{link}\n\n" +
                "If you did not ask for this, you can ignore this email. Your password stays the same.";
            try
            {
                await email.SendAsync(user.Email, "Reset your My Family Documents password", body, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Response hamesha 202 hi rahe (registered email ka pata na chale). Address / link log nahi karte.
                logger.LogError("Password reset email could not be sent ({ErrorType}).", ex.GetType().Name);
            }
        }
    }
}
