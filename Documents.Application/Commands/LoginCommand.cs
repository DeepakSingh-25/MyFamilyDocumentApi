using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    public record LoginCommand(LoginRequest Request) : IRequest<Result<LoginResponse>>;

    public class LoginCommandHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens)
        : IRequestHandler<LoginCommand, Result<LoginResponse>>
    {
        // Anjaan email aur galat password ka ek hi message — kaun sa email registered hai, pata na chale
        private const string InvalidMessage = "Invalid email or password.";

        public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken ct)
        {
            var user = await users.GetByEmailAsync(DomainRules.NormalizeEmail(command.Request.Email), ct);
            if (user is null)
            {
                hasher.VerifyDummy(command.Request.Password);
                return Result.Fail<LoginResponse>(ErrorKind.Unauthorized, InvalidMessage);
            }

            if (!hasher.Verify(command.Request.Password, user.PasswordHash))
                return Result.Fail<LoginResponse>(ErrorKind.Unauthorized, InvalidMessage);

            var (token, expiresOn) = tokens.CreateToken(user);
            var dto = new UserDto(user.Id, user.FullName, user.Email, user.CreatedOn);
            return Result.Success(new LoginResponse(token, expiresOn, dto));
        }
    }
}
