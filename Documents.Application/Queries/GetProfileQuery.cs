using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    /// <summary>My Account ka profile (spec 005 §B.1). Id hamesha token se, request se kabhi nahi.</summary>
    public record GetProfileQuery(Guid UserId) : IRequest<Result<ProfileDto>>;

    public class GetProfileQueryHandler(IUserRepository users) : IRequestHandler<GetProfileQuery, Result<ProfileDto>>
    {
        public async Task<Result<ProfileDto>> Handle(GetProfileQuery request, CancellationToken ct)
        {
            var user = await users.GetByIdAsync(request.UserId, ct);
            return user is null
                ? Result.Fail<ProfileDto>(ErrorKind.Unauthorized, "Your session has ended. Please log in again.")
                : Result.Success(ProfileDto.From(user));
        }
    }
}
