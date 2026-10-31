using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>Profile photo upload / replace. JPG / PNG / WebP / AVIF, max 2 MB, asli header check. Purani file baad mein delete.</summary>
    public record SetProfilePhotoCommand(Guid UserId, UploadedFile File) : IRequest<Result<ProfileDto>>;

    public class SetProfilePhotoCommandHandler(IUserRepository users, IFileStorage storage)
        : IRequestHandler<SetProfilePhotoCommand, Result<ProfileDto>>
    {
        private const string SessionEndedMessage = "Your session has ended. Please log in again.";
        internal const string ChangedMessage = "Your photo was changed at the same time. Please try again.";

        public async Task<Result<ProfileDto>> Handle(SetProfilePhotoCommand command, CancellationToken ct)
        {
            var file = command.File;
            var basic = FileRules.CheckPhotoBasics(file.Length);
            if (basic is not null) return Result.Fail<ProfileDto>(ErrorKind.Validation, basic);
            if (!file.Content.CanSeek) return Result.Fail<ProfileDto>(ErrorKind.Validation, "The file could not be read. Please try again.");

            var header = new byte[FileRules.HeaderLength];
            var read = await file.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            file.Content.Position = 0;
            var type = FileRules.DetectPhotoType(header.AsSpan(0, read));
            if (type is null)
                return Result.Fail<ProfileDto>(ErrorKind.Validation, $"Only {FileRules.PhotoTypesText} photos are allowed.");

            var user = await users.GetByIdAsync(command.UserId, ct);
            if (user is null) return Result.Fail<ProfileDto>(ErrorKind.Unauthorized, SessionEndedMessage);

            var key = await storage.SaveAsync(file.Content, type, ct);
            try
            {
                // Sirf tab jab beech mein doosri request ne photo nahi badli — warna purani file kabhi delete na hoti
                if (!await users.UpdatePhotoIfCurrentAsync(command.UserId, user.PhotoKey, key, type, ct))
                {
                    storage.Delete(key);
                    return Result.Fail<ProfileDto>(ErrorKind.Conflict, ChangedMessage);
                }
            }
            catch
            {
                // Row save nahi hui to file bhi na bache
                storage.Delete(key);
                throw;
            }

            if (user.PhotoKey is not null) storage.Delete(user.PhotoKey);
            user.PhotoKey = key;
            user.PhotoType = type;
            return Result.Success(ProfileDto.From(user));
        }
    }
}
