using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>Profile photo hatao. Photo pehle se na ho to bhi success (idempotent).</summary>
    public record DeleteProfilePhotoCommand(Guid UserId) : IRequest<Result<ProfileDto>>;

    public class DeleteProfilePhotoCommandHandler(IUserRepository users, IFileStorage storage)
        : IRequestHandler<DeleteProfilePhotoCommand, Result<ProfileDto>>
    {
        public async Task<Result<ProfileDto>> Handle(DeleteProfilePhotoCommand command, CancellationToken ct)
        {
            var user = await users.GetByIdAsync(command.UserId, ct);
            if (user is null)
                return Result.Fail<ProfileDto>(ErrorKind.Unauthorized, "Your session has ended. Please log in again.");
            if (!await users.UpdatePhotoIfCurrentAsync(command.UserId, user.PhotoKey, null, null, ct))
                return Result.Fail<ProfileDto>(ErrorKind.Conflict, SetProfilePhotoCommandHandler.ChangedMessage);

            if (user.PhotoKey is not null) storage.Delete(user.PhotoKey);
            user.PhotoKey = null;
            user.PhotoType = null;
            return Result.Success(ProfileDto.From(user));
        }
    }
}
