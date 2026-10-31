using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    public record GetMeQuery(Guid UserId) : IRequest<Result<UserDto>>;

    public class GetMeQueryHandler(IUserRepository users) : IRequestHandler<GetMeQuery, Result<UserDto>>
    {
        public async Task<Result<UserDto>> Handle(GetMeQuery request, CancellationToken ct)
        {
            var user = await users.GetByIdAsync(request.UserId, ct);
            return user is null
                ? Result.Fail<UserDto>(ErrorKind.Unauthorized, "Your session has ended. Please log in again.")
                : Result.Success(new UserDto(user.Id, user.FullName, user.Email, user.CreatedOn));
        }
    }
}
