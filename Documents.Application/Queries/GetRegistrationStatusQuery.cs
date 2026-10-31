using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Application.Queries
{
    public record GetRegistrationStatusQuery : IRequest<RegistrationStatusDto>;

    public class GetRegistrationStatusQueryHandler(
        IUserRepository users,
        IOptions<SignupOptions> signup,
        ILogger<GetRegistrationStatusQueryHandler> logger) : IRequestHandler<GetRegistrationStatusQuery, RegistrationStatusDto>
    {
        private static int warned;

        public async Task<RegistrationStatusDto> Handle(GetRegistrationStatusQuery request, CancellationToken ct)
        {
            if (await users.IsSetupClosedAsync(ct)) return new RegistrationStatusDto(false);

            // Code set nahi hai to sign-up band (fail closed). Code ki value kabhi log nahi hoti.
            if (string.IsNullOrWhiteSpace(signup.Value.SetupCode))
            {
                // Anonymous endpoint hai — warning sirf ek baar, logs na bharein
                if (Interlocked.Exchange(ref warned, 1) == 0)
                    logger.LogWarning("Sign-up is closed because Signup:SetupCode is not configured.");
                return new RegistrationStatusDto(false);
            }

            return new RegistrationStatusDto(true);
        }
    }
}
