using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>
    /// Sirf full name badalta hai (spec 005 §B.2; email change nahi). JWT ka "name" claim agle login tak purana rehta hai —
    /// web app naam API response se padhta hai.
    /// </summary>
    public record UpdateProfileCommand(Guid UserId, UpdateProfileRequest Request) : IRequest<Result<ProfileDto>>;

    public class UpdateProfileCommandHandler(IUserRepository users) : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
    {
        private const string SessionEndedMessage = "Your session has ended. Please log in again.";

        public async Task<Result<ProfileDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
        {
            // Trimmed value par check — "  Rohit  " → "Rohit"
            var fullName = command.Request.FullName?.Trim() ?? "";
            if (fullName.Length == 0)
                return Result.Fail<ProfileDto>(ErrorKind.Validation, "Full name is required.");
            if (fullName.Length > DomainRules.FullNameMax)
                return Result.Fail<ProfileDto>(ErrorKind.Validation,
                    $"Full name is too long (maximum {DomainRules.FullNameMax} characters).");

            if (!await users.UpdateFullNameAsync(command.UserId, fullName, ct))
                return Result.Fail<ProfileDto>(ErrorKind.Unauthorized, SessionEndedMessage);

            var user = await users.GetByIdAsync(command.UserId, ct);
            return user is null
                ? Result.Fail<ProfileDto>(ErrorKind.Unauthorized, SessionEndedMessage)
                : Result.Success(ProfileDto.From(user));
        }
    }
}
