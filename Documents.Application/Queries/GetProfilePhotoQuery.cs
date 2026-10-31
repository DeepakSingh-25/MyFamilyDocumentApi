using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Queries
{
    /// <summary>Signed-in user ki apni profile photo. Photo nahi to 404.</summary>
    public record GetProfilePhotoQuery(Guid UserId) : IRequest<Result<DocumentFileDto>>;

    public class GetProfilePhotoQueryHandler(IUserRepository users, IFileStorage storage)
        : IRequestHandler<GetProfilePhotoQuery, Result<DocumentFileDto>>
    {
        public async Task<Result<DocumentFileDto>> Handle(GetProfilePhotoQuery request, CancellationToken ct)
        {
            var user = await users.GetByIdAsync(request.UserId, ct);
            if (user?.PhotoKey is null || user.PhotoType is null)
                return Result.Fail<DocumentFileDto>(ErrorKind.NotFound, "No profile photo.");

            var stream = storage.OpenRead(user.PhotoKey);
            return stream is null
                ? Result.Fail<DocumentFileDto>(ErrorKind.NotFound, "No profile photo.")
                : Result.Success(new DocumentFileDto(stream, FileRules.MimeFor(user.PhotoType), $"profile.{user.PhotoType}"));
        }
    }
}
